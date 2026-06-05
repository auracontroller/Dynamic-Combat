using System;
using System.Collections.Generic;

namespace TaleWorlds.Library
{
    public struct Vec3 {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public Vec2 AsVec2 => new Vec2(X, Y);
        public float DistanceSquared(Vec3 other) {
            float dx = X - other.X; float dy = Y - other.Y; float dz = Z - other.Z;
            return dx*dx + dy*dy + dz*dz;
        }
    }
    public struct Vec2 {
        public float X, Y;
        public Vec2(float x, float y) { X = x; Y = y; }
        public float LengthSquared => X * X + Y * Y;
        public Vec2 Normalized() {
            float len = (float)Math.Sqrt(LengthSquared);
            return new Vec2(X / len, Y / len);
        }
        public static float DotProduct(Vec2 a, Vec2 b) {
            return a.X * b.X + a.Y * b.Y;
        }
        public static Vec2 operator -(Vec2 a, Vec2 b) {
            return new Vec2(a.X - b.X, a.Y - b.Y);
        }
    }
    public static class MBMath {
        public static float ClampFloat(float value, float min, float max) {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
    public static class Debug {
        public static void Print(string text, int color = 0, object type = null) { }
    }
}

namespace TaleWorlds.MountAndBlade
{
    public class Mission {
        public static Mission Current { get; set; } = new Mission();
        public float CurrentTime { get; set; } = 0f;
        public List<Agent> Agents { get; set; } = new List<Agent>();
    }

    public class Agent {
        public TaleWorlds.Library.Vec3 Position { get; set; }
        public TaleWorlds.Library.Vec3 LookDirection { get; set; }
        public int Index { get; set; }
        public bool IsHuman { get; set; } = true;
        public Team Team { get; set; }
        public float Health { get; set; } = 100f;

        public bool IsActive() => true;
    }

    public class Team {
        public bool IsEnemyOf(Team other) => true;
    }
}
