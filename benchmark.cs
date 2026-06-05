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
        Vec3[] positions = new Vec3[numAgents];
        Random rand = new Random(42);
        for(int i=0; i<numAgents; i++)
        {
            positions[i] = new Vec3((float)rand.NextDouble() * 100f, (float)rand.NextDouble() * 100f, 0f);
        }

        int iterations = 1000; // Simulate 1000 frames

        // Warmup
        RunOriginal(positions, 10);
        RunOptimized(positions, 10);

        Stopwatch sw = Stopwatch.StartNew();
        long unoptimizedResult = RunOriginal(positions, iterations);
        sw.Stop();
        long unoptimizedTime = sw.ElapsedMilliseconds;

        sw.Restart();
        long optimizedResult = RunOptimized(positions, iterations);
        sw.Stop();
        long optimizedTime = sw.ElapsedMilliseconds;

        Console.WriteLine($"Unoptimized time: {unoptimizedTime} ms");
        Console.WriteLine($"Optimized time:   {optimizedTime} ms");
        Console.WriteLine($"Improvement:      {((unoptimizedTime - optimizedTime) / (double)unoptimizedTime * 100):F2}%");

        // Ensure the compiler doesn't optimize away the loop
        if(unoptimizedResult != optimizedResult) {
            Console.WriteLine("Mismatch in results!");
        }
    }

    static long RunOriginal(Vec3[] positions, int iterations)
    {
        long dummy = 0;
        for(int frame = 0; frame < iterations; frame++)
        {
            for(int i = 0; i < positions.Length; i++)
            {
                Vec3 attacker = positions[i];
                for(int j = 0; j < positions.Length; j++)
                {
                    if (i == j) continue;
                    Vec3 peer = positions[j];

                    float distToPeer = attacker.Distance(peer);
                    if (distToPeer < 2.0f)
                    {
                        dummy += 1;
                        // pushForce += ...
                    }
                }
            }
        }
        return dummy;
    }

    static long RunOptimized(Vec3[] positions, int iterations)
    {
        long dummy = 0;
        for(int frame = 0; frame < iterations; frame++)
        {
            for(int i = 0; i < positions.Length; i++)
            {
                Vec3 attacker = positions[i];
                for(int j = 0; j < positions.Length; j++)
                {
                    if (i == j) continue;
                    Vec3 peer = positions[j];

                    float distToPeerSq = attacker.DistanceSquared(peer);
                    if (distToPeerSq < 4.0f)
                    {
                        float distToPeer = (float)Math.Sqrt(distToPeerSq);
                        dummy += 1;
                        // pushForce += ...
                    }
                }
            }
        }
        return dummy;
    }
}
