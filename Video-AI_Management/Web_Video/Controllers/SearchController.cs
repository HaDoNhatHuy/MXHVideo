using DataAccess.Data;
using Database_Video.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Web_Video.Extensions;
using WebVideo.Utility;
using Database_Video.DTOs;
using Web_Video.ViewModels.Search;

namespace Web_Video.Controllers
{
    public class SearchController : CoreController
    {
        private readonly DataContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        public SearchController(DataContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        public class SearchViewModel
        {
            public List<Video> Videos { get; set; } = new List<Video>();
            public List<Channel> Channels { get; set; } = new List<Channel>();
            public List<Video> RecommendedVideos { get; set; } = new List<Video>();
        }

        public class PythonRecommendResponse
        {
            public string user_id { get; set; }
            public List<Guid> recommendations { get; set; }
        }

        // --- HELPER: LẤY VIDEO ĐỀ XUẤT ---
        private async Task<List<Video>> GetRecommendationsAsync(string userId, Guid? currentVideoId = null)
        {
            const int TIMEOUT_SECONDS = 4;
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS);
                var payload = new { userId = userId, currentVideoId = currentVideoId };
                var response = await httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend", payload);

                if (response.IsSuccessStatusCode)
                {
                    var apiResult = await response.Content.ReadFromJsonAsync<PythonRecommendResponse>();
                    var videoIds = apiResult.recommendations;

                    if (videoIds != null && videoIds.Any())
                    {
                        var recVideos = await Context.Videos
                            .AsNoTracking()
                            .Include(x => x.Channel)
                            .Include(x => x.Viewers)
                            .Where(x => videoIds.Contains(x.Id))
                            .Select(v => new Video
                            {
                                Id = v.Id,
                                Title = v.Title,
                                Description = v.Description,
                                Thumbnail = v.Thumbnail,
                                UploadDate = v.UploadDate,
                                Duration = v.Duration,
                                Channel = v.Channel,
                                Category = v.Category,
                                Views = v.Viewers.Sum(vv => vv.NumberOfVisit)
                            })
                            .Take(12)
                            .ToListAsync();

                        return recVideos;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Search Rec Fallback] Lỗi Python/Timeout: {ex.Message}");
                return await Context.Videos
                    .AsNoTracking()
                    .Include(v => v.Channel)
                    .Include(v => v.Category)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(12)
                    .ToListAsync();
            }
            return new List<Video>();
        }

