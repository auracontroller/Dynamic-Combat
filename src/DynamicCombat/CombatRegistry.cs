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

        public void Clear()
        {
            _activeEngagements.Clear();
            _queuedAttackers.Clear();
            _attackerCurrentTarget.Clear();
        }

        public void RegisterAttacker(Agent attacker, Agent target)
        {
            if (attacker == null || target == null || !attacker.IsActive() || !target.IsActive())
                return;

            // If attacker is targeting someone else currently, remove them from the old target
            if (_attackerCurrentTarget.TryGetValue(attacker, out Agent oldTarget))
            {
                if (oldTarget != target)
                {
                    RemoveAttackerFromTargetLists(attacker, oldTarget);
                }
                else
                {
                    // Already targeting this target. Ensure they are in the queue or active list.
                    if (HasActiveSlot(attacker, target) || (_queuedAttackers.TryGetValue(target, out var qList) && qList.Contains(attacker)))
                    {
                        return; // Already registered to this target
                    }
                }
            }

            _attackerCurrentTarget[attacker] = target;

            if (!_queuedAttackers.TryGetValue(target, out var queueList))
            {
                queueList = new List<Agent>(8);
                _queuedAttackers[target] = queueList;
            }

            if (!queueList.Contains(attacker))
            {
                if (!_activeEngagements.TryGetValue(target, out var activeList) || !activeList.Contains(attacker))
                {
                    queueList.Add(attacker);
                }
            }
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

        public void UpdateSlots()
        {
            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return;

            int maxSlots = settings.MaxAttackSlots;
            float rearAngle = settings.RearAngle;

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

                // Demote if over max slots
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
                        activeList.RemoveAt(candidateIndex);
                        queueList.Add(furthestCandidate);
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

        public void RemoveActiveSlot(Agent attacker, Agent target)
        {
             if (attacker == null || target == null) return;
             if (_activeEngagements.TryGetValue(target, out var activeList) && activeList.Contains(attacker))
             {
                 activeList.Remove(attacker);
                 // Move back to queue
                 if (!_queuedAttackers.TryGetValue(target, out var queueList))
                 {
                    queueList = new List<Agent>(8);
                    _queuedAttackers[target] = queueList;
                 }
                 if (!queueList.Contains(attacker))
                    queueList.Add(attacker);
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