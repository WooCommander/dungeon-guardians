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

        // Blocks are 1 x 0.5 with the pivot at the bottom centre, so a cell holds two of them.
        private const float BlockHeight = 0.5f;
        private const float LadderDepth = 0.1f;
        // Bars hang near the top of the cell, at hand height of a hanging character.
        private const float BarHeight = 0.38f;
        // The door model is about 1.5 x 1.9; shrink it to roughly one cell wide.
        private const float DoorScale = 0.62f;
        private const float DoorDepth = 0.25f;
        private const float GoldScale = 1.6f;
        private const float GoldSpinSpeed = 90f;
        private const float BackWallDepth = 0.45f;
        private const int TorchSpacing = 6;
        private const int TorchRowStagger = 3;
        private const int MaxTorchLights = 8;
        // The torch model is about 0.86 tall with its fire bowl at the top; the flame sprite sits on the bowl.
        private const float TorchScale = 0.8f;
        private const float TorchMountHeight = 0.15f;
        private const float TorchFlameHeight = 0.62f;
        private const float TorchFlameSize = 0.38f;

        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private readonly Dictionary<GridPoint, GameObject> goldPieces = new Dictionary<GridPoint, GameObject>();
        private readonly List<CharacterView> guardians = new List<CharacterView>();
        private GameObject[,] cellObjects;
        private TileType[,] cellTypes;
        private Transform levelRoot;
        private CharacterView player;
        private GameObject backdrop;
        private LevelDefinition currentDefinition;
        private RuntimeLevelState currentState;

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
            PositionCamera(state.Definition);
        }

        private void Update()
        {
            float angle = GoldSpinSpeed * Time.deltaTime;
            foreach (GameObject gold in goldPieces.Values)
            {
                if (gold.activeSelf)
                {
                    gold.transform.Rotate(0f, angle, 0f, Space.World);
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
            goldPieces.Clear();
            if (levelRoot != null)
            {
                Destroy(levelRoot.gameObject);
            }

            levelRoot = new GameObject("Level").transform;
            levelRoot.SetParent(transform, false);
            cellObjects = new GameObject[definition.width, definition.height];
            cellTypes = new TileType[definition.width, definition.height];

            foreach (GridPoint point in definition.gold ?? System.Array.Empty<GridPoint>())
            {
                GameObject gold = Spawn("gold", levelRoot, new Vector3(point.x, point.y - 0.5f, ActorDepth), GoldScale, new Color(1f, 0.78f, 0.14f));
                gold.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                goldPieces[point] = gold;
            }

            BuildDecor(state);

            if (backdrop == null)
            {
                backdrop = CreateBackdrop();
            }
        }

        private void RenderTiles(RuntimeLevelState state)
        {
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
                    SpawnLocal("ladder_section", parent, new Vector3(0f, 0f, LadderDepth), 1f, new Color(0.55f, 0.35f, 0.18f));
                    break;
                case TileType.Bar:
                    SpawnLocal("rope_section", parent, new Vector3(0f, 0.5f + BarHeight, 0f), 1f, new Color(0.7f, 0.55f, 0.3f));
                    break;
                case TileType.ExitClosed:
                    SpawnLocal("door_closed", parent, new Vector3(0f, 0f, DoorDepth), DoorScale, new Color(0.35f, 0.22f, 0.12f));
                    break;
                case TileType.ExitOpen:
                    SpawnLocal("door_open", parent, new Vector3(0f, 0f, DoorDepth), DoorScale, new Color(0.18f, 0.82f, 0.75f));
                    break;
                case TileType.Altar:
                    SpawnLocal("altar", parent, new Vector3(0f, 0f, 0.15f), 1f, new Color(0.2f, 0.58f, 0.66f));
                    break;
            }

            return cell;
        }

        // Torches on the back wall above walkable floor on every tier, staggered from row to row.
        // Purely decorative: they sit behind the gameplay plane and never cover a cell's contents.
        private void BuildDecor(RuntimeLevelState state)
        {
            var torches = new List<Vector3>();
            for (int y = 1; y < state.Definition.height - 1; y++)
            {
                for (int x = 1; x < state.Definition.width - 1; x++)
                {
                    bool floor = state.Tiles[x, y] == TileType.Air && IsBlock(state.Tiles[x, y - 1]) && state.Tiles[x, y + 1] == TileType.Air;
                    if (floor && (x + y * TorchRowStagger) % TorchSpacing == 0)
                    {
                        torches.Add(new Vector3(x, y - 0.5f + TorchMountHeight, BackWallDepth));
                    }
                }
            }

            // Real-time lights are costly on phones: light an even spread of torches, the rest only glow.
            int lightEvery = Mathf.Max(1, Mathf.CeilToInt(torches.Count / (float)MaxTorchLights));
            for (int i = 0; i < torches.Count; i++)
            {
                Spawn("torch", levelRoot, torches[i], TorchScale, new Color(1f, 0.6f, 0.2f));
                Vector3 flame = torches[i] + new Vector3(0f, TorchFlameHeight, -0.12f);
                TorchFlame.Create(levelRoot, flame, TorchFlameSize, i % lightEvery == 0);
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

            if (player == null)
            {
                player = CharacterView.Create("explorer", transform, ExplorerHeight, balance.PlayerSpeed, new Color(1f, 0.55f, 0.21f));
            }

            if (newRun)
            {
                player.SnapNextMove();
                player.SetOneShotDuration("Dig", balance.DigTicks / balance.TickRate);
            }

            player.SetTarget(ToActorWorld(state.PlayerPosition));
            if (state.Lost)
            {
                player.SetPose(CharacterPose.Dead);
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
                guardians.Add(CharacterView.Create("guardian", transform, GuardianHeight, balance.GuardianSpeed, new Color(0.23f, 0.78f, 0.86f)));
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

                view.SetTarget(ToActorWorld(guardian.Position));
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
            camera.transform.position = new Vector3((definition.width - 1) * 0.5f, (definition.height - 1) * 0.5f - 0.4f, -10f);
            camera.orthographicSize = Mathf.Max(definition.height * 0.6f, definition.width * 0.32f);

            if (backdrop != null)
            {
                backdrop.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 2f);
                backdrop.transform.localScale = new Vector3(definition.width + 10f, definition.height + 8f, 1f);
            }
        }

        private GameObject Spawn(string asset, Transform parent, Vector3 localPosition, float scale, Color fallbackColor)
        {
            GameObject instance = SpawnLocal(asset, parent, localPosition, scale, fallbackColor);
            instance.name = asset;
            return instance;
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

        private GameObject CreateBackdrop()
        {
            var backdropObject = new GameObject("Cavern Backdrop");
            backdropObject.transform.SetParent(transform, false);
            var renderer = backdropObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.color = new Color(0.05f, 0.1f, 0.12f);
            renderer.sortingOrder = -10;
            return backdropObject;
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