        [HttpGet]
        public async Task<IActionResult> Index(string query, string uploadTime = "any", string duration = "any")
        {
            var viewModel = new SearchViewModel();
            ViewData["Query"] = query;
            ViewData["UploadTime"] = uploadTime.ToLower();
            ViewData["Duration"] = duration.ToLower();

            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

            // =============================================
            // PHASE 1: TÌM KIẾM CHÍNH XÁC (EXACT MATCH)
            // =============================================
            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.ToLower().Trim();

                // Tối ưu: Chỉ select các field cần thiết
                var videoQuery = _context.Videos
                    .AsNoTracking()
                    .Where(v =>
                        (v.Title != null && v.Title.ToLower().Contains(q)) ||
                        (v.Channel != null && v.Channel.ChannelName.ToLower().Contains(q)) ||
                        (v.Description != null && v.Description.ToLower().Contains(q)) ||
                        (v.RecognizedCelebrities != null && v.RecognizedCelebrities.ToLower().Contains(q))
                    )
                    .Select(v => new
                    {
                        v.Id,
                        v.Title,
                        v.Description,
                        v.Thumbnail,
                        v.UploadDate,
                        v.Duration,
                        v.ChannelId,
                        ChannelName = v.Channel.ChannelName,
                        ChannelPicture = v.Channel.ChannelPicture,
                        CategoryId = v.Category.Id,
                        CategoryName = v.Category.CategoryName,
                        TotalViews = v.Viewers.Sum(vv => vv.NumberOfVisit)
                    });

                // Áp dụng filter thời gian
                DateTime now = DateTime.UtcNow;
                if (uploadTime != "any")
                {
                    videoQuery = uploadTime.ToLower() switch
                    {
                        "last_hour" => videoQuery.Where(v => (now - v.UploadDate).TotalHours <= 1),
                        "today" => videoQuery.Where(v => (now - v.UploadDate).TotalDays <= 1),
                        "this_week" => videoQuery.Where(v => (now - v.UploadDate).TotalDays <= 7),
                        "this_month" => videoQuery.Where(v => (now - v.UploadDate).TotalDays <= 30),
                        "this_year" => videoQuery.Where(v => (now - v.UploadDate).TotalDays <= 365),
                        _ => videoQuery
                    };
                }

                var exactResults = await videoQuery
                    .OrderByDescending(v => v.TotalViews)
                    .Take(20)
                    .ToListAsync();

                // Áp dụng filter thời lượng (sau khi query)
                if (duration != "any" && exactResults.Any())
                {
                    exactResults = exactResults.Where(v =>
                    {
                        double seconds = ParseDurationToSeconds(v.Duration);
                        return duration.ToLower() switch
                        {
                            "short" => seconds < 240,
                            "medium" => seconds >= 240 && seconds <= 1200,
                            "long" => seconds > 1200,
                            _ => true
                        };
                    }).ToList();
                }

                // Chuyển đổi sang Entity Video
                if (exactResults.Any())
                {
                    var videoIds = exactResults.Select(v => v.Id).ToList();
                    viewModel.Videos = await _context.Videos
                        .AsNoTracking()
                        .Include(v => v.Channel)
                        .Include(v => v.Category)
                        .Where(v => videoIds.Contains(v.Id))
                        .ToListAsync();

                    // Sắp xếp lại theo thứ tự exact results
                    viewModel.Videos = videoIds
                        .Join(viewModel.Videos, id => id, v => v.Id, (id, v) => v)
                        .ToList();
                }

                // Tìm kiếm Channel
                viewModel.Channels = await _context.Channels
                    .AsNoTracking()
                    .Include(c => c.Subscribers)
                    .Where(c => c.ChannelName.ToLower().Contains(q))
                    .Take(5)
                    .ToListAsync();

                // =============================================
                // PHASE 2: FUZZY SEARCH (nếu không tìm thấy)
                // =============================================
                if (!viewModel.Videos.Any() && !viewModel.Channels.Any())
                {
                    var fuzzyResults = await PerformFuzzySearchAsync(query, uploadTime, duration);

                    if (fuzzyResults.Any())
                    {
                        var topFuzzyIds = fuzzyResults
                            .OrderByDescending(r => r.Score)
                            .Take(20)
                            .Select(r => r.VideoId)
                            .ToList();

                        viewModel.Videos = await _context.Videos
                            .AsNoTracking()
                            .Include(v => v.Category)
                            .Include(v => v.Channel)
                            .Where(v => topFuzzyIds.Contains(v.Id))
                            .ToListAsync();

                        // Sắp xếp theo điểm fuzzy
                        viewModel.Videos = topFuzzyIds
                            .Join(viewModel.Videos, id => id, v => v.Id, (id, v) => v)
                            .ToList();

                        ViewBag.IsFuzzyMatch = true;
                    }
                }
            }

            // =============================================
            // PHASE 3: RECOMMENDATIONS + LỌC TRÙNG
            // =============================================
            viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);

            if (viewModel.Videos.Any())
            {
                var resultIds = viewModel.Videos.Select(v => v.Id).ToHashSet();
                viewModel.RecommendedVideos = viewModel.RecommendedVideos
                    .Where(v => !resultIds.Contains(v.Id))
                    .Take(12)
                    .ToList();
            }

            ViewBag.IsFallback = (!viewModel.Videos.Any() && !viewModel.Channels.Any());

