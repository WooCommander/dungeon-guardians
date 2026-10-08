using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonGuardians.Core
{
    // Checks every level: that it can be won at all (LevelReachability), that the route found really wins in the
    // simulation with the guardians taken out (LevelWalkthrough), and that its recorded winning run, guardians and
    // all, still wins with the current rules and balance (LevelReplay). Shared by the command-line check for CI
    // (tools/LevelCheck) and the editor menu (Dungeon Guardians > Проверить уровни).
    public static class LevelValidation
    {
        public sealed class Report
        {
            public int Levels;
            public int Failed;
            // Winnable only by losing lives on purpose (a dead end after some gold).
            public int Warned;
            public int WithoutReplay;
            public readonly StringBuilder Text = new StringBuilder();
            public bool Passed => Failed == 0;
        }

        // replayFor gives the text of a level's replay by its id, or null when it has none. With requireReplays a
        // level without one fails; with strict, so does one that can only be won by losing lives.
        public static Report CheckAll(IEnumerable<LevelDefinition> levels, Func<string, string> replayFor, BalanceConfig balance, bool requireReplays, bool strict = false)
        {
            var report = new Report();
            foreach (LevelDefinition level in levels)
            {
                report.Levels++;
                bool failed = false;
                bool warned = false;
                var lines = new List<string>();

                LevelReachability.Result reach = LevelReachability.Check(level);
                if (reach.Passable && reach.LivesNeeded == 0)
                {
                    lines.Add("  can be won: all gold and the exit reachable in one run");
                    if (HasGates(level))
                    {
                        // The walk cannot press plates or lure a guardian onto one.
                        lines.Add("  walk without guardians skipped: the way leads through gates");
                    }
                    else
                    {
                        LevelReplay walk = LevelWalkthrough.Play(level, balance, reach.Runs[0], out string walkProblem);
                        // The walk written out and read back must win again: the replay format keeps every key.
                        bool roundTrip = walk != null && LevelReplay.Parse(walk.Serialize()).Run(LevelWalkthrough.WithoutGuardians(level), balance).Won;
                        if (walk != null && !roundTrip)
                        {
                            failed = true;
                            lines.Add("  REPLAY FORMAT BROKEN: the walk, written out and read back, no longer wins");
                        }
                        else if (walk != null)
                        {
                            lines.Add($"  walked through without guardians: {walk.Ticks} ticks ({walk.Ticks / balance.TickRate:0.0} s)");
                        }
                        else
                        {
                            failed = true;
                            lines.Add("  WALK FAILS: the route does not work in the simulation, guardians aside: " + walkProblem);
                        }
                    }
                }
                else if (reach.Passable)
                {
                    warned = true;
                    failed |= strict;
                    lines.Add($"  ONLY BY LOSING LIVES: {reach.LivesNeeded} of the {LevelReplay.Lives} must be lost on the way");
                    foreach (List<GridPoint> run in reach.Runs)
                    {
                        lines.Add("    run: start -> " + string.Join(" -> ", run));
                    }
                }
                else
                {
                    failed = true;
                    foreach (string problem in reach.Problems)
                    {
                        lines.Add("  CANNOT BE WON: " + problem);
                    }
                }

                foreach (string note in reach.Notes)
                {
                    lines.Add("  note: " + note);
                }

                string text = replayFor(level.id);
                if (text == null)
                {
                    report.WithoutReplay++;
                    failed |= requireReplays;
                    lines.Add(requireReplays
                        ? "  NO REPLAY: play the level through in the editor to record one"
                        : "  no replay yet: play the level through in the editor to record one");
                }
                else
                {
                    try
                    {
                        LevelReplay replay = LevelReplay.Parse(text);
                        LevelReplay.RunResult run = replay.Run(level, balance);
                        if (run.Won)
                        {
                            lines.Add($"  replay wins: {run.Ticks} ticks ({run.Ticks / balance.TickRate:0.0} s), {run.LivesLost} lives lost");
                        }
                        else
                        {
                            failed = true;
                            lines.Add("  REPLAY FAILS: " + run.Problem);
                        }
                    }
                    catch (FormatException exception)
                    {
                        failed = true;
                        lines.Add("  REPLAY UNREADABLE: " + exception.Message);
                    }
                }

                report.Failed += failed ? 1 : 0;
                report.Warned += warned && !failed ? 1 : 0;
                report.Text.Append(failed ? "FAIL " : warned ? "warn " : "ok   ").Append(level.id).Append("  «").Append(level.title).Append("»\n");
                foreach (string line in lines)
                {
                    report.Text.Append(line).Append('\n');
                }
            }

            report.Text.Append($"\n{report.Levels} levels: {report.Levels - report.Failed - report.Warned} ok, {report.Warned} warned, {report.Failed} failed, {report.WithoutReplay} without a replay.\n");
            return report;
        }

        private static bool HasGates(LevelDefinition level)
        {
            foreach (string row in level.rows)
            {
                if (row.IndexOf('|') >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
