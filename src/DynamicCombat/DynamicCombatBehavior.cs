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

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            _tickTimer += dt;
            if (_tickTimer >= UpdateInterval)
            {
                float passedTime = _tickTimer;
                _tickTimer = 0f;
                CombatRegistry.Instance.UpdateSlots();
                UpdateAgents(passedTime);
            }
        }

        private void UpdateAgents(float dt)
        {
            var settings = DynamicCombatSettings.Instance;
            if (settings == null) return;

            float minDistance = settings.MinDistance;
            float maxDistance = settings.MaxDistance;
            float rearAngle = settings.RearAngle;

            var agents = Mission.Current.Agents;
            int count = agents.Count;

            // Iterate using standard loop to avoid LINQ allocation
            for (int i = 0; i < count; i++)
            {
                Agent attacker = agents[i];

                if (!attacker.IsActive() || !attacker.IsHuman)
                    continue;

                // We only care about melee fighters. If they are holding a ranged weapon and using it as ranged, ignore.
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
                // This means looking for active slots before settling for a queue
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
                bool isTargetSwarmed = CombatRegistry.Instance.IsTargetSwarmed(target);

                float distanceToTarget = attacker.Position.Distance(target.Position);

                // Rear-Quadrant Truncation
                bool isBehindTarget = CombatRegistry.IsInRearQuadrant(attacker, target, rearAngle);

                if (isBehindTarget && hasActiveSlot)
                {
                    // Strip authorization
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

                    if (tookDamage || queueTime > 5.0f)
                    {
                        // Immediately cancel timer
                        CombatRegistry.Instance.ResetQueueTimer(attacker);
                        // Break current lock & place on blacklist for 2 seconds
                        CombatRegistry.Instance.BlacklistTarget(attacker, target, Mission.Current.CurrentTime, 2.0f);
                        CombatRegistry.Instance.RemoveAttacker(attacker);

                        // Scan for alternative
                        Agent altTarget = CombatRegistry.Instance.FindAlternativeTarget(attacker, true);
                        if (altTarget != null)
                        {
                            attacker.SetTargetAgent(altTarget);
                            CombatRegistry.Instance.TryRegisterAttacker(attacker, altTarget);
                            target = altTarget;
                        }
                    }
                }

                // Handle Cavalry
                if (attacker.HasMount)
                {
                    if (isTargetSwarmed)
                    {
                        // Cavalry orbit behavior
                        float orbitDistance = maxDistance + 1.0f; // 1 meter outside max distance
                        Vec2 targetPos = target.Position.AsVec2;

                        // Pick a position to orbit. We can approximate an orbit by picking a point offset by the orbit distance
                        // and rotating it based on time.
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
                    // This allows standard formation AI and pathfinding to work naturally
                    // until they approach the engagement zone.
                    if (distanceToTarget > maxDistance)
                    {
                        attacker.DisableScriptedMovement();
                        attacker.SetMaximumSpeedLimit(-1f, false);
                    }
                    else
                    {
                        // Force agent to face the enemy when inside max radius
                        attacker.SetLookAgent(target);

                        // Maintain elastic boundary inside the max radius
                        if (distanceToTarget < minDistance)
                        {
                            // Step back
                            Vec2 diff = attacker.Position.AsVec2 - target.Position.AsVec2;
                            Vec2 dirAway = diff.LengthSquared < 0.0001f ? new Vec2(1, 0) : diff.Normalized();
                            Vec2 idealPos = target.Position.AsVec2 + (dirAway * minDistance);
                            WorldPosition retreatPos = new WorldPosition(Mission.Current.Scene, UIntPtr.Zero, new Vec3(idealPos.x, idealPos.y, attacker.Position.z), false);
                            attacker.SetScriptedPosition(ref retreatPos, false, Agent.AIScriptedFrameFlags.None);
                        }
                        else
                        {
                            // Inside the dead-zone, zero-movement. Let them stop.
                            attacker.DisableScriptedMovement();
                            attacker.SetMaximumSpeedLimit(0f, false); // Try to force them to stop walking towards the target
                        }

                        // Force the agent to block/defend while waiting in the queue
                        // We use ActionIndexCache.act_defend_shield_up_forward to trigger the defense state if they have a shield
                        if (HasShield(attacker))
                        {
                            attacker.SetActionChannel(1, ActionIndexCache.Create("act_defend_shield_up_forward"), false, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
                            attacker.EnforceShieldUsage(Agent.UsageDirection.DefendDown);
                        }
                        else
                        {
                            // Without a shield, simply suppress their attack
                            attacker.SetActionChannel(1, ActionIndexCache.act_none, false, 0, 0, 0, 0, 0, 0, false, 0, 0, true);
                        }
                    }
                }
                else
                {
                    // Has active slot, allow normal combat behavior
                    attacker.DisableScriptedMovement();
                    attacker.SetMaximumSpeedLimit(-1f, false);
                    attacker.EnforceShieldUsage(Agent.UsageDirection.None);
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

        private bool HasShield(Agent agent)
        {
            var equipment = agent.Equipment;
            if (equipment == null) return false;

            var wieldedOffhand = agent.WieldedOffhandWeapon;
            if (!wieldedOffhand.IsEmpty && wieldedOffhand.Item != null && wieldedOffhand.Item.ItemType == ItemObject.ItemTypeEnum.Shield)
            {
                return true;
            }

            return false;
        }

        public override void OnAgentDeleted(Agent agent)
        {
            base.OnAgentDeleted(agent);
            CombatRegistry.Instance.RemoveAttacker(agent);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            CombatRegistry.Instance.Clear();
        }
    }
}