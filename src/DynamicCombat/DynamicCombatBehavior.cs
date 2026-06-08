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

        private static readonly ActionIndexCache CheerActionCache = ActionIndexCache.Create("act_command_view_cheer");

        // Tracks agents currently forced into a defensive animation to prevent FMOD frame-spam leaks
        private System.Collections.Generic.Dictionary<Agent, bool> _isCheering = new System.Collections.Generic.Dictionary<Agent, bool>();

        // Tracks the last target we logged a forced override for to prevent log spam
        private System.Collections.Generic.Dictionary<Agent, Agent> _lastLoggedOverride = new System.Collections.Generic.Dictionary<Agent, Agent>();

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
                    // Instead of continuing (which skips overflow logic entirely), let it fall through to TryRegisterAttacker which will fail,
                    // and then the fallback logic will attempt to find a target. If it can't find a target, it will hit global overflow.
                }

                // If completely free of an attack slot or queue, trigger Hunter Instinct Preference
                if (target == null || !CombatRegistry.Instance.TryRegisterAttacker(attacker, target))
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
                        // Fallback to queue search
                        Agent queueTarget = CombatRegistry.Instance.FindAlternativeTarget(attacker, false);
                        if (queueTarget != null && queueTarget.IsActive())
                        {
                            attacker.SetTargetAgent(queueTarget);
                            CombatRegistry.Instance.TryRegisterAttacker(attacker, queueTarget);
                            target = queueTarget;
                        }
                        else
                        {
                            CombatRegistry.Instance.RemoveAttacker(attacker);
                            // Set to null to explicitly trigger global overflow handling in EnforceSuppression
                            target = null;
                        }
                    }
                }

                bool hasActiveSlot = CombatRegistry.Instance.HasActiveSlot(attacker, target);
                float distanceToTargetSq = attacker.Position.DistanceSquared(target.Position);

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
                    float evictionThreshold = minDistance + 2.0f;
                    if (distanceToTargetSq > evictionThreshold * evictionThreshold) // If they drift too far outside striking circle
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

                bool isCheering = _isCheering.TryGetValue(attacker, out bool cheeringState) && cheeringState;

                if (target == null || !target.IsActive() || !target.IsHuman)
                {
                    // If they have no valid target whatsoever (global overflow), halt and cheer
                    attacker.SetTargetAgent(null);
                    attacker.DisableScriptedMovement();
                    attacker.SetMaximumSpeedLimit(0f, false);
                    EnterCheerState(attacker);
                    continue;
                }

                // Handle Cheer Interruption from Damage
                if (isCheering)
                {
                    if (CombatRegistry.Instance.DidTakeDamage(attacker))
                    {
                        ClearCheerState(attacker);
                        isCheering = false;
                    }
                }

                isCheering = _isCheering.TryGetValue(attacker, out bool currentCheeringState) && currentCheeringState;

                // Handle Cheer Interruption from Damage
                if (isCheering)
                {
                    if (CombatRegistry.Instance.DidTakeDamage(attacker))
                    {
                        ClearCheerState(attacker);
                        isCheering = false;
                    }
                }

                bool hasActiveSlot = CombatRegistry.Instance.HasActiveSlot(attacker, target);
                bool isTargetSwarmed = CombatRegistry.Instance.IsTargetSwarmed(target);
                bool isBehindTarget = CombatRegistry.IsInRearQuadrant(attacker, target, rearAngle);

                float distanceToTargetSq = attacker.Position.DistanceSquared(target.Position);

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

                // Strict Enforce Mod Target against Native AI rubber-banding
                Agent assignedTarget = CombatRegistry.Instance.GetCurrentTarget(attacker);
                if (assignedTarget != null && assignedTarget.IsActive() && target != assignedTarget)
                {
                    attacker.SetTargetAgent(assignedTarget);
                    // Only log if we haven't already logged an override for this exact assignment
                    if (!_lastLoggedOverride.TryGetValue(attacker, out Agent lastOverride) || lastOverride != assignedTarget)
                    {
                        ModLogger.Log($"Agent {attacker.Index} rubber-banding fixed: forced target override to {assignedTarget.Index}.");
                        _lastLoggedOverride[attacker] = assignedTarget;
                    }

                    target = assignedTarget; // Force the override
                }
                else if (assignedTarget != null && assignedTarget.IsActive() && target == assignedTarget)
                {
                    // If native AI target aligns with our assigned target, we can clear the logged state
                    // so if it rubber-bands again, we log it.
                    if (_lastLoggedOverride.ContainsKey(attacker))
                    {
                        _lastLoggedOverride.Remove(attacker);
                    }
                }

                if (!hasActiveSlot || isBehindTarget)
                {
                    // Passive Containment Override
                    // If the attacker is outside the maximum distance, do not micromanage them.
                    if (distanceToTargetSq > maxDistance * maxDistance)
                    {
                        attacker.DisableScriptedMovement();
                        attacker.SetMaximumSpeedLimit(-1f, false);
                        ClearCheerState(attacker);
                    }
                    else
                    {
                        // Cascade 1: Calculate the Spatial Delta
                        Vec3 vectorToTarget = target.Position - attacker.Position;
                        float currentDistance = vectorToTarget.Length;

                        // Cascade 2: Anchor the Body Orientation
                        // Truncate the vertical Z-axis noise to maintain a level gaze.
                        vectorToTarget.z = 0f;

                        if (vectorToTarget.LengthSquared > 0.0001f)
                        {
                            Vec3 lookDirection = vectorToTarget.NormalizedCopy();
                            // Force the Agent's collision capsule and skeletal head to face the player natively.
                            attacker.LookDirection = lookDirection;

                            Vec2 forward2D = lookDirection.AsVec2;
                            Vec2 right2D = new Vec2(forward2D.y, -forward2D.x);

                            // Cascade 3: The Exclusion Zone & Queue Spacer
                            Vec2 pushForce = Vec2.Zero;

                            // Calculate repulsion from other queued peers (Queue Spacer)
                            var queuedPeers = CombatRegistry.Instance.GetQueuedAttackers(target);
                            if (queuedPeers != null)
                            {
                                Vec2 attackerPos2D = attacker.Position.AsVec2;
                                for (int j = 0; j < queuedPeers.Count; j++)
                                {
                                    Agent peer = queuedPeers[j];
                                    if (peer == attacker || !peer.IsActive()) continue;

                                    float distToPeerSq = attacker.Position.DistanceSquared(peer.Position);
                                    if (distToPeerSq < 4.0f) // 2 meters spacer squared
                                    {
                                        float distToPeer = (float)Math.Sqrt(distToPeerSq);
                                        Vec2 peerDiff = attackerPos2D - peer.Position.AsVec2;
                                        Vec2 peerDirAway = peerDiff.LengthSquared < 0.0001f ? new Vec2(1, 0) : peerDiff.Normalized();
                                        // Scale push force based on how close they are
                                        pushForce += peerDirAway * (2.0f - distToPeer);
                                    }
                                }
                            }

                            Vec2 movementInput = Vec2.Zero;

                            if (currentDistance < minDistance)
                            {
                                // Bypass the pathfinder by hijacking the Agent's simulated movement input.
                                // X = 0 (no strafing), Y = -1 (walk straight backward relative to LookDirection).
                                // This triggers native ushiro (backward) walking animations fluidly.
                                movementInput.y -= 1f;
                            }

                            // Integrate peer repulsion into MovementInputVector
                            if (pushForce.LengthSquared > 0.0001f)
                            {
                                float localY = Vec2.DotProduct(pushForce, forward2D);
                                float localX = Vec2.DotProduct(pushForce, right2D);
                                movementInput.x += localX;
                                movementInput.y += localY;
                            }

                            float inputMagSq = movementInput.LengthSquared;
                            // Hysteresis: Require a stronger push to break an active cheer
                            float breakCheerThreshold = isCheering ? 0.05f : 0.005f;

                            if (inputMagSq > breakCheerThreshold)
                            {
                                ClearCheerState(attacker); // Break cheer so they can walk
                                attacker.DisableScriptedMovement(); // Ensure scripted movement is off so we can hijack input
                                attacker.SetMaximumSpeedLimit(-1f, false);

                                // Cap movement input to length 1
                                if (inputMagSq > 1f)
                                {
                                    movementInput = movementInput.Normalized();
                                }
                                attacker.MovementInputVector = movementInput;
                            }
                            else
                            {
                                // Flush the movement input cache when outside the exclusion zone and properly spaced.
                                attacker.MovementInputVector = Vec2.Zero;
                                attacker.DisableScriptedMovement();
                                attacker.SetMaximumSpeedLimit(0f, false);
                                EnterCheerState(attacker); // Entering holding pattern, begin cheer
                            }
                        }
                    }
                }
                else
                {
                    // Has active slot, allow normal combat behavior
                    attacker.DisableScriptedMovement();
                    attacker.SetMaximumSpeedLimit(-1f, false);
                    ClearCheerState(attacker);
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
                if (agent.IsActive())
                {
                    // Play on channel 0 (full body) instead of channel 1 to guarantee it overrides stance
                    agent.SetActionChannel(0, CheerActionCache, false, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
                    agent.EnforceShieldUsage(Agent.UsageDirection.None); // Ensure shield doesn't block the animation
                }
                _isCheering[agent] = true;
                ModLogger.Log($"Agent {agent.Index} entering Cheer state.");
            }
        }

        private void ClearCheerState(Agent agent)
        {
            if (_isCheering.TryGetValue(agent, out bool cheering) && cheering)
            {
                _isCheering[agent] = false;
                if (agent.IsActive())
                {
                    // Force an action clear to snap them out of the cheer quickly
                    agent.SetActionChannel(0, ActionIndexCache.act_none, true, 0, 0, 1f, 0f, 0.5f, 0f, false, -0.2f, 0, true);
                }
                ModLogger.Log($"Agent {agent.Index} clearing Cheer state.");
            }
        }

        public override void OnAgentDeleted(Agent agent)
        {
            base.OnAgentDeleted(agent);
            CombatRegistry.Instance.PurgeAgent(agent);
            _isCheering.Remove(agent);
            _lastLoggedOverride.Remove(agent);

            // Clean up any references where this agent was the target in the log tracker
            var keysToRemove = new System.Collections.Generic.List<Agent>();
            foreach (var kvp in _lastLoggedOverride)
            {
                if (kvp.Value == agent)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                _lastLoggedOverride.Remove(key);
            }
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            CombatRegistry.Instance.Clear();
            _isCheering.Clear();
            _lastLoggedOverride.Clear();
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            CombatRegistry.Instance.Clear();
            _isCheering.Clear();
            _lastLoggedOverride.Clear();
        }
    }
}