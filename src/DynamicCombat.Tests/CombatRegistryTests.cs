using System;
using Xunit;
using DynamicCombat;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

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
    }
}
