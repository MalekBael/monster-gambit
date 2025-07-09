using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DualEditorApp
{
    public class MonsterInfo
    {
        public string Name { get; set; }
        public string Level { get; set; }
        public string Location { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string ImageUrl { get; set; }
        public string PageUrl { get; set; }

        // Add new fields
        public int BNpcNameId { get; set; }
        public int BNpcBaseId { get; set; }
        public string TerritoryId { get; set; }
    }

    public class BestiaryService
    {
        private readonly MonsterDataLoader _dataLoader;

        public BestiaryService()
        {
            // Initialize the data loader
            _dataLoader = new MonsterDataLoader();
        }

        // This method now uses local CSV data instead of web scraping
        public async Task<List<MonsterInfo>> GetMonstersAsync(IProgress<string> progress = null)
        {
            try
            {
                // Load data from CSV instead of scraping
                return await _dataLoader.LoadMonstersFromCsvAsync(progress);
            }
            catch (Exception ex)
            {
                progress?.Report($"Error loading monster data: {ex.Message}");
                throw;
            }
        }


    }
}