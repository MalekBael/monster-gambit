using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DualEditorApp
{
    public class MonsterDataLoader
    {
        // Remove readonly keyword to allow modification after construction
        private string _csvFilePath;

        public MonsterDataLoader(string csvFilePath = null)
        {
            if (csvFilePath != null)
            {
                _csvFilePath = csvFilePath;
            }
            else
            {
                // Try several possible locations for the CSV file

                // 1. Try relative to application directory
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // Check the resources folder in the application directory
                string resourcesPath = Path.Combine(appDirectory, "resources", "MonsterData.csv");

                // Check in the root directory of the application
                string rootPath = Path.Combine(appDirectory, "MonsterData.csv");

                // Check in a data_csv subdirectory (from your earlier paths)
                string dataCsvPath = Path.Combine(appDirectory, "data_csv", "CombinedMonsterData.csv");

                // Use the first path that exists
                if (File.Exists(resourcesPath))
                {
                    _csvFilePath = resourcesPath;
                }
                else if (File.Exists(rootPath))
                {
                    _csvFilePath = rootPath;
                }
                else if (File.Exists(dataCsvPath))
                {
                    _csvFilePath = dataCsvPath;
                }
                else
                {
                    // For debugging, check current directory and print it
                    Console.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
                    _csvFilePath = resourcesPath; // Default to resources path even if it doesn't exist yet
                }
            }

            Console.WriteLine($"Using CSV path: {_csvFilePath}");
        }

        public async Task<List<MonsterInfo>> LoadMonstersFromCsvAsync(IProgress<string> progress = null)
        {
            progress?.Report("Loading monsters from CSV file...");

            return await Task.Run(() =>
            {
                var monsters = new List<MonsterInfo>();

                try
                {
                    if (!File.Exists(_csvFilePath))
                    {
                        // Try to look more extensively for the file
                        string fileName = Path.GetFileName(_csvFilePath);
                        string[] possibleLocations = new[]
                        {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", fileName),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                    Path.Combine(Directory.GetCurrentDirectory(), "resources", fileName),
                    Path.Combine(Directory.GetCurrentDirectory(), fileName)
                };

                        string foundPath = possibleLocations.FirstOrDefault(File.Exists);

                        if (foundPath != null)
                        {
                            progress?.Report($"Found file at alternative location: {foundPath}");
                            _csvFilePath = foundPath; // This is now allowed
                        }
                        else
                        {
                            throw new FileNotFoundException(
                                $"Monster data file not found: {_csvFilePath}. " +
                                $"Searched in: {string.Join(", ", possibleLocations)}");
                        }
                    }

                    // Read all lines from CSV
                    string[] lines = File.ReadAllLines(_csvFilePath);

                    // Skip header row
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (i % 100 == 0)
                        {
                            progress?.Report($"Processing monster data {i}/{lines.Length}...");
                        }

                        string line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] values = line.Split(',');
                        if (values.Length < 6) continue;

                        // Make sure the name isn't empty
                        if (string.IsNullOrWhiteSpace(values[0])) continue;

                        // Parse the new ID fields, defaulting to 0 if parsing fails
                        int nameId = 0;
                        int baseId = 0;

                        if (values.Length > 1)
                            int.TryParse(values[1], out nameId);

                        if (values.Length > 2)
                            int.TryParse(values[2], out baseId);

                        string territoryId = values.Length > 3 ? values[3].Trim() : "";
                        string placename = values.Length > 4 ? values[4].Trim() : "";
                        string level = values.Length > 5 ? values[5].Trim() : "";

                        var monster = new MonsterInfo
                        {
                            Name = values[0].Trim(),
                            BNpcNameId = nameId,
                            BNpcBaseId = baseId,
                            TerritoryId = territoryId,
                            Location = placename,
                            Level = level,
                            // Infer monster type from name or set default
                            Type = InferMonsterType(values[0]),
                            // We don't have family in the CSV, leave blank or infer if possible
                            Family = InferMonsterFamily(values[0]),
                            // Set placeholder URLs
                            ImageUrl = "",
                            PageUrl = $"https://ffxiv.gamerescape.com/wiki/{values[0].Replace(" ", "_")}"
                        };

                        monsters.Add(monster);
                    }

                    // Remove duplicates (keeping first occurrence)
                    monsters = monsters
                        .GroupBy(m => m.Name)
                        .Select(g => g.First())
                        .OrderBy(m => m.Name)
                        .ToList();

                    progress?.Report($"Loaded {monsters.Count} unique monsters from CSV");
                    return monsters;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error loading CSV data: {ex.Message}");
                    throw;
                }
            });
        }

        private string InferMonsterType(string monsterName)
        {
            // Converting to lowercase for case-insensitive comparison
            string nameLower = monsterName.ToLowerInvariant();

            if (nameLower.Contains("funguar") || nameLower.Contains("ochu") || nameLower.Contains("treant"))
                return "Plant";
            else if (nameLower.Contains("sprite") || nameLower.Contains("elemental"))
                return "Elemental";
            else if (nameLower.Contains("golem") || nameLower.Contains("sentinel"))
                return "Construct";
            else if (nameLower.Contains("imp") || nameLower.Contains("kalong") || nameLower.Contains("demon"))
                return "Demon";
            else if (nameLower.Contains("boar") || nameLower.Contains("squirrel") || nameLower.Contains("antelope"))
                return "Beast";
            else if (nameLower.Contains("gnat") || nameLower.Contains("mite") || nameLower.Contains("chigoe"))
                return "Insect";
            else
                return "Monster";
        }

        private string InferMonsterFamily(string monsterName)
        {
            // Converting to lowercase for case-insensitive comparison
            string nameLower = monsterName.ToLowerInvariant();

            if (nameLower.Contains("funguar") || nameLower.Contains("toadstool"))
                return "Funguar";
            else if (nameLower.Contains("treant") || nameLower.Contains("sapling"))
                return "Treant";
            else if (nameLower.Contains("sprite"))
                return "Sprite";
            else if (nameLower.Contains("golem"))
                return "Golem";
            else if (nameLower.Contains("boar") || nameLower.Contains("hog"))
                return "Boar";
            else if (nameLower.Contains("gnat") || nameLower.Contains("swarm"))
                return "Gnat";
            else if (nameLower.Contains("opo"))
                return "Opo-opo";
            else if (nameLower.Contains("slug"))
                return "Slug";
            else
                return "";
        }
    }
}