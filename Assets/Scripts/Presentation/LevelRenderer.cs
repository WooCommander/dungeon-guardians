using System.Collections.Generic;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    // Builds the level from the modular 3D pieces in Resources/Environment and places the characters.
    // A cell is 1 x 1 unit; cell (x, y) is centred on world (x, y) and the gameplay plane is z = 0.
    public sealed class LevelRenderer : MonoBehaviour
    {
        // Character height in cells and depth. Characters stand on the centre line of the blocks (z = 0), so they read as
        // standing on the platform rather than in front of it.
        private const float ExplorerHeight = 0.92f;
        private const float GuardianHeight = 0.95f;
        private const float ActorDepth = 0f;
        // When the explorer is caught, both stand in one cell: the explorer steps towards the camera and the guardian
        // steps back and aside, so the stone figure is in full view with its captor beside it.
        private const float CaughtDepth = 0.35f;
        private const float CaughtGap = 0.5f;
        // Walk clip playback speed for the explorer; raise it if the feet still lag behind the movement.
        private const float ExplorerWalkPlayback = 1.8f;

        // Blocks are 1 x 0.5 with the pivot at the bottom centre, so a cell holds two of them.
        private const float BlockHeight = 0.5f;
        private const float LadderDepth = 0.1f;
        // Bars hang near the top of the cell, at hand height of a hanging character.
        private const float BarHeight = 0.38f;
        // The painted rope (tools/cutout_props.py): two seamless twisted pieces per cell, a wall bracket with the rope
        // wound round it at each end of a bar. The bracket is drawn at the rope's scale (121 x 80 px against the
        // rope's 38 px thickness) with the rope 61 px below its top, so the rope runs straight into the wrap.
        private const float RopeThickness = 0.17f;
        private const float RopeDepth = 0.02f;
        private const float RopeBracketHeight = RopeThickness * 121f / 38f;
        private const float RopeBracketCentre = (121f - 61f) / 121f;
        private const float RopeBracketWidth = RopeBracketHeight * 80f / 121f;
        // How far the bracket's inner edge reaches into the bar's end cell.
        private const float RopeBracketInset = 0.1f;
        private const float DoorDepth = 0.25f;
        // The painted door from the concept (tools/cut_door.py), standing on the exit cell's floor and rising above
        // it like the concept's tall arched door.
        private const float DoorHeight = 1.7f;
        // The glow is centred on the door and spills well past its edges.
        private const float ExitGlowHeight = 0.85f;
        private const float ExitGlowSize = 3f;
        // Painted props (tools/cutout_props.py), sized in cells.
        private const float GoldWidth = 0.55f;
        private const float GoldRestHeight = 0.04f;
        private const float GoldBobHeight = 0.06f;
        private const float GoldBobSpeed = 2.2f;
        private static readonly Vector2 GoldHaloSize = new Vector2(1.15f, 0.8f);
        private static readonly Color GoldHaloColor = new Color(1f, 0.82f, 0.3f, 0.6f);
        // Just behind the characters, so the explorer passes in front of the bar he picks up.
        private const float GoldDepth = 0.05f;
        private const float LadderWidth = 0.78f;
        private const float HudTopMargin = 0.9f;
        private const int TorchSpacing = 6;
        private const int TorchRowStagger = 3;
        private const int MaxTorchLights = 8;
        // Floor torches on iron stands, as beside the concept's exit door (tools/cut_torch_stand.py). The cavern has
        // hardly any back wall, so the torches stand on the floor, between the painting and the characters.
        private const float TorchStandHeight = 0.85f;
        // The cup's rim is this far up the stand sprite.
        private const float TorchStandCup = 0.8f;
        private const float TorchStandDepth = 0.2f;
        private const float StandFlameSize = 0.5f;
        // The pair beside the exit stands a little away from the door, clear of its frame.
        private const float ExitTorchOffset = 0.25f;

        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly Dictionary<GridPoint, GameObject> goldPieces = new Dictionary<GridPoint, GameObject>();
        // The petrified explorer stays on the level as a statue; the next Render makes a new one at the start.
        // A buried explorer is sealed inside the block and leaves nothing to see.
        public void LeaveStatue(bool keep = true)
        {
            if (player == null)
            {
                return;
            }

            if (!keep)
            {
                Destroy(player.gameObject);
                player = null;
                PlayerLamp = null;
                return;
            }

            player.name = "Statue";
            player.enabled = false;
            statues.Add(player);
            player = null;
            PlayerLamp = null;
        }

        // The explorer and its helmet lamp, for the defeat sequence.
        public CharacterView Player => player;
        public HeadLamp PlayerLamp { get; private set; }

        // The map being drawn, for cells whose look depends on their neighbours (the ends of a rope).
        private TileType[,] currentTiles;
        private readonly Dictionary<GridPoint, float> goldRestY = new Dictionary<GridPoint, float>();
        private readonly Dictionary<GridPoint, SpriteRenderer> goldHalos = new Dictionary<GridPoint, SpriteRenderer>();
        private readonly List<CharacterView> guardians = new List<CharacterView>();
        private GameObject[,] cellObjects;
        private TileType[,] cellTypes;
        private Transform levelRoot;
        private CharacterView player;
        private AltarWarning altarWarning;
        // Seal trial pieces: the plates glow while pressed.
        private readonly List<SpriteRenderer> plates = new List<SpriteRenderer>();
        // Guardian kinds: the red glow of each infected guardian (null for the others); the last noise shown.
        private readonly List<InfectedGlow> infectedGlows = new List<InfectedGlow>();
        private int shownNoiseTick = -1;
        private const float GateHeight = 1.25f;
        private static readonly Color PlateIdle = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color PlatePressed = new Color(1.6f, 1.35f, 0.8f);
        // Following camera (LevelDefinition.view = "follow").
        private const int FollowRows = 13;
        // The translucent controls float directly over the level without artificial bottom dead zone.
        private const float ControlsMargin = 0f;

        private bool Follow => currentDefinition != null && (currentDefinition.FollowCamera || GameSettings.TouchControlsVisible);
        private int ViewRows => GameSettings.TouchControlsVisible ? GameSettings.MobileRows : FollowRows;
        private const float FollowSmoothTime = 0.25f;
        private const float LookAhead = 2.5f;
        private const float FallLookDown = 2.5f;
        // The explorer sits centered in the view.
        private const float FollowAnchor = 0.5f;
        private const float LightRefreshInterval = 0.3f;
        private Vector3 cameraVelocity;
        private bool snapCamera = true;
        private float lastPlayerY;
        private float lookDown;
        private float lightRefreshAt;
        private readonly List<TorchFlame> torchFlames = new List<TorchFlame>();
        // Dark halls (LevelDefinition.dark).
        private DarknessOverlay darkness;
        private readonly List<(Vector3 head, int facing)?> guardianHeads = new List<(Vector3 head, int facing)?>();
        // Between the guardian's eyes, as a share of its height.
        private const float GuardianEyeHeight = 0.8f;
        // Ticks before a guardian's return during which its altar glows (1.5 s at 30 Hz).
        private const int RespawnWarningTicks = 45;
        // Explorers caught earlier in this attempt, left standing as stone statues.
        private readonly List<CharacterView> statues = new List<CharacterView>();
        private LevelDefinition currentDefinition;
        private RuntimeLevelState currentState;
        private CavernBackdrop backdrop;

        public void Render(DungeonSimulation simulation)
        {
            RuntimeLevelState state = simulation.State;
            if (currentDefinition != state.Definition)
            {
                RebuildLevel(state);
            }

            RenderTiles(state);
            RenderGold(state);
            RenderActors(simulation);
            RenderAltarWarning(simulation);
            RenderPlates(state);
            RenderNoise(state);
            PositionCamera(state.Definition);
        }

        // Where the explorer made a noise, a ring spreads, in levels where someone listens.
        private void RenderNoise(RuntimeLevelState state)
        {
            if (state.NoiseTick == shownNoiseTick)
            {
                return;
            }

            bool fresh = state.NoiseTick > shownNoiseTick;
            shownNoiseTick = state.NoiseTick;
            if (!fresh || state.NoiseTick < 0)
            {
                return;
            }

            foreach (GuardianState guardian in state.Guardians)
            {
                if (guardian.Kind == GuardianKind.Listener)
                {
                    NoiseRipple.Spawn(transform, new Vector3(state.NoisePoint.x, state.NoisePoint.y, -0.5f));
                    return;
                }
            }
        }

        private void RenderPlates(RuntimeLevelState state)
        {
            plates.RemoveAll(plate => plate == null);
            foreach (SpriteRenderer plate in plates)
            {
                plate.color = state.PlatePressed ? PlatePressed : PlateIdle;
                // Pressed plates sink a little into the floor.
                Vector3 position = plate.transform.localPosition;
                plate.transform.localPosition = new Vector3(position.x, state.PlatePressed ? -0.04f : 0f, position.z);
            }
        }

        private static SpriteRenderer AddSealSprite(Transform parent, Sprite sprite, Vector3 bottomCentre, Vector2 size)
        {
            var item = new GameObject(sprite.name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = bottomCentre;
            Vector2 native = sprite.bounds.size;
            item.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }

        // A guardian about to return: its altar glows for the last RespawnWarningTicks of the wait, and for as long
        // as the explorer stands too close and holds it back.
        private void RenderAltarWarning(DungeonSimulation simulation)
        {
            if (altarWarning == null)
            {
                altarWarning = AltarWarning.Create(transform);
            }

            foreach (GuardianState guardian in simulation.State.Guardians)
            {
                if (guardian.RespawnTicks > 0 && guardian.RespawnTicks <= RespawnWarningTicks)
                {
                    GridPoint altar = simulation.FindRespawnPoint();
                    altarWarning.Show(new Vector3(altar.x, altar.y, ActorDepth + 0.3f));
                    return;
                }
            }

            altarWarning.Hide();
        }

        private void Update()
        {
            // Gold gently floats so it catches the eye; each bar has its own phase.
            foreach (KeyValuePair<GridPoint, GameObject> gold in goldPieces)
            {
                if (gold.Value.activeSelf)
                {
                    float phase = gold.Key.x * 0.9f + gold.Key.y * 1.7f;
                    float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * GoldBobSpeed + phase);
                    float lift = GoldBobHeight * wave;
                    if (goldHalos.TryGetValue(gold.Key, out SpriteRenderer halo))
                    {
                        // The glow swells as the bar rises.
                        halo.color = new Color(GoldHaloColor.r, GoldHaloColor.g, GoldHaloColor.b, GoldHaloColor.a * (0.7f + 0.3f * wave));
                    }

                    Vector3 position = gold.Value.transform.localPosition;
                    position.y = goldRestY[gold.Key] + lift;
                    gold.Value.transform.localPosition = position;
                }
            }
        }

        private void RebuildLevel(RuntimeLevelState state)
        {
            LevelDefinition definition = state.Definition;
            currentDefinition = definition;

            foreach (CharacterView guardian in guardians)
            {
                Destroy(guardian.gameObject);
            }

            guardians.Clear();
            infectedGlows.Clear();
            torchFlames.Clear();
            plates.Clear();
            snapCamera = true;
            goldPieces.Clear();
            goldRestY.Clear();
            goldHalos.Clear();
            if (levelRoot != null)
            {
                Destroy(levelRoot.gameObject);
            }

            levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(transform, false);
            if (darkness != null)
            {
                Destroy(darkness.gameObject);
                darkness = null;
            }

            if (definition.dark)
            {
                darkness = DarknessOverlay.Create(transform);
            }
            cellObjects = new GameObject[definition.width, definition.height];
            cellTypes = new TileType[definition.width, definition.height];

            foreach (GridPoint point in definition.gold ?? System.Array.Empty<GridPoint>())
            {
                var bottom = new Vector3(point.x, point.y - 0.5f + GoldRestHeight, GoldDepth);
                GameObject gold = SpawnSprite("gold", levelRoot, bottom, GoldWidth, 0f);
                goldPieces[point] = gold;
                goldRestY[point] = gold.transform.localPosition.y;
                goldHalos[point] = AddGoldHalo(gold);
                if (definition.dark)
                {
                    DarknessOverlay.AddGlint(gold);
                }
            }

            BuildDecor(state);
            backdrop = CavernBackdrop.Build(levelRoot, definition.width, definition.height, definition.background);
        }

        private void RenderTiles(RuntimeLevelState state)
        {
            currentTiles = state.Tiles;
            for (int y = 0; y < state.Definition.height; y++)
            {
                for (int x = 0; x < state.Definition.width; x++)
                {
                    TileType tile = state.Tiles[x, y];
                    if (cellObjects[x, y] != null && cellTypes[x, y] == tile)
                    {
                        continue;
                    }

                    // Cells change rarely (digging, hole refill, exit opening), so rebuild only those.
                    if (cellObjects[x, y] != null)
                    {
                        Destroy(cellObjects[x, y]);
                    }

                    // Planks broken by a heavy guardian scatter splinters.
                    if (cellTypes[x, y] == TileType.FragileFloor && tile == TileType.Air && cellObjects[x, y] != null)
                    {
                        SandBurst.Spawn(levelRoot, new Vector3(x, y - 0.5f, 0f), 1f);
                    }

                    cellTypes[x, y] = tile;
                    cellObjects[x, y] = BuildCell(tile, x, y);
                }
            }
        }

        private GameObject BuildCell(TileType tile, int x, int y)
        {
            var cell = new GameObject($"Cell {x},{y} {tile}");
            cell.transform.SetParent(levelRoot, false);
            cell.transform.localPosition = new Vector3(x, y - 0.5f, 0f);
            Transform parent = cell.transform;

            // With the painted cavern, its carved walls frame the level: the side and top border stay solid for
            // gameplay but are not drawn, so the painting shows inside the level as on the concept. The base row stays.
            bool frame = x == 0 || x == currentDefinition.width - 1 || y == currentDefinition.height - 1;
            if (frame && backdrop != null && backdrop.HasPainting)
            {
                return cell;
            }

            switch (tile)
            {
                case TileType.Solid:
                    SpawnLocal("block_solid", parent, Vector3.zero, 1f, new Color(0.3f, 0.32f, 0.36f));
                    SpawnLocal("block_solid", parent, new Vector3(0f, BlockHeight, 0f), 1f, new Color(0.3f, 0.32f, 0.36f));
                    break;
                case TileType.Brick:
                    SpawnLocal("block_diggable", parent, Vector3.zero, 1f, new Color(0.62f, 0.45f, 0.28f));
                    SpawnLocal("block_diggable", parent, new Vector3(0f, BlockHeight, 0f), 1f, new Color(0.62f, 0.45f, 0.28f));
                    break;
                case TileType.Ladder:
                    // One seamless three-rung tile per cell; stacked cells continue the rails.
                    SpawnSprite("ladder", parent, new Vector3(0f, 0f, LadderDepth), LadderWidth, 1f);
                    break;
                case TileType.Bar:
                    BuildRope(parent, x, y);
                    break;
                case TileType.ExitClosed:
                    SpawnSprite("door_closed", parent, new Vector3(0f, 0f, DoorDepth), 0f, DoorHeight);
                    break;
                case TileType.ExitOpen:
                    SpawnSprite("door_open", parent, new Vector3(0f, 0f, DoorDepth), 0f, DoorHeight);

                    // All gold is collected: the door glows. The halo sits behind the door, centred on its middle.
                    ExitGlow.Create(parent, new Vector3(0f, ExitGlowHeight, DoorDepth + 0.15f), ExitGlowSize);
                    break;
                case TileType.Altar:
                    SpawnLocal("altar", parent, new Vector3(0f, 0f, 0.15f), 1f, new Color(0.2f, 0.58f, 0.66f));
                    break;
                case TileType.FragileFloor:
                    // A board floor across the top of the cell, where the walk row above stands on it.
                    AddSealSprite(parent, SealArt.Planks(), new Vector3(0f, 0.62f, 0.02f), new Vector2(1.02f, 0.38f));
                    break;
                case TileType.PressurePlate:
                    plates.Add(AddSealSprite(parent, SealArt.Plate(), new Vector3(0f, 0f, 0.04f), new Vector2(0.9f, 0.12f)));
                    break;
                case TileType.GateClosed:
                    AddSealSprite(parent, SealArt.Gate(), new Vector3(0f, 0f, 0.08f), new Vector2(0.92f, GateHeight));
                    break;
                case TileType.GateOpen:
                    // Raised into the ceiling of the cell: only its spiked lower edge still shows at the top.
                    AddSealSprite(parent, SealArt.Gate(), new Vector3(0f, GateHeight * 0.8f, 0.08f), new Vector2(0.92f, GateHeight));
                    break;
            }

            return cell;
        }

        // Torches on stands on walkable floor on every tier, staggered from row to row, plus a pair beside the exit.
        // Purely decorative: they stand behind the gameplay plane and never on a cell with gold.
        private void BuildDecor(RuntimeLevelState state)
        {
            var torches = new List<Vector3>();
            GridPoint exit = state.Definition.exit;
            if (state.Definition.torches != null && state.Definition.torches.Length > 0)
            {
                // Spots chosen by tools/build_levels.py, each standing on a block that cannot be dug away.
                foreach (GridPoint spot in state.Definition.torches)
                {
                    // The pair beside the exit stands a little away from the door, clear of its frame.
                    float shift = spot.y == exit.y && Mathf.Abs(spot.x - exit.x) == 1 ? (spot.x - exit.x) * ExitTorchOffset : 0f;
                    torches.Add(new Vector3(spot.x + shift, spot.y - 0.5f, 0f));
                }

                SpawnTorches(torches);
                return;
            }

            // Older level files without torch spots: the same rule as the builder, without reinforcing the floor.
            for (int y = 1; y < state.Definition.height - 1; y++)
            {
                for (int x = 1; x < state.Definition.width - 1; x++)
                {
                    // A rope overhead is thin enough to leave room for the flame.
                    TileType above = state.Tiles[x, y + 1];
                    bool floor = state.Tiles[x, y] == TileType.Air && IsBlock(state.Tiles[x, y - 1]) && (above == TileType.Air || above == TileType.Bar)
                        && !state.RemainingGold.Contains(new GridPoint(x, y));
                    if (floor && (x + y * TorchRowStagger) % TorchSpacing == 0)
                    {
                        torches.Add(new Vector3(x, y - 0.5f, 0f));
                    }
                }
            }

            // A pair of torches flanking the exit door, as on the concept screen.
            foreach (int side in new[] { -1, 1 })
            {
                int x = exit.x + side;
                var position = new Vector3(x, exit.y - 0.5f, 0f);
                if (state.Tiles[x, exit.y] == TileType.Air)
                {
                    torches.Remove(position);
                    torches.Add(position + new Vector3(side * ExitTorchOffset, 0f, 0f));
                }
            }

            SpawnTorches(torches);
        }

        private void SpawnTorches(List<Vector3> torches)
        {
            // Real-time lights are costly on phones: light an even spread of torches, the rest only glow. With a
            // following camera every torch gets a light and LateUpdate keeps only those nearest the camera on.
            bool follow = Follow;
            int lightEvery = follow ? 1 : Mathf.Max(1, Mathf.CeilToInt(torches.Count / (float)MaxTorchLights));
            for (int i = 0; i < torches.Count; i++)
            {
                bool lit = i % lightEvery == 0;
                // The painted stand ends at the cup; the animated flame sits on its rim.
                Vector3 floor = torches[i] + new Vector3(0f, 0f, TorchStandDepth);
                SpawnSprite("torch_stand", levelRoot, floor, 0f, TorchStandHeight);
                torchFlames.Add(TorchFlame.Create(levelRoot, floor + new Vector3(0f, TorchStandHeight * TorchStandCup, -0.05f), StandFlameSize, lit));
            }
        }

        private void RenderGold(RuntimeLevelState state)
        {
            foreach (KeyValuePair<GridPoint, GameObject> gold in goldPieces)
            {
                gold.Value.SetActive(state.RemainingGold.Contains(gold.Key));
            }
        }

        private void RenderActors(DungeonSimulation simulation)
        {
            RuntimeLevelState state = simulation.State;
            BalanceConfig balance = simulation.Balance;
            bool newRun = currentState != state;
            currentState = state;
            if (newRun)
            {
                snapCamera = true;
            }

            if (player == null)
            {
                player = CharacterView.Create("explorer", transform, ExplorerHeight, balance.PlayerSpeed, new Color(1f, 0.55f, 0.21f), ExplorerWalkPlayback);
                PlayerLamp = HeadLamp.Attach(player);
            }

            if (newRun)
            {
                foreach (CharacterView statue in statues)
                {
                    Destroy(statue.gameObject);
                }

                statues.Clear();
            }

            if (newRun)
            {
                // A new attempt: the explorer is flesh again (and visible, if it was buried) and the lamp burns.
                player.SetVisible(true);
                player.ClearStone();
                PlayerLamp.SetLevel(1f);
                player.SnapNextMove();
                player.SetOneShotDuration("Dig", balance.DigTicks / balance.TickRate);
            }

            Vector3 playerWorld = ToActorWorld(state.PlayerPosition);
            player.SetTarget(state.Lost ? playerWorld + new Vector3(0f, 0f, -CaughtDepth) : playerWorld);
            if (state.Lost)
            {
                player.SetPose(CharacterPose.Petrify);
            }
            else if (state.PlayerDigTicks > 0)
            {
                player.SetPose(CharacterPose.Dig, state.PlayerDigDirection);
            }
            else
            {
                player.SetPose(MovementPose(simulation, state.PlayerPosition));
            }

            while (guardians.Count < state.Guardians.Count)
            {
                GuardianKind kind = state.Guardians[guardians.Count].Kind;
                CharacterView view = CharacterView.Create("guardian", transform, GuardianHeight * GuardianMarks.HeightScale(kind),
                    balance.GuardianSpeed * GuardianMarks.SpeedScale(kind), new Color(0.23f, 0.78f, 0.86f));
                view.SetTint(GuardianMarks.Tint(kind));
                infectedGlows.Add(kind == GuardianKind.Infected ? InfectedGlow.Attach(view) : null);
                guardians.Add(view);
            }

            for (int i = 0; i < guardians.Count; i++)
            {
                CharacterView view = guardians[i];
                bool active = i < state.Guardians.Count && state.Guardians[i].RespawnTicks <= 0;
                view.SetVisible(active);
                if (!active)
                {
                    continue;
                }

                GuardianState guardian = state.Guardians[i];
                if (newRun)
                {
                    view.SnapNextMove();
                }

                Vector3 guardianWorld = ToActorWorld(guardian.Position);
                bool captor = state.Lost && guardian.Position.y == state.PlayerPosition.y && Mathf.Abs(guardian.Position.x - state.PlayerPosition.x) <= 1;
                if (captor)
                {
                    // Aside on the side it came from.
                    float side = view.transform.position.x >= playerWorld.x ? 1f : -1f;
                    guardianWorld = new Vector3(playerWorld.x + side * CaughtGap, guardianWorld.y, guardianWorld.z + CaughtDepth);
                }

                view.SetTarget(guardianWorld);
                if (infectedGlows[i] != null)
                {
                    infectedGlows[i].SetFlaring(DungeonSimulation.IsAboutToLunge(guardian));
                }
                view.SetPose(guardian.Trapped ? CharacterPose.Struggle : MovementPose(simulation, guardian.Position));
            }
        }

        private static CharacterPose MovementPose(DungeonSimulation simulation, GridPoint position)
        {
            switch (simulation.State.Tiles[position.x, position.y])
            {
                case TileType.Ladder:
                    return CharacterPose.Ladder;
                case TileType.Bar:
                    return CharacterPose.Bar;
            }

            return simulation.HasSupport(position) ? CharacterPose.Ground : CharacterPose.Fall;
        }

        private void PositionCamera(LevelDefinition definition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // Strict side view (TZ section 3). A scene camera may be perspective; that would crop the level
            // and make anything nearer the camera look larger and higher.
            camera.orthographic = true;
            camera.transform.rotation = Quaternion.identity;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.05f, 0.06f);

            if (Follow)
            {
                // The cell size of a 13-row hall (8 rows on a phone); LateUpdate moves the camera after the explorer.
                camera.orthographicSize = (ViewRows + HudTopMargin) / 2f;
                return;
            }

            // The whole level on screen (a PC, where there are no touch controls), with a little room at the top for
            // the HUD text, centered vertically.
            float levelTop = definition.height - 0.5f + HudTopMargin;
            float levelBottom = -0.5f;
            float sizeForHeight = (levelTop - levelBottom) / 2f;
            float sizeForWidth = (definition.width + 0.4f) / (2f * camera.aspect);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            camera.orthographicSize = size;
            float centerY = (levelTop + levelBottom) * 0.5f;
            camera.transform.position = new Vector3((definition.width - 1) * 0.5f, centerY, -10f);
            if (backdrop != null)
            {
                backdrop.FitToView(camera);
            }
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (currentDefinition == null || camera == null)
            {
                return;
            }

            if (Follow && player != null)
            {
                FollowExplorer(camera);
                if (Time.time >= lightRefreshAt)
                {
                    lightRefreshAt = Time.time + LightRefreshInterval;
                    LightNearestTorches(camera.transform.position);
                }
            }

            if (darkness != null)
            {
                UpdateDarkness(camera);
            }
        }

        private void UpdateDarkness(Camera camera)
        {
            Vector3? lamp = null;
            if (player != null && player.gameObject.activeInHierarchy && PlayerLamp != null && PlayerLamp.Level > 0.05f)
            {
                lamp = PlayerLamp.transform.position;
            }

            guardianHeads.Clear();
            foreach (CharacterView view in guardians)
            {
                if (!view.gameObject.activeInHierarchy)
                {
                    guardianHeads.Add(null);
                    continue;
                }

                // Eyes show when the face is towards the camera or in profile, not from behind on a ladder.
                Vector3 forward = view.transform.forward;
                if (forward.z > 0.5f)
                {
                    guardianHeads.Add(null);
                    continue;
                }

                int facing = forward.z < -0.5f ? 0 : (forward.x > 0f ? 1 : -1);
                guardianHeads.Add((view.transform.position + Vector3.up * view.Height * GuardianEyeHeight, facing));
            }

            darkness.UpdateView(camera, lamp, torchFlames, guardianHeads);
        }

        // Keeps the explorer a little below the middle of the playfield, looks ahead in the walking direction and
        // down while falling, and never shows beyond the level's edges.
        private void FollowExplorer(Camera camera)
        {
            LevelDefinition definition = currentDefinition;
            float size = camera.orthographicSize;
            float halfWidth = size * camera.aspect;

            // The playfield is the view below the HUD margin.
            float fieldBottom = -size;
            float fieldTop = size - HudTopMargin;

            Vector3 explorer = player.transform.position;
            float falling = explorer.y < lastPlayerY - 0.001f ? 1f : 0f;
            lastPlayerY = explorer.y;
            lookDown = Mathf.MoveTowards(lookDown, falling * FallLookDown, Time.deltaTime * 6f);
            float facing = player.Facing;

            float x = explorer.x + facing * LookAhead;
            float y = explorer.y - lookDown - (fieldBottom + (fieldTop - fieldBottom) * FollowAnchor);

            float minX = -0.5f + halfWidth;
            float maxX = definition.width - 0.5f - halfWidth;
            x = minX > maxX ? (definition.width - 1) * 0.5f : Mathf.Clamp(x, minX, maxX);
            float minY = -0.5f - fieldBottom;
            float maxY = definition.height - 0.5f - fieldTop;
            y = minY > maxY ? (minY + maxY) * 0.5f : Mathf.Clamp(y, minY, maxY);

            var target = new Vector3(x, y, -10f);
            if (snapCamera)
            {
                snapCamera = false;
                cameraVelocity = Vector3.zero;
                camera.transform.position = target;
            }
            else
            {
                camera.transform.position = Vector3.SmoothDamp(camera.transform.position, target, ref cameraVelocity, FollowSmoothTime);
            }

            if (backdrop != null)
            {
                Vector3 position = camera.transform.position;
                var parallax = new Vector2(
                    minX < maxX ? Mathf.InverseLerp(minX, maxX, position.x) * 2f - 1f : 0f,
                    minY < maxY ? Mathf.InverseLerp(minY, maxY, position.y) * 2f - 1f : 0f);
                backdrop.FitToView(camera, parallax == Vector2.zero ? new Vector2(0.0001f, 0f) : parallax);
            }
        }

        // Only the torches nearest the camera keep a real-time light.
        private void LightNearestTorches(Vector3 centre)
        {
            torchFlames.RemoveAll(flame => flame == null);
            torchFlames.Sort((a, b) =>
                ((Vector2)(a.transform.position - centre)).sqrMagnitude.CompareTo(((Vector2)(b.transform.position - centre)).sqrMagnitude));
            for (int i = 0; i < torchFlames.Count; i++)
            {
                torchFlames[i].SetLightEnabled(i < MaxTorchLights);
            }
        }

        // The rope across a bar cell, with a wall bracket where the bar ends on either side.
        private void BuildRope(Transform parent, int x, int y)
        {
            float centre = 0.5f + BarHeight;
            float bottom = centre - RopeThickness * 0.5f;
            // Slightly wider than half a cell, so neighbouring pieces overlap instead of leaving a hairline gap.
            SpawnSprite("rope", parent, new Vector3(-0.25f, bottom, RopeDepth), 0.505f, RopeThickness);
            SpawnSprite("rope", parent, new Vector3(0.25f, bottom, RopeDepth), 0.505f, RopeThickness);
            float bracketBottom = centre - RopeBracketHeight * RopeBracketCentre;
            foreach (int side in new[] { -1, 1 })
            {
                int neighbour = x + side;
                if (neighbour >= 0 && neighbour < currentTiles.GetLength(0) && currentTiles[neighbour, y] == TileType.Bar)
                {
                    continue;
                }

                float bracketX = side * (0.5f - RopeBracketInset + RopeBracketWidth * 0.5f);
                GameObject bracket = SpawnSprite("rope_bracket", parent, new Vector3(bracketX, bracketBottom, RopeDepth + 0.01f), 0f, RopeBracketHeight);
                if (bracket != null)
                {
                    // The cut-out is the left bracket; the right end of a bar mirrors it.
                    bracket.GetComponent<SpriteRenderer>().flipX = side > 0;
                }
            }
        }

        // A painted cut-out from Resources/Sprites standing on bottomCenter. Give the width or the height (0 keeps the
        // aspect ratio), or both to stretch. Returns null when the sprite is missing.
        private GameObject SpawnSprite(string asset, Transform parent, Vector3 bottomCenter, float width, float height)
        {
            if (!sprites.TryGetValue(asset, out Sprite sprite))
            {
                sprite = Resources.Load<Sprite>($"Sprites/{asset}");
                sprites[asset] = sprite;
            }

            if (sprite == null)
            {
                return null;
            }

            Vector2 size = sprite.bounds.size;
            float scaleX = width > 0f ? width / size.x : height / size.y;
            float scaleY = height > 0f ? height / size.y : scaleX;
            if (width <= 0f)
            {
                scaleX = scaleY;
            }

            var item = new GameObject(asset);
            item.transform.SetParent(parent, false);
            item.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            // Sprites pivot at their centre; lift by half the height so the art stands on bottomCenter.
            item.transform.localPosition = bottomCenter + new Vector3(0f, size.y * scaleY * 0.5f - sprite.bounds.center.y * scaleY, 0f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return item;
        }

        // A warm glow behind a gold bar, as on the concept, so the bars stand out against the dark cavern.
        // It is a child of the bar, so it bobs and disappears with it.
        private static SpriteRenderer AddGoldHalo(GameObject gold)
        {
            var haloObject = new GameObject("Glow");
            haloObject.transform.SetParent(gold.transform, false);
            Vector3 barScale = gold.transform.localScale;
            haloObject.transform.localScale = new Vector3(GoldHaloSize.x / barScale.x, GoldHaloSize.y / barScale.y, 1f);
            haloObject.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            var halo = haloObject.AddComponent<SpriteRenderer>();
            halo.sprite = ExitGlow.GetHaloSprite();
            halo.color = GoldHaloColor;
            return halo;
        }

        private GameObject SpawnLocal(string asset, Transform parent, Vector3 localPosition, float scale, Color fallbackColor)
        {
            GameObject prefab = LoadPrefab(asset);
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab, parent, false);
                instance.transform.localScale = prefab.transform.localScale * scale;
                ModelTextures.Apply(instance, "Environment", asset, true);
            }
            else
            {
                // Keep the game playable if an art file is missing: a cube roughly the size of the piece.
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.transform.SetParent(parent, false);
                instance.transform.localScale = new Vector3(0.9f, BlockHeight, 0.8f) * scale;
                instance.GetComponent<Renderer>().material.color = fallbackColor;
                Destroy(instance.GetComponent<Collider>());
                localPosition += new Vector3(0f, BlockHeight * 0.5f * scale, 0f);
            }

            instance.transform.localPosition = localPosition;
            return instance;
        }

        private GameObject LoadPrefab(string asset)
        {
            if (!prefabs.TryGetValue(asset, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>($"Environment/{asset}");
                if (prefab == null)
                {
                    Debug.LogError($"Environment model Resources/Environment/{asset} not found; using a placeholder cube.");
                }

                prefabs[asset] = prefab;
            }

            return prefab;
        }

        private static bool IsBlock(TileType tile)
        {
            return tile == TileType.Solid || tile == TileType.Brick;
        }

        // Models stand on their origin, so place them at the bottom of the cell.
        private static Vector3 ToActorWorld(GridPoint point)
        {
            return new Vector3(point.x, point.y - 0.5f, ActorDepth);
        }
    }
}
