using System;
using System.Linq;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

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
        private const string BaseUrl = "https://ffxiv.gamerescape.com";
        private const string BestiaryUrl = "https://ffxiv.gamerescape.com/wiki/Category:Bestiary";
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly Random _random = new Random();
        private readonly MonsterDataLoader _dataLoader;

        public BestiaryService()
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

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

        // Keep these methods for potential future use if needed
        public async Task LoadMonsterDetailsIfNeededAsync(MonsterInfo monster, IProgress<string> progress = null)
        {
            // If we're using CSV data, there's not much detail to load
            // But we could still try to fetch additional details from the web if needed
            if (string.IsNullOrEmpty(monster.ImageUrl))
            {
                try
                {
                    await LoadMonsterDetailsAsync(monster, progress);
                }
                catch
                {
                    // If web fetch fails, just continue with the data we have
                }
            }
        }

        private async Task LoadMonsterDetailsAsync(MonsterInfo monster, IProgress<string> progress = null)
        {
            // Placeholder implementation to avoid warning CS1998
            await Task.Delay(1);

            // Implementation for future use - currently just a placeholder
            monster.ImageUrl = $"{BaseUrl}/images/thumb/placeholder.jpg";
        }

        // Helper method for web requests - kept for potential future use
        private async Task<string> GetPageContentAsync(string url, IProgress<string> progress)
        {
            // Placeholder implementation to avoid CS0161 error
            await Task.Delay(1);

            // For now, just return an empty string since this method isn't being used
            return string.Empty;
        }
    }
}