            return View(viewModel);
        }

        // --- FUZZY SEARCH RIÊNG (Tối ưu hóa) ---
        private async Task<List<FuzzySearchResult>> PerformFuzzySearchAsync(string query, string uploadTime, string duration)
        {
            // Lấy candidate videos (tối ưu query)
            var candidatesQuery = _context.Videos
                .AsNoTracking()
                .Select(v => new
                {
                    v.Id,
                    v.Title,
                    ChannelName = v.Channel.ChannelName,
                    v.Description,
                    CategoryName = v.Category.CategoryName,
                    v.UploadDate,
                    v.Duration
                })
                .AsQueryable();

            // Áp dụng filter thời gian trước khi lấy về
            DateTime now = DateTime.UtcNow;
            if (uploadTime != "any")
            {
                candidatesQuery = uploadTime.ToLower() switch
                {
                    "last_hour" => candidatesQuery.Where(v => (now - v.UploadDate).TotalHours <= 1),
                    "today" => candidatesQuery.Where(v => (now - v.UploadDate).TotalDays <= 1),
                    "this_week" => candidatesQuery.Where(v => (now - v.UploadDate).TotalDays <= 7),
                    "this_month" => candidatesQuery.Where(v => (now - v.UploadDate).TotalDays <= 30),
                    "this_year" => candidatesQuery.Where(v => (now - v.UploadDate).TotalDays <= 365),
                    _ => candidatesQuery
                };
            }

            var candidates = await candidatesQuery
                .OrderByDescending(v => v.UploadDate)
                .Take(2000)
                .ToListAsync();

            // Fuzzy matching
            var fuzzyResults = new List<FuzzySearchResult>();

            foreach (var video in candidates)
            {
                // Bỏ qua filter duration nếu không match
                if (duration != "any")
                {
                    double seconds = ParseDurationToSeconds(video.Duration);
                    bool durationMatch = duration.ToLower() switch
                    {
                        "short" => seconds < 240,
                        "medium" => seconds >= 240 && seconds <= 1200,
                        "long" => seconds > 1200,
                        _ => true
                    };

                    if (!durationMatch) continue;
                }

                double scoreTitle = FuzzySearchHelper.CalculateFuzzyScore(video.Title, query);
                double scoreChannel = FuzzySearchHelper.CalculateFuzzyScore(video.ChannelName, query);
                double scoreDescription = FuzzySearchHelper.CalculateFuzzyScore(video.Description, query);
                double scoreCategory = FuzzySearchHelper.CalculateFuzzyScore(video.CategoryName, query);

                // Trọng số: Title > Channel > Description > Category
                double finalScore = Math.Max(
                    Math.Max(scoreTitle * 1.0, scoreChannel * 0.8),
                    Math.Max(scoreDescription * 0.6, scoreCategory * 0.5)
                );

                // Ngưỡng fuzzy thấp hơn để bắt nhiều kết quả hơn
                if (finalScore >= 50)
                {
                    fuzzyResults.Add(new FuzzySearchResult
                    {
                        VideoId = video.Id,
                        Title = video.Title,
                        Score = finalScore
                    });
                }
            }

            return fuzzyResults;
        }

        private double ParseDurationToSeconds(string durationStr)
        {
            if (string.IsNullOrEmpty(durationStr)) return 0;
            try
            {
                var parts = durationStr.Split(':').Select(double.Parse).ToList();
                if (parts.Count == 3) return parts[0] * 3600 + parts[1] * 60 + parts[2];
                if (parts.Count == 2) return parts[0] * 60 + parts[1];
                return 0;
            }
            catch { return 0; }
        }

        [HttpPost]
        public async Task<IActionResult> SearchByImage(IFormFile image)
        {
            var viewModel = new SearchViewModel();
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";
            ViewBag.IsFallback = false;

            if (image == null || image.Length == 0 || image.Length > 5 * 1024 * 1024 ||
                !new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(image.FileName).ToLowerInvariant()))
            {
                ViewData["Query"] = "Hình ảnh không hợp lệ hoặc quá lớn (giới hạn 5MB)";
                ViewBag.IsFallback = true;
                viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);
                return View("Index", viewModel);
            }

            string imageBase64;
            using (var memoryStream = new MemoryStream())
            {
                await image.CopyToAsync(memoryStream);
                imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            List<string> recognizedCelebrities = new List<string>();

            try
            {
                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://localhost:5000/"),
                    Timeout = TimeSpan.FromMinutes(5)
                };
                var requestBody = new { image_base64 = imageBase64 };
                var response = await client.PostAsJsonAsync("recognize_image", requestBody);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
                    recognizedCelebrities = (result?["celebrities"] ?? Array.Empty<string>()).ToList();
                }
                else
                {
                    ViewData["Query"] = "Lỗi khi nhận diện hình ảnh (Python API lỗi)";
                    ViewBag.IsFallback = true;
                }
            }
            catch (Exception)
            {
                ViewData["Query"] = "Lỗi khi xử lý tìm kiếm bằng hình ảnh (Timeout)";
                ViewBag.IsFallback = true;
            }

            if (recognizedCelebrities.Any())
            {
                var celebIds = await _context.Celebrities
                    .Where(c => recognizedCelebrities.Contains(c.Name))
                    .Select(c => c.Id)
                    .ToListAsync();

                viewModel.Videos = await _context.Videos
                    .AsNoTracking()
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Where(v => v.RecognizeCelebrities.Any(rc => celebIds.Contains(rc.CelebrityId ?? Guid.Empty)))
                    .ToListAsync();

                ViewData["Query"] = $"Tìm kiếm bằng hình ảnh: {string.Join(", ", recognizedCelebrities)}";

                if (!viewModel.Videos.Any())
                {
                    ViewBag.IsFallback = true;
                }
            }
            else if (!ViewBag.IsFallback)
            {
                ViewData["Query"] = "Không nhận diện được người nổi tiếng";
                ViewBag.IsFallback = true;
            }

            viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);

            if (viewModel.Videos.Any())
            {
                var searchResultIds = viewModel.Videos.Select(v => v.Id).ToHashSet();
                viewModel.RecommendedVideos = viewModel.RecommendedVideos
                    .Where(v => !searchResultIds.Contains(v.Id))
                    .Take(12)
                    .ToList();
            }

            return View("Index", viewModel);
        }
    }
}