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
    }
}
