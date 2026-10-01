using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using OCT7.Sim;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;

namespace OCT7.Tools.MatchRunner
{
    /// <summary>
    /// Headless AI-vs-AI batch runner — the automated playtester (docs/04-ai-and-difficulty.md §4).
    /// Example: dotnet run --project tools/MatchRunner -- --matches 20 --ticks 6000 --p0 idf --p1 hamas --verify-determinism
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(Options.Usage);
                return 2;
            }

            if (options.ShowHelp)
            {
                Console.WriteLine(Options.Usage);
                return 0;
            }

            var data = RepoData.Load(options.DataDirectory);
            var errors = GameDataValidator.Validate(data);
            if (errors.Count > 0)
            {
                Console.Error.WriteLine("Game data is invalid:");
                errors.ForEach(e => Console.Error.WriteLine("  - " + e));
                return 1;
            }

            var results = new List<MatchResult>();
            var total = Stopwatch.StartNew();
            for (int m = 0; m < options.Matches; m++)
            {
                ulong seed = options.Seed + (ulong)m;
                var result = RunMatch(data, options, seed);
                if (options.VerifyDeterminism)
                {
                    var replay = RunMatch(data, options, seed);
                    result.Deterministic = replay.FinalHash == result.FinalHash;
                }

                results.Add(result);
            }

            total.Stop();
            bool determinismOk = results.All(r => r.Deterministic != false);
            var summary = new
            {
                matchup = $"{options.Faction0} vs {options.Faction1}",
                matches = results.Count,
                ticksPerMatch = options.Ticks,
                simulatedMinutesPerMatch = options.Ticks / (double)SimConfig.TicksPerMinute,
                determinism = options.VerifyDeterminism ? (determinismOk ? "ok" : "FAILED") : "not checked",
                wallClockSeconds = Math.Round(total.Elapsed.TotalSeconds, 2),
                averageMsPerMatch = Math.Round(results.Average(r => r.WallClockMs), 1),
                speedupVsRealTime = Math.Round(results.Sum(r => options.Ticks * SimConfig.TickSeconds * 1000.0) / Math.Max(1.0, results.Sum(r => r.WallClockMs)), 1),
                averageFinalDistanceBetweenForces = Math.Round(results.Average(r => r.FinalForceDistance), 1),
                results = options.Verbose ? results : null,
            };

            Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            }));

            return determinismOk ? 0 : 1;
        }

        private static MatchResult RunMatch(GameDataSet data, Options options, ulong seed)
        {
            var watch = Stopwatch.StartNew();
            var sim = MatchSetup.CreateSandboxMatch(data, options.Faction0, options.Faction1, seed);
            var ais = new List<IAiController> { new AdvanceAi(0, seed), new AdvanceAi(1, seed) };
            var buffer = new List<Command>();
            for (int t = 0; t < options.Ticks; t++)
            {
                SimLoop.Step(sim, ais, buffer);
            }

            watch.Stop();
            return new MatchResult
            {
                Seed = seed,
                FinalHash = sim.ComputeStateHash().ToString("x16", CultureInfo.InvariantCulture),
                WallClockMs = watch.Elapsed.TotalMilliseconds,
                CommandsExecuted = sim.ExecutedCommandCount,
                FinalForceDistance = Vec2.Distance(Centroid(sim, 0), Centroid(sim, 1)),
                Manpower = sim.Players.Select(p => Math.Round(p.Manpower, 1)).ToArray(),
            };
        }

        private static Vec2 Centroid(Simulation sim, int playerId)
        {
            var sum = Vec2.Zero;
            int count = 0;
            foreach (var s in sim.World.Squads)
            {
                if (s.OwnerId == playerId)
                {
                    sum += s.Position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Vec2.Zero;
        }

        private sealed class MatchResult
        {
            public ulong Seed { get; set; }
            public string FinalHash { get; set; }
            public bool? Deterministic { get; set; }
            public double WallClockMs { get; set; }
            public int CommandsExecuted { get; set; }
            public float FinalForceDistance { get; set; }
            public double[] Manpower { get; set; }
        }

        private sealed class Options
        {
            public const string Usage =
                "Usage: MatchRunner [--matches N] [--ticks T] [--seed S] [--p0 FACTION] [--p1 FACTION]\n" +
                "                   [--verify-determinism] [--verbose] [--data DIR]\n" +
                "Factions: idf | hamas | hezbollah. Defaults: 10 matches, 6000 ticks (10 min), seed 1, idf vs hamas.";

            public int Matches { get; private set; } = 10;
            public int Ticks { get; private set; } = 6000;
            public ulong Seed { get; private set; } = 1;
            public string Faction0 { get; private set; } = "idf";
            public string Faction1 { get; private set; } = "hamas";
            public bool VerifyDeterminism { get; private set; }
            public bool Verbose { get; private set; }
            public bool ShowHelp { get; private set; }
            public string DataDirectory { get; private set; }

            public static Options Parse(string[] args)
            {
                var o = new Options();
                for (int i = 0; i < args.Length; i++)
                {
                    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {args[i]}");
                    switch (args[i])
                    {
                        case "--matches": o.Matches = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--ticks": o.Ticks = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--seed": o.Seed = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--p0": o.Faction0 = Next(); break;
                        case "--p1": o.Faction1 = Next(); break;
                        case "--data": o.DataDirectory = Next(); break;
                        case "--verify-determinism": o.VerifyDeterminism = true; break;
                        case "--verbose": o.Verbose = true; break;
                        case "-h":
                        case "--help": o.ShowHelp = true; break;
                        default: throw new ArgumentException($"Unknown argument '{args[i]}'");
                    }
                }

                if (o.Matches < 1 || o.Ticks < 1)
                {
                    throw new ArgumentException("--matches and --ticks must be >= 1");
                }

                return o;
            }
        }
    }
}
