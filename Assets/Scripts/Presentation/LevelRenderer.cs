using System.Collections.Generic;
using DungeonGuardians.Core;
using UnityEngine;

namespace DungeonGuardians.Presentation
{
    public sealed class LevelRenderer : MonoBehaviour
    {
        private readonly List<GameObject> tiles = new List<GameObject>();
        private readonly List<GameObject> actors = new List<GameObject>();
        private GameObject player;
        private GameObject goldRoot;
        private GameObject backdrop;
        private LevelDefinition currentDefinition;

        public void Render(RuntimeLevelState state)
        {
            if (currentDefinition != state.Definition)
            {
                RebuildStaticLevel(state);
            }

            RenderTiles(state);
            RenderGold(state);
            RenderActors(state);
            PositionCamera(state.Definition);
        }

        private void RebuildStaticLevel(RuntimeLevelState state)
        {
            Clear(tiles);
            Clear(actors);
            DestroyIfExists(player);
            DestroyIfExists(goldRoot);
            currentDefinition = state.Definition;

            goldRoot = new GameObject("Gold");
            goldRoot.transform.SetParent(transform, false);

            if (backdrop == null)
            {
                backdrop = CreateSprite("Cavern Backdrop", new Color(0.04f, 0.18f, 0.21f), transform);
                backdrop.GetComponent<SpriteRenderer>().sortingOrder = -10;
            }
        }

        private void RenderTiles(RuntimeLevelState state)
        {
            EnsureCount(tiles, state.Definition.width * state.Definition.height, "Tile");

            for (int y = 0; y < state.Definition.height; y++)
            {
                for (int x = 0; x < state.Definition.width; x++)
                {
                    int index = y * state.Definition.width + x;
                    TileType tile = state.Tiles[x, y];
                    GameObject tileObject = tiles[index];
                    tileObject.transform.position = ToWorld(new GridPoint(x, y), 0f);
                    tileObject.transform.localScale = TileScale(tile);
                    tileObject.SetActive(tile != TileType.Air);
                    tileObject.GetComponent<SpriteRenderer>().color = TileColor(tile);
                }
            }
        }

        private void RenderGold(RuntimeLevelState state)
        {
            foreach (Transform child in goldRoot.transform)
            {
                Destroy(child.gameObject);
            }

            foreach (GridPoint point in state.RemainingGold)
            {
                GameObject gold = CreateSprite("Gold", new Color(1f, 0.78f, 0.14f), goldRoot.transform);
                gold.transform.position = ToWorld(point, -0.25f);
                gold.transform.localScale = new Vector3(0.45f, 0.28f, 1f);
            }
        }

        private void RenderActors(RuntimeLevelState state)
        {
            if (player == null)
            {
                player = CreateSprite("Explorer", new Color(1f, 0.55f, 0.21f), transform);
                player.transform.localScale = new Vector3(0.56f, 0.8f, 1f);
            }

            player.transform.position = ToWorld(state.PlayerPosition, -0.5f);

            EnsureCount(actors, state.Guardians.Count, "Guardian");
            for (int i = 0; i < actors.Count; i++)
            {
                GameObject guardian = actors[i];
                bool active = i < state.Guardians.Count && state.Guardians[i].RespawnTicks <= 0;
                guardian.SetActive(active);
                if (!active)
                {
                    continue;
                }

                guardian.transform.position = ToWorld(state.Guardians[i].Position, -0.45f);
                guardian.transform.localScale = new Vector3(0.64f, 0.82f, 1f);
            }
        }

        private void PositionCamera(LevelDefinition definition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.position = new Vector3((definition.width - 1) * 0.5f, (definition.height - 1) * 0.5f - 0.4f, -10f);
            camera.orthographicSize = Mathf.Max(definition.height * 0.6f, definition.width * 0.32f);

            if (backdrop != null)
            {
                backdrop.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 1f);
                backdrop.transform.localScale = new Vector3(definition.width + 10f, definition.height + 8f, 1f);
            }
        }

        private void EnsureCount(List<GameObject> list, int count, string prefix)
        {
            while (list.Count < count)
            {
                Color color = prefix == "Guardian" ? new Color(0.23f, 0.78f, 0.86f) : Color.white;
                list.Add(CreateSprite($"{prefix} {list.Count}", color, transform));
            }

            for (int i = 0; i < list.Count; i++)
            {
                list[i].SetActive(i < count);
            }
        }

        private static GameObject CreateSprite(string name, Color color, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            renderer.color = color;
            return gameObject;
        }

        private static Vector3 ToWorld(GridPoint point, float z)
        {
            return new Vector3(point.x, point.y, z);
        }

        private static Vector3 TileScale(TileType tile)
        {
            switch (tile)
            {
                case TileType.Ladder:
                    return new Vector3(0.22f, 1f, 1f);
                case TileType.Bar:
                    return new Vector3(1f, 0.16f, 1f);
                case TileType.ExitClosed:
                case TileType.ExitOpen:
                    return new Vector3(0.72f, 0.9f, 1f);
                case TileType.Altar:
                    return new Vector3(0.58f, 0.58f, 1f);
                default:
                    return Vector3.one * 0.96f;
            }
        }

        private static Color TileColor(TileType tile)
        {
            switch (tile)
            {
                case TileType.Solid:
                    return new Color(0.62f, 0.43f, 0.25f);
                case TileType.Brick:
                    return new Color(0.35f, 0.2f, 0.14f);
                case TileType.Ladder:
                    return new Color(0.9f, 0.48f, 0.16f);
                case TileType.Bar:
                    return new Color(0.96f, 0.6f, 0.22f);
                case TileType.ExitClosed:
                    return new Color(0.48f, 0.31f, 0.18f);
                case TileType.ExitOpen:
                    return new Color(0.18f, 0.82f, 0.75f);
                case TileType.Altar:
                    return new Color(0.2f, 0.58f, 0.66f);
                default:
                    return Color.clear;
            }
        }

        private static void Clear(List<GameObject> objects)
        {
            foreach (GameObject item in objects)
            {
                DestroyIfExists(item);
            }

            objects.Clear();
        }

        private static void DestroyIfExists(GameObject item)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
    }
}
