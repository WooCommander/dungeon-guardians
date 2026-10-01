using System.IO;
using UnityEngine;

namespace DungeonGuardians.Persistence
{
    public sealed class ProgressStore
    {
        private const string FileName = "progress.json";

        private string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public PlayerProgress Load()
        {
            if (!File.Exists(Path))
            {
                return new PlayerProgress();
            }

            try
            {
                return JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(Path)) ?? new PlayerProgress();
            }
            catch
            {
                return new PlayerProgress();
            }
        }

        public void Save(PlayerProgress progress)
        {
            string tempPath = Path + ".tmp";
            string backupPath = Path + ".bak";
            File.WriteAllText(tempPath, JsonUtility.ToJson(progress, true));

            if (File.Exists(Path))
            {
                File.Copy(Path, backupPath, true);
            }

            if (File.Exists(Path))
            {
                File.Delete(Path);
            }

            File.Move(tempPath, Path);
        }
    }
}
