using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;

namespace DynamicCombat
{
    public class DynamicCombatBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private float _tickTimer = 0f;
        private const float UpdateInterval = 0.25f; // Update every 250ms to save performance

        private static readonly ActionIndexCache DefendActionCache = ActionIndexCache.Create("act_defend_shield_up_forward");
        private static readonly ActionIndexCache CheerActionCache = ActionIndexCache.Create("act_arena_cheer_1");

        // Tracks agents currently forced into a defensive animation to prevent FMOD frame-spam leaks
        private System.Collections.Generic.Dictionary<Agent, bool> _isDefending = new System.Collections.Generic.Dictionary<Agent, bool>();
        private System.Collections.Generic.Dictionary<Agent, bool> _isCheering = new System.Collections.Generic.Dictionary<Agent, bool>();

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            _tickTimer += dt;
            if (_tickTimer >= UpdateInterval)
            {
                float passedTime = _tickTimer;
                _tickTimer = 0f;

                // Keep slot allocation, telemetry logging, and state calculation securely on the 250ms interval tick.
                CombatRegistry.Instance.UpdateSlots();
                CombatRegistry.Instance.PrintTelemetry();
                UpdateSlotAllocations(passedTime);
            }

            // Continuous suppression commands moved back to per-frame loop to actively suppress vanilla AI.
            // C# dictionary guards prevent spamming unmanaged FMOD action requests.
            EnforceSuppression(dt);
        }

        private void UpdateSlotAllocations(float dt)
        {
            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return;

            float minDistance = settings.MinDistance;
            float rearAngle = settings.RearAngle;

            var agents = Mission.Current.Agents;
            int count = agents.Count;

            // Iterate using standard loop to avoid LINQ allocation
            for (int i = 0; i < count; i++)
            {
                Agent attacker = agents[i];

                if (!attacker.IsActive() || !attacker.IsHuman)
                    continue;

                if (IsRanged(attacker))
                {
                    CombatRegistry.Instance.RemoveAttacker(attacker);
                    continue;
                }

                Agent target = attacker.GetTargetAgent();

                if (target == null || !target.IsActive() || !target.IsHuman)
                {
                    CombatRegistry.Instance.RemoveAttacker(attacker);
                    continue;
                }

                // If completely free of an attack slot or queue, trigger Hunter Instinct Preference
                if (!CombatRegistry.Instance.TryRegisterAttacker(attacker, target))
                {
                    // Registration failed because queue is full or target is blacklisted.
                    Agent newTarget = CombatRegistry.Instance.FindAlternativeTarget(attacker, true); // true = require active slot
                    if (newTarget != null && newTarget.IsActive())
                    {
                        attacker.SetTargetAgent(newTarget);
                        CombatRegistry.Instance.TryRegisterAttacker(attacker, newTarget);
                        target = newTarget; // Update local ref for this tick
                    }
                    else
                    {
                        CombatRegistry.Instance.RemoveAttacker(attacker);
                    }
                }

                bool hasActiveSlot = CombatRegistry.Instance.HasActiveSlot(attacker, target);
                float distanceToTarget = attacker.Position.Distance(target.Position);

                // Rear-Quadrant Truncation
                bool isBehindTarget = CombatRegistry.IsInRearQuadrant(attacker, target, rearAngle);

                if (isBehindTarget && hasActiveSlot)
                {
                    CombatRegistry.Instance.RemoveActiveSlot(attacker, target);
                    hasActiveSlot = false;
                }

                // Active Attacker Priority vs. Spatial Eviction
                if (hasActiveSlot)
                {
                    if (distanceToTarget > minDistance + 2.0f) // If they drift too far outside striking circle
                    {
                        // Spatial Eviction & Demotion
                        CombatRegistry.Instance.RemoveActiveSlot(attacker, target);
                        hasActiveSlot = false;
                    }
                    else
                    {
                        // Active Attacker Priority: Immune to interrupts, lock absolute
                        CombatRegistry.Instance.ResetQueueTimer(attacker);
                    }
                }

                // Queue Flank Interrupt & Blacklist
                if (!hasActiveSlot && target != null)
                {
                    bool tookDamage = CombatRegistry.Instance.DidTakeDamage(attacker);
                    CombatRegistry.Instance.IncrementQueueTimer(attacker, dt);
                    float queueTime = CombatRegistry.Instance.GetQueueTimer(attacker);

                    if (tookDamage || queueTime > 2.0f)
                    {
                        // Immediately cancel timer
                        CombatRegistry.Instance.ResetQueueTimer(attacker);
                        // Break current lock & place on blacklist for 2 seconds
                        CombatRegistry.Instance.BlacklistTarget(attacker, target, Mission.Current.CurrentTime, 2.0f);
                        CombatRegistry.Instance.RemoveAttacker(attacker);

                        // Scan for alternative
                        Agent altTarget = CombatRegistry.Instance.FindAlternativeTarget(attacker, true); // true = require active slot
                        if (altTarget != null)
                        {
                            ClearCheerState(attacker);
                            attacker.SetTargetAgent(altTarget);
                            CombatRegistry.Instance.TryRegisterAttacker(attacker, altTarget);
                            target = altTarget;
                        }
                        else
                        {
                            // Try finding a target with an open queue slot as a fallback
                            Agent altQueueTarget = CombatRegistry.Instance.FindAlternativeTarget(attacker, false);
                            if (altQueueTarget != null)
                            {
                                ClearCheerState(attacker);
                                attacker.SetTargetAgent(altQueueTarget);
                                CombatRegistry.Instance.TryRegisterAttacker(attacker, altQueueTarget);
                                target = altQueueTarget;
                            }
                            else
                            {
                                // No targets available at all, global slots full. Overflow to Cheer.
                                EnterCheerState(attacker);
                            }
                        }
                    }
                }
            }
        }

        private void EnforceSuppression(float dt)
        {
            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return;

            float minDistance = settings.MinDistance;
            float maxDistance = settings.MaxDistance;
            float rearAngle = settings.RearAngle;

            var agents = Mission.Current.Agents;
            int count = agents.Count;

            for (int i = 0; i < count; i++)
            {
                Agent attacker = agents[i];

                if (!attacker.IsActive() || !attacker.IsHuman)
                    continue;

                if (IsRanged(attacker))
                    continue;

                Agent target = attacker.GetTargetAgent();

                if (target == null || !target.IsActive() || !target.IsHuman)
                    continue;

                bool isCheering = _isCheering.TryGetValue(attacker, out bool cheeringState) && cheeringState;

                // Handle Cheer Interruption from Damage
                if (isCheering)
                {
                    if (CombatRegistry.Instance.DidTakeDamage(attacker))
                    {
                        ClearCheerState(attacker);
                        isCheering = false;
                    }
                    else
                    {
                        // Maintain cheer state, hold ground, and skip standard suppression logic
                        attacker.SetLookAgent(null);
                        attacker.DisableScriptedMovement();
                        attacker.SetMaximumSpeedLimit(0f, false);
                        continue;
                    }
                }

                bool hasActiveSlot = CombatRegistry.Instance.HasActiveSlot(attacker, target);
                bool isTargetSwarmed = CombatRegistry.Instance.IsTargetSwarmed(target);
                bool isBehindTarget = CombatRegistry.IsInRearQuadrant(attacker, target, rearAngle);

                float distanceToTarget = attacker.Position.Distance(target.Position);

                // Handle Cavalry
                if (attacker.HasMount)
                {
                    if (isTargetSwarmed)
                    {
                        // Cavalry orbit behavior
                        float orbitDistance = maxDistance + 1.0f; // 1 meter outside max distance
                        Vec2 targetPos = target.Position.AsVec2;

                        float angle = (Mission.Current.CurrentTime * 0.5f) % (2 * (float)Math.PI);
                        Vec2 orbitPos = targetPos + new Vec2((float)Math.Cos(angle) * orbitDistance, (float)Math.Sin(angle) * orbitDistance);

                        WorldPosition orbitWorldPos = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, new Vec3(orbitPos.x, orbitPos.y, attacker.Position.z), false);
                        attacker.SetScriptedPosition(ref orbitWorldPos, false, Agent.AIScriptedFrameFlags.None);
                        continue;
                    }
                    else
                    {
                        attacker.DisableScriptedMovement();
                    }
                }

                if (!hasActiveSlot || isBehindTarget)
                {
                    // Passive Containment Override
                    // If the attacker is outside the maximum distance, do not micromanage them.
                    if (distanceToTarget > maxDistance)
                    {
                        attacker.DisableScriptedMovement();
                        attacker.SetMaximumSpeedLimit(-1f, false);
                    }
                    else
                    {
                        // Force agent to face the enemy when inside max radius
                        attacker.SetLookAgent(target);

                        // 1. Spatial Tolerance Zone & 2. The Queue Spacer
                        Vec2 attackerPos2D = attacker.Position.AsVec2;
                        Vec2 targetPos2D = target.Position.AsVec2;

                        Vec2 pushForce = new Vec2(0, 0);

                        // Calculate repulsion from target (maintain min distance)
                        if (distanceToTarget < minDistance)
                        {
                            Vec2 diff = attackerPos2D - targetPos2D;
                            Vec2 dirAway = diff.LengthSquared < 0.0001f ? new Vec2(1, 0) : diff.Normalized();
                            // Strong push away from target if inside minimum ring
                            pushForce += dirAway * (minDistance - distanceToTarget);
                        }

                        // Calculate repulsion from other queued peers (Queue Spacer)
                        var queuedPeers = CombatRegistry.Instance.GetQueuedAttackers(target);
                        if (queuedPeers != null)
                        {
                            for (int j = 0; j < queuedPeers.Count; j++)
                            {
                                Agent peer = queuedPeers[j];
                                if (peer == attacker || !peer.IsActive()) continue;

                                float distToPeer = attacker.Position.Distance(peer.Position);
                                if (distToPeer < 2.0f) // 2 meters spacer
                                {
                                    Vec2 peerDiff = attackerPos2D - peer.Position.AsVec2;
                                    Vec2 peerDirAway = peerDiff.LengthSquared < 0.0001f ? new Vec2(1, 0) : peerDiff.Normalized();

                                    // Scale push force based on how close they are
                                    pushForce += peerDirAway * (2.0f - distToPeer);
                                }
                            }
                        }

                        // Apply spatial forces or hold ground
                        if (pushForce.LengthSquared > 0.01f) // Needs to move to maintain spacing/min distance
                        {
                            Vec2 idealPos = attackerPos2D + pushForce;
                            WorldPosition adjustmentPos = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, new Vec3(idealPos.x, idealPos.y, attacker.Position.z), false);
                            attacker.SetScriptedPosition(ref adjustmentPos, false, Agent.AIScriptedFrameFlags.None);
                            attacker.SetMaximumSpeedLimit(-1f, false); // Allow movement to the new spot
                        }
                        else
                        {
                            // Inside the safe fluid zone and properly spaced, hold ground.
                            attacker.DisableScriptedMovement();
                            attacker.SetMaximumSpeedLimit(0f, false);
                        }

                        // Force the agent to block/defend while waiting in the queue
                        bool currentlyDefending = _isDefending.TryGetValue(attacker, out bool def) && def;

                        if (!currentlyDefending)
                        {
                            attacker.SetActionChannel(1, DefendActionCache, false, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
                            attacker.EnforceShieldUsage(Agent.UsageDirection.DefendDown);
                            _isDefending[attacker] = true;
                        }
                        else
                        {
                            // Need to continually enforce usage even if action is set
                            attacker.EnforceShieldUsage(Agent.UsageDirection.DefendDown);
                        }
                    }
                }
                else
                {
                    // Has active slot, allow normal combat behavior
                    attacker.DisableScriptedMovement();
                    attacker.SetMaximumSpeedLimit(-1f, false);
                    attacker.EnforceShieldUsage(Agent.UsageDirection.None);

                    if (_isDefending.TryGetValue(attacker, out bool def) && def)
                    {
                        // Safely unflag so we don't spam resets if they are already free
                        _isDefending[attacker] = false;
                    }
                }
            }
        }

        private bool IsRanged(Agent agent)
        {
            var equipment = agent.Equipment;
            if (equipment == null) return false;

            var wieldedWeapon = agent.WieldedWeapon;
            if (wieldedWeapon.IsEmpty) return false;

            var item = wieldedWeapon.Item;
            if (item == null) return false;

            return item.ItemType == ItemObject.ItemTypeEnum.Bow ||
                   item.ItemType == ItemObject.ItemTypeEnum.Crossbow ||
                   item.ItemType == ItemObject.ItemTypeEnum.Thrown;
        }

        private void EnterCheerState(Agent agent)
        {
            if (!_isCheering.TryGetValue(agent, out bool cheering) || !cheering)
            {
                agent.SetActionChannel(1, CheerActionCache, false, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
                _isCheering[agent] = true;
            }
        }

        private void ClearCheerState(Agent agent)
        {
            if (_isCheering.TryGetValue(agent, out bool cheering) && cheering)
            {
                _isCheering[agent] = false;
                // Force an action clear to snap them out of the cheer quickly
                agent.SetActionChannel(1, ActionIndexCache.act_none, true, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
            }
        }

        public override void OnAgentDeleted(Agent agent)
        {
            base.OnAgentDeleted(agent);
            CombatRegistry.Instance.RemoveAttacker(agent);
            _isDefending.Remove(agent);
            _isCheering.Remove(agent);
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            CombatRegistry.Instance.Clear();
            _isDefending.Clear();
            _isCheering.Clear();
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            CombatRegistry.Instance.Clear();
            _isDefending.Clear();
            _isCheering.Clear();
        }
    }
}