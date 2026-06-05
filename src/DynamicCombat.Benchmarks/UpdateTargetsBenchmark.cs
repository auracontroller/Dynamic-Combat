using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DynamicCombat;
using TaleWorlds.MountAndBlade;

namespace DynamicCombat.Benchmarks
{
    [MemoryDiagnoser]
    public class UpdateTargetsBenchmark
    {
        private Dictionary<Agent, List<Agent>> _activeEngagements = new();
        private Dictionary<Agent, List<Agent>> _queuedAttackers = new();

        [Params(10, 50, 100, 500)]
        public int NumberOfAgents { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _activeEngagements.Clear();
            _queuedAttackers.Clear();

            for (int i = 0; i < NumberOfAgents; i++)
            {
                var agent = new Agent { Index = i };
                _activeEngagements[agent] = new List<Agent>();

                // Add some overlap, some unique
                if (i % 2 == 0)
                {
                    _queuedAttackers[agent] = new List<Agent>();
                }
                else
                {
                    var uniqueAgent = new Agent { Index = i + NumberOfAgents };
                    _queuedAttackers[uniqueAgent] = new List<Agent>();
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void Baseline_List()
        {
            List<Agent> targetsToProcess = new List<Agent>(_activeEngagements.Keys);
            foreach (var t in _queuedAttackers.Keys)
            {
                if (!targetsToProcess.Contains(t))
                    targetsToProcess.Add(t);
            }
        }

        [Benchmark]
        public void Optimized_HashSet()
        {
            HashSet<Agent> targetsToProcess = new HashSet<Agent>(_activeEngagements.Keys);
            foreach (var t in _queuedAttackers.Keys)
            {
                targetsToProcess.Add(t); // HashSet handles duplicates automatically and is O(1)
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<UpdateTargetsBenchmark>();
        }
    }
}
