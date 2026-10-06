using System;
using System.Collections.Generic;

namespace DungeonGuardians.Core
{
    public enum LossCause
    {
        None,
        // Touched by an active guardian.
        Guardian,
        // A hole closed while the explorer was inside it.
        Buried
    }

    public enum GuardianKind
    {
        // Always goes for the explorer.
        Chaser,
        // Walks its beat and returns to its post; gives chase only when the explorer comes close or into view.
        Warden,
        // Sees only a couple of cells, but goes to the last noise: digging, gold taken, a hard landing.
        Listener,
        // Slow; breaks fragile floors under its weight and is slower to climb out of a hole.
        Heavy,
        // Stands still, then lunges a few cells at speed; its cracks flare up red just before.
        Infected
    }

    public sealed class RuntimeLevelState
    {
        public readonly LevelDefinition Definition;
        public readonly TileType[,] Tiles;
        public readonly HashSet<GridPoint> RemainingGold;
        public readonly List<GuardianState> Guardians;
        public readonly List<HoleState> Holes;
        public GridPoint PlayerPosition;
        // Ticks left in the current dig; movement is locked while it is above zero.
        public int PlayerDigTicks;
        // -1 for left, 1 for right.
        public int PlayerDigDirection;
        public bool ExitOpen;
        // Seal trial: a plate is pressed this tick; the gates are open.
        public bool PlatePressed;
        public bool GatesOpen;
        // In "hold" mode: ticks the gates stay open after the plate is released.
        public int GateOpenTicks;
        public bool Won;
        public bool Lost;
        public LossCause LossCause;
        // Ticks since the level started.
        public int Ticks;
        // The last noise the explorer made (for listeners) and the tick it was made; -1 before the first.
        public GridPoint NoisePoint;
        public int NoiseTick = -1;

        public RuntimeLevelState(LevelDefinition definition)
        {
            Definition = definition;
            Tiles = LevelParser.ParseTiles(definition);
            RemainingGold = new HashSet<GridPoint>(definition.gold ?? Array.Empty<GridPoint>());
            Guardians = new List<GuardianState>();
            Holes = new List<HoleState>();
            PlayerPosition = definition.playerStart;

            GridPoint[] starts = definition.guardians ?? Array.Empty<GridPoint>();
            GuardianSpec[] specs = definition.guardianKinds ?? Array.Empty<GuardianSpec>();
            for (int i = 0; i < starts.Length; i++)
            {
                Guardians.Add(new GuardianState(starts[i], i < specs.Length ? specs[i] : null));
            }
        }
    }

    public sealed class GuardianState
    {
        public GridPoint Position;
        public int RespawnTicks;
        public bool Trapped;
        // Accumulates speed per tick; the guardian steps one cell each time it reaches 1.
        public float MoveBudget;

        public readonly GuardianKind Kind;
        // Warden: where it stands guard, its beat on that row and the way it is walking; chasing the explorer.
        public readonly GridPoint Post;
        public readonly int PatrolLeft;
        public readonly int PatrolRight;
        public int PatrolDirection = 1;
        public bool Alerted;
        // Listener: the noise it is walking to, and the tick of the last noise it took notice of.
        public GridPoint? NoiseGoal;
        public int HeardTick = -1;
        // Infected: ticks left standing still before the next lunge, and cells left in the current lunge.
        public int RestTicks;
        public int LungeCells;

        public GuardianState(GridPoint position, GuardianSpec spec = null)
        {
            Position = position;
            Post = position;
            Kind = ParseKind(spec?.kind);
            PatrolLeft = spec != null && spec.patrolLeft >= 0 ? spec.patrolLeft : position.x;
            PatrolRight = spec != null && spec.patrolRight >= 0 ? spec.patrolRight : position.x;
        }

        // Back to how it started, for a new life of the explorer.
        public void ResetBehaviour()
        {
            PatrolDirection = 1;
            Alerted = false;
            NoiseGoal = null;
            RestTicks = 0;
            LungeCells = 0;
        }

        private static GuardianKind ParseKind(string kind)
        {
            switch (kind)
            {
                case "warden":
                    return GuardianKind.Warden;
                case "listener":
                    return GuardianKind.Listener;
                case "heavy":
                    return GuardianKind.Heavy;
                case "infected":
                    return GuardianKind.Infected;
                default:
                    return GuardianKind.Chaser;
            }
        }
    }

    public sealed class HoleState
    {
        public GridPoint Position;
        public TileType RestoresTo;
        public int RemainingTicks;

        public HoleState(GridPoint position, TileType restoresTo, int remainingTicks)
        {
            Position = position;
            RestoresTo = restoresTo;
            RemainingTicks = remainingTicks;
        }
    }
}
