using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

namespace DynamicCombat
{
    public class CombatRegistry
    {
        private static CombatRegistry _instance;
        public static CombatRegistry Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new CombatRegistry();
                return _instance;
            }
        }

        // Target to active attackers list
        private Dictionary<Agent, List<Agent>> _activeEngagements = new Dictionary<Agent, List<Agent>>(128);

        // Target to queued attackers list
        private Dictionary<Agent, List<Agent>> _queuedAttackers = new Dictionary<Agent, List<Agent>>(128);

        // Track what target each attacker is currently assigned to, to easily clean up when they switch targets.
        private Dictionary<Agent, Agent> _attackerCurrentTarget = new Dictionary<Agent, Agent>(512);

        // Blacklisted targets for an attacker and the expiration time
        private Dictionary<Agent, Dictionary<Agent, float>> _blacklistedTargets = new Dictionary<Agent, Dictionary<Agent, float>>(512);

        // Timer for how long an agent has been in the queue
        private Dictionary<Agent, float> _queueTimers = new Dictionary<Agent, float>(512);

        // Track agent health to detect damage interrupts
        private Dictionary<Agent, float> _agentHealths = new Dictionary<Agent, float>(512);

        public void Clear()
        {
            _activeEngagements.Clear();
            _queuedAttackers.Clear();
            _attackerCurrentTarget.Clear();
            _blacklistedTargets.Clear();
            _queueTimers.Clear();
            _agentHealths.Clear();
        }

        public void BlacklistTarget(Agent attacker, Agent target, float currentTime, float durationSeconds)
        {
            if (attacker == null || target == null) return;
            if (!_blacklistedTargets.TryGetValue(attacker, out var blacklist))
            {
                blacklist = new Dictionary<Agent, float>();
                _blacklistedTargets[attacker] = blacklist;
            }
            blacklist[target] = currentTime + durationSeconds;
        }

        public bool IsBlacklisted(Agent attacker, Agent target, float currentTime)
        {
            if (attacker == null || target == null) return false;
            if (_blacklistedTargets.TryGetValue(attacker, out var blacklist))
            {
                if (blacklist.TryGetValue(target, out float expirationTime))
                {
                    if (currentTime < expirationTime) return true;
                    // Expired
                    blacklist.Remove(target);
                }
            }
            return false;
        }

        public bool DidTakeDamage(Agent attacker)
        {
            if (attacker == null || !attacker.IsActive()) return false;
            float currentHealth = attacker.Health;
            if (_agentHealths.TryGetValue(attacker, out float previousHealth))
            {
                if (currentHealth < previousHealth)
                {
                    _agentHealths[attacker] = currentHealth;
                    return true;
                }
            }
            _agentHealths[attacker] = currentHealth;
            return false;
        }

        public void IncrementQueueTimer(Agent attacker, float dt)
        {
            if (attacker == null) return;
            if (_queueTimers.TryGetValue(attacker, out float timer))
            {
                _queueTimers[attacker] = timer + dt;
            }
            else
            {
                _queueTimers[attacker] = dt;
            }
        }

        public float GetQueueTimer(Agent attacker)
        {
            if (attacker == null) return 0f;
            return _queueTimers.TryGetValue(attacker, out float timer) ? timer : 0f;
        }

        public void ResetQueueTimer(Agent attacker)
        {
            if (attacker == null) return;
            _queueTimers.Remove(attacker);
        }

        public bool TryRegisterAttacker(Agent attacker, Agent target)
        {
            if (attacker == null || target == null || !attacker.IsActive() || !target.IsActive())
                return false;

            if (IsBlacklisted(attacker, target, Mission.Current.CurrentTime))
                return false;

            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return false;

            int maxSlots = settings.MaxAttackSlots;
            int maxQueue = settings.MaxQueueSlots;

            // Check if they are already registered to this target
            bool isAlreadyQueued = _queuedAttackers.TryGetValue(target, out var targetQueue) && targetQueue.Contains(attacker);
            bool isAlreadyActive = _activeEngagements.TryGetValue(target, out var targetActive) && targetActive.Contains(attacker);

            if (isAlreadyQueued || isAlreadyActive)
            {
                // Ensure map is synced
                _attackerCurrentTarget[attacker] = target;
                return true;
            }

            // Target is full check
            int currentActiveCount = targetActive?.Count ?? 0;
            int currentQueueCount = targetQueue?.Count ?? 0;

            if (currentActiveCount >= maxSlots && currentQueueCount >= maxQueue)
            {
                return false; // Overflow! Cannot accept this attacker
            }

            // If attacker is targeting someone else currently, remove them from the old target
            if (_attackerCurrentTarget.TryGetValue(attacker, out Agent oldTarget))
            {
                if (oldTarget != target)
                {
                    RemoveAttackerFromTargetLists(attacker, oldTarget);
                }
            }

            _attackerCurrentTarget[attacker] = target;

            if (targetQueue == null)
            {
                targetQueue = new List<Agent>(8);
                _queuedAttackers[target] = targetQueue;
            }

            targetQueue.Add(attacker);
            return true;
        }

        private void RemoveAttackerFromTargetLists(Agent attacker, Agent target)
        {
            if (_activeEngagements.TryGetValue(target, out var activeList))
            {
                activeList.Remove(attacker);
            }
            if (_queuedAttackers.TryGetValue(target, out var queueList))
            {
                queueList.Remove(attacker);
            }
        }

        public void RemoveAttacker(Agent attacker)
        {
            if (_attackerCurrentTarget.TryGetValue(attacker, out Agent target))
            {
                RemoveAttackerFromTargetLists(attacker, target);
                _attackerCurrentTarget.Remove(attacker);
            }
        }

        public void DemoteToQueue(Agent attacker, Agent target, float currentTime)
        {
            if (attacker == null || target == null) return;

            var settings = DynamicCombatSettings.Instance;
            int maxQueue = settings?.MaxQueueSlots ?? 3;

            if (_activeEngagements.TryGetValue(target, out var activeList))
            {
                activeList.Remove(attacker);
            }

            if (!_queuedAttackers.TryGetValue(target, out var targetQueue))
            {
                targetQueue = new List<Agent>(8);
                _queuedAttackers[target] = targetQueue;
            }

            if (!targetQueue.Contains(attacker))
            {
                if (targetQueue.Count >= maxQueue)
                {
                    // Target queue is full. Evict entirely. Blacklist for 2 seconds and find new target.
                    BlacklistTarget(attacker, target, currentTime, 2.0f);
                    RemoveAttacker(attacker);
                }
                else
                {
                    targetQueue.Add(attacker);
                    ResetQueueTimer(attacker); // Start queue timer
                }
            }
        }

        public void PrintTelemetry()
        {
            int totalActive = 0;
            int totalQueued = 0;
            foreach (var kvp in _activeEngagements) totalActive += kvp.Value.Count;
            foreach (var kvp in _queuedAttackers) totalQueued += kvp.Value.Count;

            TaleWorlds.Library.Debug.Print($"[DynamicCombat] Active Slots: {totalActive} | Queued: {totalQueued}");
        }

        public void UpdateSlots()
        {
            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return;

            int maxSlots = settings.MaxAttackSlots;
            int maxQueue = settings.MaxQueueSlots;
            float rearAngle = settings.RearAngle;
            float currentTime = Mission.Current.CurrentTime;

            // Pre-allocate list to avoid garbage collection hit every 250ms
            List<Agent> targetsToProcess = new List<Agent>(_activeEngagements.Keys);
            foreach(var t in _queuedAttackers.Keys)
            {
                if (!targetsToProcess.Contains(t))
                    targetsToProcess.Add(t);
            }

            // Iterate backwards to allow removal
            for (int i = targetsToProcess.Count - 1; i >= 0; i--)
            {
                Agent target = targetsToProcess[i];
                if (target == null || !target.IsActive())
                {
                    _activeEngagements.Remove(target);
                    _queuedAttackers.Remove(target);
                    continue; // Skip processing dead targets
                }

                if (!_activeEngagements.TryGetValue(target, out var activeList))
                {
                    activeList = new List<Agent>(maxSlots);
                    _activeEngagements[target] = activeList;
                }

                if (!_queuedAttackers.TryGetValue(target, out var queueList))
                {
                    queueList = new List<Agent>(8);
                    _queuedAttackers[target] = queueList;
                }

                // Cleanup inactive attackers from this target's lists
                for (int j = activeList.Count - 1; j >= 0; j--)
                {
                    if (activeList[j] == null || !activeList[j].IsActive())
                    {
                        _attackerCurrentTarget.Remove(activeList[j]);
                        activeList.RemoveAt(j);
                    }
                }
                for (int j = queueList.Count - 1; j >= 0; j--)
                {
                    if (queueList[j] == null || !queueList[j].IsActive())
                    {
                        _attackerCurrentTarget.Remove(queueList[j]);
                        queueList.RemoveAt(j);
                    }
                }

                // Try to promote queued attackers to active slots
                while (activeList.Count < maxSlots && queueList.Count > 0)
                {
                    // Find closest eligible attacker
                    Agent bestCandidate = null;
                    float closestDistSq = float.MaxValue;
                    int candidateIndex = -1;

                    for (int j = 0; j < queueList.Count; j++)
                    {
                        var candidate = queueList[j];

                        // Check if candidate is behind the target. If so, they are not eligible for promotion.
                        if (IsInRearQuadrant(candidate, target, rearAngle))
                        {
                            continue;
                        }

                        float distSq = candidate.Position.DistanceSquared(target.Position);
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            bestCandidate = candidate;
                            candidateIndex = j;
                        }
                    }

                    if (bestCandidate != null)
                    {
                        queueList.RemoveAt(candidateIndex);
                        activeList.Add(bestCandidate);
                    }
                    else
                    {
                        // No eligible candidates found (e.g. all in rear quadrant)
                        break;
                    }
                }

                // Active Over-Capacity Audit: Demote if over max slots
                // Specifically evicting the furthest (or last-added, we use furthest as a proxy for least committed)
                while (activeList.Count > maxSlots)
                {
                    Agent furthestCandidate = null;
                    float furthestDistSq = -1f;
                    int candidateIndex = -1;

                    for (int j = 0; j < activeList.Count; j++)
                    {
                        float distSq = activeList[j].Position.DistanceSquared(target.Position);
                        if (distSq > furthestDistSq)
                        {
                            furthestDistSq = distSq;
                            furthestCandidate = activeList[j];
                            candidateIndex = j;
                        }
                    }

                    if (furthestCandidate != null)
                    {
                        // Safely demote, which applies the max queue logic and blacklists if full
                        DemoteToQueue(furthestCandidate, target, currentTime);
                        // Have to adjust loop because activeList was modified directly
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        public bool HasActiveSlot(Agent attacker, Agent target)
        {
            if (attacker == null || target == null) return false;

            if (_activeEngagements.TryGetValue(target, out var activeList))
            {
                return activeList.Contains(attacker);
            }
            return false;
        }

        public bool IsTargetSwarmed(Agent target)
        {
            if (target == null) return false;

            int activeCount = _activeEngagements.TryGetValue(target, out var activeList) ? activeList.Count : 0;
            int queueCount = _queuedAttackers.TryGetValue(target, out var queueList) ? queueList.Count : 0;

            return (activeCount + queueCount) > 10;
        }

        // Finds the closest valid enemy target that has open slots.
        // If requireActiveSlot is true, it strictly prioritizes targets with open ACTIVE attack slots (Hunter Instinct).
        public Agent FindAlternativeTarget(Agent attacker, bool requireActiveSlot = false)
        {
            if (attacker == null || !attacker.IsActive() || Mission.Current == null) return null;

            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return null;

            int maxSlots = settings.MaxAttackSlots;
            int maxQueue = settings.MaxQueueSlots;
            float currentTime = Mission.Current.CurrentTime;

            Agent bestTarget = null;
            float closestDistSq = float.MaxValue;

            var agents = Mission.Current.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var potentialTarget = agents[i];
                if (!potentialTarget.IsActive() || !potentialTarget.IsHuman || potentialTarget.Team == null) continue;
                if (!attacker.Team.IsEnemyOf(potentialTarget.Team)) continue;

                // Avoid blacklisted targets
                if (IsBlacklisted(attacker, potentialTarget, currentTime)) continue;

                int activeCount = _activeEngagements.TryGetValue(potentialTarget, out var aList) ? aList.Count : 0;
                int queueCount = _queuedAttackers.TryGetValue(potentialTarget, out var qList) ? qList.Count : 0;

                if (requireActiveSlot)
                {
                    if (activeCount >= maxSlots) continue;
                }
                else
                {
                    if (activeCount >= maxSlots && queueCount >= maxQueue) continue; // Target is fully saturated
                }

                float distSq = attacker.Position.DistanceSquared(potentialTarget.Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    bestTarget = potentialTarget;
                }
            }

            // Fallback: If we demanded an active slot but found none, just look for ANY open queue.
            if (bestTarget == null && requireActiveSlot)
            {
                return FindAlternativeTarget(attacker, false);
            }

            return bestTarget;
        }

        public void RemoveActiveSlot(Agent attacker, Agent target)
        {
             if (attacker == null || target == null) return;
             if (_activeEngagements.TryGetValue(target, out var activeList) && activeList.Contains(attacker))
             {
                 DemoteToQueue(attacker, target, Mission.Current.CurrentTime);
             }
        }

        // Shared utility to determine if attacker is in rear quadrant
        public static bool IsInRearQuadrant(Agent attacker, Agent target, float rearAngleDegrees)
        {
            Vec2 targetLookDirection = target.LookDirection.AsVec2;
            if (targetLookDirection.LengthSquared < 0.0001f)
                targetLookDirection = new Vec2(0, 1);
            else
                targetLookDirection = targetLookDirection.Normalized();

            Vec2 diff = attacker.Position.AsVec2 - target.Position.AsVec2;

            if (diff.LengthSquared < 0.0001f)
                return false;

            Vec2 toAttackerDirection = diff.Normalized();

            float dotProduct = Vec2.DotProduct(targetLookDirection, toAttackerDirection);

            float angleRadians = (float)Math.Acos(MBMath.ClampFloat(dotProduct, -1f, 1f));
            float angleDegrees = angleRadians * (180f / (float)Math.PI);

            float threshold = 180f - (rearAngleDegrees / 2f);

            return angleDegrees >= threshold;
        }
    }
}