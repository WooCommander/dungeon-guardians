// Checks that every level can be won (see Assets/Scripts/Core/LevelValidation.cs) and exits with 1 if one cannot.
//   dotnet run -c Release --project tools/LevelCheck [-- --require-replays] [--strict] [--root <repository>]
// --require-replays: a level without a recorded replay fails. --strict: so does one winnable only by losing lives.
// Levels come from Assets/Resources/Levels/*.json, replays from LevelReplays/<id>.txt.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DungeonGuardians.Core;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        bool requireReplays = Array.IndexOf(args, "--require-replays") >= 0;
        bool strict = Array.IndexOf(args, "--strict") >= 0;
        int rootAt = Array.IndexOf(args, "--root");
        string root = rootAt >= 0 && rootAt + 1 < args.Length ? args[rootAt + 1] : FindRoot(Directory.GetCurrentDirectory());
        if (root == null)
        {
            Console.Error.WriteLine("Run from inside the repository, or pass --root <repository>.");
            return 2;
        }

        string levelsDir = Path.Combine(root, "Assets", "Resources", "Levels");
        string replaysDir = Path.Combine(root, "LevelReplays");
        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = false };
        var levels = new List<LevelDefinition>();
        foreach (string file in Directory.GetFiles(levelsDir, "*.json"))
        {
            LevelDefinition level = JsonSerializer.Deserialize<LevelDefinition>(File.ReadAllText(file), options);
            levels.Add(level);
        }

        // In the game's order (LevelCatalog sorts by id).
        levels.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        LevelValidation.Report report = LevelValidation.CheckAll(levels, id =>
        {
            string path = Path.Combine(replaysDir, id + ".txt");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }, new BalanceConfig(), requireReplays, strict);

        Console.Write(report.Text.ToString());
        return report.Passed ? 0 : 1;
    }

    // The repository root: the nearest folder up from here with Assets/Resources/Levels in it.
    private static string FindRoot(string from)
    {
        for (var dir = new DirectoryInfo(from); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Levels")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
