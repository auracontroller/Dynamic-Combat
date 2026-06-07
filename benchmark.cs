using System;
using System.Diagnostics;

public struct Vec3
{
    public float X, Y, Z;
    public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

    public float Distance(Vec3 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public float DistanceSquared(Vec3 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }
}

public class Benchmark
{
    public static void Main()
    {
        int numAgents = 1000;
        Vec3[] attackers = new Vec3[numAgents];
        Vec3[] targets = new Vec3[numAgents];
        Random rand = new Random(42);
        for(int i=0; i<numAgents; i++)
        {
            attackers[i] = new Vec3((float)rand.NextDouble() * 100f, (float)rand.NextDouble() * 100f, 0f);
            targets[i] = new Vec3((float)rand.NextDouble() * 100f, (float)rand.NextDouble() * 100f, 0f);
        }

        int iterations = 100000; // Simulate 100000 frames

        // Warmup
        RunOriginal(attackers, targets, 1000, 10f);
        RunOptimized(attackers, targets, 1000, 10f);

        Stopwatch sw = Stopwatch.StartNew();
        long unoptimizedResult = RunOriginal(attackers, targets, iterations, 10f);
        sw.Stop();
        long unoptimizedTime = sw.ElapsedMilliseconds;

        sw.Restart();
        long optimizedResult = RunOptimized(attackers, targets, iterations, 10f);
        sw.Stop();
        long optimizedTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Unoptimized time: {unoptimizedTime} ms");
        Console.WriteLine($"Optimized time:   {optimizedTime} ms");
        Console.WriteLine($"Improvement:      {((unoptimizedTime - optimizedTime) / (double)unoptimizedTime * 100):F2}%");

        if(unoptimizedResult != optimizedResult) {
            Console.WriteLine("Mismatch in results!");
        }
    }

    static long RunOriginal(Vec3[] attackers, Vec3[] targets, int iterations, float maxDistance)
    {
        long dummy = 0;
        for(int frame = 0; frame < iterations; frame++)
        {
            for(int i = 0; i < attackers.Length; i++)
            {
                Vec3 attacker = attackers[i];
                Vec3 target = targets[i];

                float distanceToTarget = attacker.Distance(target);
                if (distanceToTarget > maxDistance)
                {
                    dummy += 1;
                }
            }
        }
        return dummy;
    }

    static long RunOptimized(Vec3[] attackers, Vec3[] targets, int iterations, float maxDistance)
    {
        long dummy = 0;
        float maxDistanceSq = maxDistance * maxDistance;
        for(int frame = 0; frame < iterations; frame++)
        {
            for(int i = 0; i < attackers.Length; i++)
            {
                Vec3 attacker = attackers[i];
                Vec3 target = targets[i];

                float distanceToTargetSq = attacker.DistanceSquared(target);
                if (distanceToTargetSq > maxDistanceSq)
                {
                    dummy += 1;
                }
            }
        }
        return dummy;
    }
}
