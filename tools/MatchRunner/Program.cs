using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using OCT7.Sim;
using OCT7.Sim.AI;
using OCT7.Sim.Data;
using OCT7.Sim.Match;

namespace OCT7.Tools.MatchRunner
{
    /// <summary>
    /// Headless AI-vs-AI skirmish runner — the automated playtester (docs/04-ai-and-difficulty.md §4).
    /// Plays full matches on the default skirmish map until victory (or the time limit) and reports
    /// win rates, match length, stalls and determinism.
    /// Examples:
    ///   dotnet run --project tools/MatchRunner -- --matches 10 --p0 idf --p1 hamas --d0 normal --d1 normal --verify-determinism
    ///   dotnet run --project tools/MatchRunner -- --all-matchups --matches 4 --d0 hard --d1 easy
    /// </summary>
    public static class Program
    {
        private static readonly string[] Factions = { "idf", "hamas", "hezbollah" };

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

            var matchups = options.AllMatchups
                ? (from a in Factions from b in Factions select (a, b)).ToList()
                : new List<(string, string)> { (options.Faction0, options.Faction1) };

            var reports = new List<object>();
            bool ok = true;
            var total = Stopwatch.StartNew();
            foreach (var (f0, f1) in matchups)
            {
                var results = new List<MatchResult>();
                for (int m = 0; m < options.Matches; m++)
                {
                    ulong seed = options.Seed + (ulong)m;
                    var result = RunMatch(data, options, f0, f1, seed);
                    if (options.VerifyDeterminism)
                    {
                        result.Deterministic = RunMatch(data, options, f0, f1, seed).FinalHash == result.FinalHash;
                    }

                    results.Add(result);
                }

                bool determinismOk = results.All(r => r.Deterministic != false);
                bool noErrors = results.All(r => r.Error == null);
                ok &= determinismOk && noErrors;
                int finished = results.Count(r => r.Finished);
                reports.Add(new
                {
                    matchup = $"{f0} ({options.Difficulty0}) vs {f1} ({options.Difficulty1})",
                    matches = results.Count,
                    finishedPercent = Math.Round(100.0 * finished / results.Count, 1),
                    p0WinPercent = Math.Round(100.0 * results.Count(r => r.Winner == 0) / results.Count, 1),
                    p1WinPercent = Math.Round(100.0 * results.Count(r => r.Winner == 1) / results.Count, 1),
                    averageMinutes = Math.Round(results.Average(r => r.Minutes), 1),
                    averageSquadsLost = Math.Round(results.Average(r => r.SquadsLost), 1),
                    averageStructuresBuilt = Math.Round(results.Average(r => r.StructuresBuilt), 1),
                    determinism = options.VerifyDeterminism ? (determinismOk ? "ok" : "FAILED") : "not checked",
                    errors = noErrors ? null : results.Where(r => r.Error != null).Select(r => $"seed {r.Seed}: {r.Error}").ToArray(),
                    averageMsPerMatch = Math.Round(results.Average(r => r.WallClockMs), 0),
                    results = options.Verbose ? results : null,
                });
            }

            total.Stop();
            Console.WriteLine(JsonSerializer.Serialize(new { wallClockSeconds = Math.Round(total.Elapsed.TotalSeconds, 1), reports }, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }));

            return ok ? 0 : 1;
        }

        private static MatchResult RunMatch(GameDataSet data, Options options, string f0, string f1, ulong seed)
        {
            var watch = Stopwatch.StartNew();
            var result = new MatchResult { Seed = seed };
            try
            {
                var sim = MatchSetup.CreateSkirmish(data, f0, f1, seed, options.MapId);
                var ais = new List<IAiController>
                {
                    SkirmishAi.Create(sim, 0, options.Difficulty0),
                    SkirmishAi.Create(sim, 1, options.Difficulty1),
                };
                var buffer = new List<Command>();
                int maxTicks = (int)(options.MaxMinutes * SimConfig.TicksPerMinute);
                while (!sim.IsOver && sim.Tick < maxTicks)
                {
                    SimLoop.Step(sim, ais, buffer);
                    foreach (var e in sim.Events)
                    {
                        if (e.Type == SimEventType.SquadDestroyed)
                        {
                            result.SquadsLost++;
                        }
                        else if (e.Type == SimEventType.StructureCompleted)
                        {
                            result.StructuresBuilt++;
                        }
                    }
                }

                result.Finished = sim.IsOver;
                result.Winner = sim.IsOver ? sim.WinnerId : -1;
                result.Minutes = Math.Round(sim.ElapsedSeconds / 60.0, 1);
                result.Tickets = sim.Players.Select(p => Math.Round(p.Tickets, 0)).ToArray();
                result.FinalHash = sim.ComputeStateHash().ToString("x16", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name + ": " + ex.Message + " @ " + ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim();
            }

            watch.Stop();
            result.WallClockMs = watch.Elapsed.TotalMilliseconds;
            return result;
        }

        private sealed class MatchResult
        {
            public ulong Seed { get; set; }
            public bool Finished { get; set; }
            public int Winner { get; set; } = -1;
            public double Minutes { get; set; }
            public double[] Tickets { get; set; }
            public int SquadsLost { get; set; }
            public int StructuresBuilt { get; set; }
            public string FinalHash { get; set; }
            public bool? Deterministic { get; set; }
            public string Error { get; set; }
            public double WallClockMs { get; set; }
        }

        private sealed class Options
        {
            public const string Usage =
                "Usage: MatchRunner [--matches N] [--seed S] [--p0 FACTION] [--p1 FACTION] [--d0 DIFF] [--d1 DIFF]\n" +
                "                   [--all-matchups] [--max-minutes M] [--map ID] [--verify-determinism] [--verbose] [--data DIR]\n" +
                "Factions: idf | hamas | hezbollah. Difficulties: easy | normal | hard.\n" +
                "Defaults: 4 matches, seed 1, idf vs hamas, normal vs normal, 45 minute limit.";

            public int Matches { get; private set; } = 4;
            public ulong Seed { get; private set; } = 1;
            public string Faction0 { get; private set; } = "idf";
            public string Faction1 { get; private set; } = "hamas";
            public string Difficulty0 { get; private set; } = "normal";
            public string Difficulty1 { get; private set; } = "normal";
            public bool AllMatchups { get; private set; }
            public double MaxMinutes { get; private set; } = 45;
            public string MapId { get; private set; }
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
                        case "--seed": o.Seed = ulong.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--p0": o.Faction0 = Next(); break;
                        case "--p1": o.Faction1 = Next(); break;
                        case "--d0": o.Difficulty0 = Next(); break;
                        case "--d1": o.Difficulty1 = Next(); break;
                        case "--all-matchups": o.AllMatchups = true; break;
                        case "--max-minutes": o.MaxMinutes = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                        case "--map": o.MapId = Next(); break;
                        case "--data": o.DataDirectory = Next(); break;
                        case "--verify-determinism": o.VerifyDeterminism = true; break;
                        case "--verbose": o.Verbose = true; break;
                        case "-h":
                        case "--help": o.ShowHelp = true; break;
                        default: throw new ArgumentException($"Unknown argument '{args[i]}'");
                    }
                }

                if (o.Matches < 1 || o.MaxMinutes <= 0)
                {
                    throw new ArgumentException("--matches must be >= 1 and --max-minutes > 0");
                }

                return o;
            }
        }
    }
}
