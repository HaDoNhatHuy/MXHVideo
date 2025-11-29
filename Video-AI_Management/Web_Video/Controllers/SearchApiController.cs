using DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchApiController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly IMemoryCache _cache;

        public SearchApiController(DataContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions(string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<string>());

            string cacheKey = $"search_suggestions_{q.ToLower()}";

            // Kiểm tra cache trước
            if (_cache.TryGetValue(cacheKey, out List<string> cachedResults))
            {
                return Ok(cachedResults);
            }

            var suggestions = new List<SuggestionItem>();

            // 1. Tìm exact match trước (ưu tiên cao)
            var exactVideoTitles = await _context.Videos
                .AsNoTracking()
                .Where(v => v.Title != null && v.Title.ToLower().StartsWith(q.ToLower()))
                .Select(v => new SuggestionItem
                {
                    Text = v.Title,
                    Score = 100,
                    Type = "video"
                })
                .Distinct()
                .Take(5)
                .ToListAsync();

            var exactChannels = await _context.Channels
                .AsNoTracking()
                .Where(c => c.ChannelName != null && c.ChannelName.ToLower().StartsWith(q.ToLower()))
                .Select(c => new SuggestionItem
                {
                    Text = c.ChannelName,
                    Score = 100,
                    Type = "channel"
                })
                .Distinct()
                .Take(5)
                .ToListAsync();

            suggestions.AddRange(exactVideoTitles);
            suggestions.AddRange(exactChannels);

            // 2. Nếu chưa đủ 10 kết quả -> tìm contains
            if (suggestions.Count < 10)
            {
                var remainingCount = 10 - suggestions.Count;

                var containsVideoTitles = await _context.Videos
                    .AsNoTracking()
                    .Where(v => v.Title != null &&
                               v.Title.ToLower().Contains(q.ToLower()) &&
                               !v.Title.ToLower().StartsWith(q.ToLower()))
                    .Select(v => new SuggestionItem
                    {
                        Text = v.Title,
                        Score = 80,
                        Type = "video"
                    })
                    .Distinct()
                    .Take(remainingCount)
                    .ToListAsync();

                suggestions.AddRange(containsVideoTitles);
            }

            // 3. Nếu vẫn chưa đủ -> fuzzy search
            if (suggestions.Count < 10)
            {
                var remainingCount = 10 - suggestions.Count;
                var fuzzySuggestions = await GetFuzzySuggestionsAsync(q, remainingCount);
                suggestions.AddRange(fuzzySuggestions);
            }

            // 4. Sắp xếp theo score và loại bỏ trùng lặp
            var results = suggestions
                .GroupBy(s => s.Text.ToLower())
                .Select(g => g.OrderByDescending(s => s.Score).First())
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Text.Length)
                .Select(s => s.Text)
                .Take(10)
                .ToList();

            // Cache kết quả 5 phút
            _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));

            return Ok(results);
        }

        private async Task<List<SuggestionItem>> GetFuzzySuggestionsAsync(string query, int count)
        {
            // Lấy candidates từ DB
            var candidates = await _context.Videos
                .AsNoTracking()
                .Select(v => v.Title)
                .Union(_context.Channels.AsNoTracking().Select(c => c.ChannelName))
                .Where(t => t != null)
                .Take(500)
                .ToListAsync();

            var fuzzyResults = new List<SuggestionItem>();

            foreach (var candidate in candidates)
            {
                double score = FuzzySearchHelper.CalculateFuzzyScore(candidate, query);

                if (score >= 60)
                {
                    fuzzyResults.Add(new SuggestionItem
                    {
                        Text = candidate,
                        Score = score,
                        Type = "fuzzy"
                    });
                }
            }

            return fuzzyResults
                .OrderByDescending(r => r.Score)
                .Take(count)
                .ToList();
        }

        private class SuggestionItem
        {
            public string Text { get; set; }
            public double Score { get; set; }
            public string Type { get; set; }
        }
    }
}