using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonGuardians.Core
{
    // A won run of a level as the keys held on every tick of the simulation, and the moments a new life began.
    // The simulation is deterministic, so playing the keys back through it wins again, unless the level or the
    // balance has changed under it: the regression check that a level stays passable (tools/LevelCheck).
    //
    // Text form, one file per level (LevelReplays/<id>.txt):
    //   # comment lines
    //   level: level_07
    //   R:12 .:5 RU:3 E:1 * L:4
    // Each step is the keys held and for how many ticks: L R U D for the directions, Q and E for digging left and
    // right, "." for none. "*" is a lost life: the explorer starts again (DungeonSimulation.RevivePlayer).
    public sealed class LevelReplay
    {
        // The explorer has this many lives on a level (GameController.Lives).
        public const int Lives = 3;

        public struct Step
        {
            public bool Revive;
            public InputSnapshot Input;
            public int Ticks;
        }

        public sealed class RunResult
        {
            public bool Won;
            public int Ticks;
            public int LivesLost;
            // Why the run did not win; empty when it did.
            public string Problem = string.Empty;
        }

        public string LevelId = string.Empty;
        public readonly List<Step> Steps = new List<Step>();

        public int Ticks
        {
            get
            {
                int ticks = 0;
                foreach (Step step in Steps)
                {
                    ticks += step.Revive ? 0 : step.Ticks;
                }
                return ticks;
            }
        }

        public int LivesLost
        {
            get
            {
                int lost = 0;
                foreach (Step step in Steps)
                {
                    lost += step.Revive ? 1 : 0;
                }
                return lost;
            }
        }

        // One tick played with these keys (call before DungeonSimulation.Tick, only while the explorer is alive).
        public void Add(InputSnapshot input)
        {
            if (Steps.Count > 0)
            {
                Step last = Steps[Steps.Count - 1];
                if (!last.Revive && SameKeys(last.Input, input))
                {
                    last.Ticks++;
                    Steps[Steps.Count - 1] = last;
                    return;
                }
            }

            Steps.Add(new Step { Input = Keys(input), Ticks = 1 });
        }

        // A life was lost and the explorer starts again.
        public void AddRevive()
        {
            Steps.Add(new Step { Revive = true });
        }

        // Plays the replay through a fresh simulation of the level.
        public RunResult Run(LevelDefinition level, BalanceConfig balance)
        {
            var result = new RunResult();
            var simulation = new DungeonSimulation(level, balance);
            RuntimeLevelState state = simulation.State;
            for (int s = 0; s < Steps.Count; s++)
            {
                Step step = Steps[s];
                if (step.Revive)
                {
                    if (!state.Lost)
                    {
                        result.Problem = $"tick {state.Ticks}: the recorded run lost a life here, this one did not; the replay is out of step";
                        return result;
                    }

                    result.LivesLost++;
                    if (result.LivesLost >= Lives)
                    {
                        result.Problem = $"tick {state.Ticks}: all {Lives} lives lost";
                        return result;
                    }

                    simulation.RevivePlayer();
                    continue;
                }

                for (int t = 0; t < step.Ticks; t++)
                {
                    if (state.Lost)
                    {
                        result.Problem = $"tick {state.Ticks}: {Describe(state)} where the recorded run went on";
                        result.Ticks = state.Ticks;
                        return result;
                    }

                    simulation.Tick(step.Input);
                    if (state.Won)
                    {
                        result.Won = true;
                        result.Ticks = state.Ticks;
                        return result;
                    }
                }
            }

            result.Ticks = state.Ticks;
            result.Problem = state.Lost
                ? $"tick {state.Ticks}: {Describe(state)} at the end of the replay"
                : $"tick {state.Ticks}: the replay ended without a win ({state.RemainingGold.Count} gold left, explorer at {state.PlayerPosition})";
            return result;
        }

        private static string Describe(RuntimeLevelState state)
        {
            return state.LossCause == LossCause.Buried
                ? $"the explorer was buried at {state.PlayerPosition}"
                : $"a guardian caught the explorer at {state.PlayerPosition}";
        }

        public string Serialize(string comment = null)
        {
            var text = new StringBuilder();
            text.Append("# Dungeon Guardians replay: the keys held each tick (see Assets/Scripts/Core/LevelReplay.cs).\n");
            if (!string.IsNullOrEmpty(comment))
            {
                text.Append("# ").Append(comment).Append('\n');
            }

            text.Append($"# {Ticks} ticks, {LivesLost} lives lost\n");
            text.Append("level: ").Append(LevelId).Append('\n');
            int onLine = 0;
            foreach (Step step in Steps)
            {
                text.Append(step.Revive ? "*" : $"{KeyString(step.Input)}:{step.Ticks}");
                onLine++;
                text.Append(onLine % 16 == 0 ? '\n' : ' ');
            }

            text.Append('\n');
            return text.ToString();
        }

        public static LevelReplay Parse(string text)
        {
            var replay = new LevelReplay();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith("level:"))
                {
                    replay.LevelId = line.Substring("level:".Length).Trim();
                    continue;
                }

                foreach (string token in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (token == "*")
                    {
                        replay.AddRevive();
                        continue;
                    }

                    int colon = token.IndexOf(':');
                    if (colon <= 0 || !int.TryParse(token.Substring(colon + 1), out int ticks) || ticks <= 0)
                    {
                        throw new FormatException($"Bad replay step '{token}'.");
                    }

                    replay.Steps.Add(new Step { Input = ParseKeys(token.Substring(0, colon)), Ticks = ticks });
                }
            }

            return replay;
        }

        private static InputSnapshot Keys(InputSnapshot input)
        {
            return new InputSnapshot
            {
                Left = input.Left, Right = input.Right, Up = input.Up, Down = input.Down,
                DigLeft = input.DigLeft, DigRight = input.DigRight,
            };
        }

        private static bool SameKeys(InputSnapshot a, InputSnapshot b)
        {
            return a.Left == b.Left && a.Right == b.Right && a.Up == b.Up && a.Down == b.Down
                && a.DigLeft == b.DigLeft && a.DigRight == b.DigRight;
        }

        private static string KeyString(InputSnapshot input)
        {
            var keys = new StringBuilder();
            if (input.Left) keys.Append('L');
            if (input.Right) keys.Append('R');
            if (input.Up) keys.Append('U');
            if (input.Down) keys.Append('D');
            if (input.DigLeft) keys.Append('Q');
            if (input.DigRight) keys.Append('E');
            return keys.Length == 0 ? "." : keys.ToString();
        }

        private static InputSnapshot ParseKeys(string keys)
        {
            var input = new InputSnapshot();
            foreach (char key in keys)
            {
                switch (key)
                {
                    case 'L': input.Left = true; break;
                    case 'R': input.Right = true; break;
                    case 'U': input.Up = true; break;
                    case 'D': input.Down = true; break;
                    case 'Q': input.DigLeft = true; break;
                    case 'E': input.DigRight = true; break;
                    case '.': break;
                    default: throw new FormatException($"Bad replay key '{key}' in '{keys}'.");
                }
            }

            return input;
        }
    }
}
