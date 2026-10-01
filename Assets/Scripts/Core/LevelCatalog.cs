using System.Collections.Generic;
using UnityEngine;

namespace DungeonGuardians.Core
{
    public sealed class LevelCatalog
    {
        private readonly List<LevelDefinition> levels = new List<LevelDefinition>();

        public IReadOnlyList<LevelDefinition> Levels => levels;

        public static LevelCatalog LoadFromResources()
        {
            var catalog = new LevelCatalog();
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Levels");
            foreach (TextAsset asset in assets)
            {
                LevelDefinition definition = JsonUtility.FromJson<LevelDefinition>(asset.text);
                LevelParser.Validate(definition);
                catalog.levels.Add(definition);
            }

            catalog.levels.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return catalog;
        }
    }
}
