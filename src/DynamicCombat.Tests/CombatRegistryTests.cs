using System;
using Xunit;
using DynamicCombat;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using System.Collections.Generic;

namespace DynamicCombat.Tests
{
    public class CombatRegistryTests
    {
        [Fact]
        public void IsInRearQuadrant_DirectlyBehind_ReturnsTrue()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0, -1, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.True(result);
        }

        [Fact]
        public void IsInRearQuadrant_DirectlyInFront_ReturnsFalse()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0, 1, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.False(result);
        }

        [Fact]
        public void IsInRearQuadrant_ExactlyOnBoundaryRight_ReturnsTrue()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0.7071f, -0.7071f, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.True(result);
        }

        [Fact]
        public void IsInRearQuadrant_SlightlyOutsideBoundaryRight_ReturnsFalse()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0.766f, -0.642f, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.False(result);
        }

        [Fact]
        public void IsInRearQuadrant_SamePosition_ReturnsFalse()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0, 0, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.False(result);
        }

        [Fact]
        public void IsInRearQuadrant_ZeroLookDirection_DefaultsToForwardAndReturnsTrue()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 0, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(0, -1, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 90f);
            Assert.True(result);
        }

        [Fact]
        public void IsInRearQuadrant_WithDifferentRearAngle180_ReturnsTrueOnSides()
        {
            var target = new Agent {
                Position = new Vec3(0, 0, 0),
                LookDirection = new Vec3(0, 1, 0)
            };
            var attacker = new Agent {
                Position = new Vec3(1, 0, 0)
            };

            bool result = CombatRegistry.IsInRearQuadrant(attacker, target, 180f);
            Assert.True(result);
        }

        [Fact]
        public void BlacklistTarget_AddsTargetToBlacklist()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target = new Agent { Index = 2 };

            registry.BlacklistTarget(attacker, target, 10.0f, 5.0f);

            Assert.True(registry.IsBlacklisted(attacker, target, 12.0f));
            Assert.False(registry.IsBlacklisted(attacker, target, 16.0f));
        }

        [Fact]
        public void BlacklistTarget_UpdatesExistingBlacklistExpiration()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target = new Agent { Index = 2 };

            registry.BlacklistTarget(attacker, target, 10.0f, 5.0f);
            Assert.True(registry.IsBlacklisted(attacker, target, 12.0f));

            registry.BlacklistTarget(attacker, target, 15.0f, 5.0f);
            Assert.True(registry.IsBlacklisted(attacker, target, 18.0f));
            Assert.False(registry.IsBlacklisted(attacker, target, 21.0f));
        }

        [Fact]
        public void BlacklistTarget_NullAttacker_DoesNotThrow()
        {
            var registry = new CombatRegistry();
            var target = new Agent { Index = 2 };

            var exception = Record.Exception(() => registry.BlacklistTarget(null, target, 10.0f, 5.0f));
            Assert.Null(exception);
            Assert.False(registry.IsBlacklisted(null, target, 10.0f));
        }

        [Fact]
        public void BlacklistTarget_NullTarget_DoesNotThrow()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };

            var exception = Record.Exception(() => registry.BlacklistTarget(attacker, null, 10.0f, 5.0f));
            Assert.Null(exception);
            Assert.False(registry.IsBlacklisted(attacker, null, 10.0f));
        }

        [Fact]
        public void IsBlacklisted_NullAttacker_ReturnsFalse()
        {
            var registry = new CombatRegistry();
            var target = new Agent { Index = 2 };

            bool result = registry.IsBlacklisted(null, target, 10.0f);
            Assert.False(result);
        }

        [Fact]
        public void IsBlacklisted_NullTarget_ReturnsFalse()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };

            bool result = registry.IsBlacklisted(attacker, null, 10.0f);
            Assert.False(result);
        }

        [Fact]
        public void IsBlacklisted_AttackerNotInBlacklist_ReturnsFalse()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target = new Agent { Index = 2 };

            bool result = registry.IsBlacklisted(attacker, target, 10.0f);
            Assert.False(result);
        }

        [Fact]
        public void IsBlacklisted_TargetNotInAttackerBlacklist_ReturnsFalse()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target1 = new Agent { Index = 2 };
            var target2 = new Agent { Index = 3 };

            registry.BlacklistTarget(attacker, target1, 10.0f, 5.0f);

            bool result = registry.IsBlacklisted(attacker, target2, 12.0f);
            Assert.False(result);
        }

        [Fact]
        public void IsBlacklisted_BeforeExpiration_ReturnsTrue()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target = new Agent { Index = 2 };

            registry.BlacklistTarget(attacker, target, 10.0f, 5.0f); // Expires at 15.0

            bool result = registry.IsBlacklisted(attacker, target, 14.9f);
            Assert.True(result);
        }

        [Fact]
        public void IsBlacklisted_AtOrAfterExpiration_ReturnsFalseAndRemovesFromBlacklist()
        {
            var registry = new CombatRegistry();
            var attacker = new Agent { Index = 1 };
            var target = new Agent { Index = 2 };

            registry.BlacklistTarget(attacker, target, 10.0f, 5.0f); // Expires at 15.0

            // Test exactly at expiration
            bool resultAt = registry.IsBlacklisted(attacker, target, 15.0f);
            Assert.False(resultAt);

            // Test after expiration (should have been removed by previous call)
            bool resultAfter = registry.IsBlacklisted(attacker, target, 16.0f);
            Assert.False(resultAfter);
        }

        [Fact]
        public void DemoteToQueue_TargetQueueFull_EvictsAndBlacklistsAttacker()
        {
            var registry = new CombatRegistry();
            registry.Clear();

            var settings = DynamicCombatSettings.Instance;
            int prevMaxQueue = settings.MaxQueueSlots;
            int prevMaxSlots = settings.MaxAttackSlots;
            settings.MaxQueueSlots = 3;

            try
            {
                var target = new Agent { Index = 0 };
                var attacker1 = new Agent { Index = 1 };
                var attacker2 = new Agent { Index = 2 };
                var attacker3 = new Agent { Index = 3 };
                var overflowAttacker = new Agent { Index = 4 };

                float currentTime = 10.0f;

                // First ensure max attack slots is 0 so they go to queue
                settings.MaxAttackSlots = 0;

                // We use TryRegisterAttacker to populate the attacker's current target
                // so that RemoveAttacker inside DemoteToQueue works properly.
                // However, TryRegisterAttacker just returns false if we hit the limit,
                // and it doesn't call DemoteToQueue. We need to call DemoteToQueue manually.

                // First register the 3 attackers normally
                registry.TryRegisterAttacker(attacker1, target);
                registry.TryRegisterAttacker(attacker2, target);
                registry.TryRegisterAttacker(attacker3, target);

                // Registering sets their current target. For overflowAttacker,
                // let's temporarily bump MaxQueueSlots to register them, then demote them.
                settings.MaxQueueSlots = 4;
                registry.TryRegisterAttacker(overflowAttacker, target);
                settings.MaxQueueSlots = 3;

                // Ensure queue is 4 since we bypassed the check
                var queued = registry.GetQueuedAttackers(target);
                Assert.NotNull(queued);

                // Now we remove it so it's not in the queue, but _attackerCurrentTarget is still set
                ((List<Agent>)queued).Remove(overflowAttacker);

                Assert.Equal(3, queued.Count);

                // Now attempt to demote a 4th attacker, causing an overflow
                registry.DemoteToQueue(overflowAttacker, target, currentTime);

                // The overflow attacker should be completely evicted and blacklisted for 2.0 seconds
                // because it couldn't fit in the queue of size 3.
                Assert.True(registry.IsBlacklisted(overflowAttacker, target, currentTime + 1.0f));

                // Should not be in queue
                Assert.DoesNotContain(overflowAttacker, queued);
            }
            finally
            {
                settings.MaxQueueSlots = prevMaxQueue;
                settings.MaxAttackSlots = prevMaxSlots;
            }
        }

        [Fact]
        public void DemoteToQueue_TargetQueueNotFull_AddsToQueueAndResetsTimer()
        {
            var registry = new CombatRegistry();
            registry.Clear();

            var target = new Agent { Index = 0 };
            var attacker = new Agent { Index = 1 };

            float currentTime = 10.0f;

            // Increment queue timer to verify reset
            registry.IncrementQueueTimer(attacker, 5.0f);
            Assert.Equal(5.0f, registry.GetQueueTimer(attacker));

            // Demote to queue
            registry.DemoteToQueue(attacker, target, currentTime);

            // Attacker should be in queue
            var queued = registry.GetQueuedAttackers(target);
            Assert.NotNull(queued);
            Assert.Contains(attacker, queued);

            // Queue timer should be reset
            Assert.Equal(0.0f, registry.GetQueueTimer(attacker));
        }
    }
}
