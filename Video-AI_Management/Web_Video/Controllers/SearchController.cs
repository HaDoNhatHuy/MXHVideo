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
using Web_Video.ViewModels.Search; // Cần thiết cho việc tính Views

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

        // ViewModel để chứa kết quả tìm kiếm
        public class SearchViewModel
        {
            public List<Video> Videos { get; set; } = new List<Video>();
            public List<Channel> Channels { get; set; } = new List<Channel>();
            // LUÔN CHỨA video đề xuất
            public List<Video> RecommendedVideos { get; set; } = new List<Video>();
        }

        // Helper Class để hứng kết quả JSON từ Python
        public class PythonRecommendResponse
        {
            public string user_id { get; set; }
            public List<Guid> recommendations { get; set; }
        }

        // --- HÀM HELPER LẤY VIDEO ĐỀ XUẤT (TÁI SỬ DỤNG) ---
        private async Task<List<Video>> GetRecommendationsAsync(string userId, Guid? currentVideoId = null)
        {
            const int TIMEOUT_SECONDS = 4; // FIX P3: Tăng Timeout cho độ tin cậy

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
                        // Lấy chi tiết video từ DB
                        var recVideos = await Context.Videos
                            .Include(x => x.Channel)
                            .Include(x => x.Viewers)
                            .Where(x => videoIds.Contains(x.Id))
                            .ToListAsync();

                        // Sắp xếp lại theo thứ tự Python trả về và ánh xạ để có data tính views
                        var orderedVideos = videoIds
                            .Join(recVideos, id => id, v => v.Id, (id, v) => v)
                            .Select(v => new Video // Cần trả về Entity Video để khớp với ViewModel
                            {
                                Id = v.Id,
                                Title = v.Title,
                                Description = v.Description,
                                Thumbnail = v.Thumbnail,
                                UploadDate = v.UploadDate,
                                Duration = v.Duration,
                                // Phải include Channel và Category trước khi truy cập
                                Channel = v.Channel,
                                Category = v.Category,
                                Views = v.Viewers.Select(vv => vv.NumberOfVisit).Sum(), // Tính Views tổng
                            })
                            .Take(12)
                            .ToList();

                        // Chuyển lại về Entity Video (với các trường navigation đã được load)
                        return recVideos.Where(v => orderedVideos.Select(o => o.Id).Contains(v.Id)).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Search Rec Fallback] Lỗi Python/Timeout: {ex.Message}");
                // Fallback: Nếu Python lỗi, lấy Random từ DB
                return await Context.Videos
                    .Include(v => v.Channel)
                    .Include(v => v.Category)
                    .Include(v => v.Viewers)
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

            // Nếu có query — chạy tìm kiếm Exact trước
            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.ToLower();

                // ============================================
                // 1. BẮT ĐẦU BẰNG VIDEO QUERY CƠ BẢN
                // ============================================
                var videoQuery = await Context.Videos
                    .AsNoTracking() // **TỐI ƯU 1: Tắt Entity Tracking** (Quan trọng để tăng tốc độ đọc)
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Include(v => v.Viewers)
                    .Where(v =>
                        (v.Title != null && v.Title.ToLower().Contains(q)) ||
                        (v.Channel != null && v.Channel.ChannelName.ToLower().Contains(q)) ||
                        (v.Description != null && v.Description.ToLower().Contains(q)) ||
                        (v.RecognizedCelebrities != null && v.RecognizedCelebrities.ToLower().Contains(q))
                    ).ToListAsync();

                // ============================================
                // 2. ÁP DỤNG BỘ LỌC TÌM KIẾM NÂNG CAO
                // ============================================
                // A. Lọc thời gian (Upload Time)
                DateTime now = DateTime.UtcNow;
                if (uploadTime != "any")
                {
                    videoQuery = videoQuery.Where(v =>
                    {
                        var diff = now - v.UploadDate;
                        return uploadTime.ToLower() switch
                        {
                            "last_hour" => diff.TotalHours <= 1,
                            "today" => diff.TotalDays <= 1,
                            "this_week" => diff.TotalDays <= 7,
                            "this_month" => diff.TotalDays <= 30,
                            "this_year" => diff.TotalDays <= 365,
                            _ => true
                        };
                    }).ToList();
                }

                // B. Lọc theo thời lượng
                if (duration != "any")
                {
                    videoQuery = videoQuery.Where(v =>
                    {
                        double seconds = ParseDurationToSeconds(v.Duration);
                        return duration.ToLower() switch
                        {
                            "short" => seconds < 240,       // Dưới 4 phút (< 4*60)
                            "medium" => seconds >= 240 && seconds <= 1200, // 4-20 phút
                            "long" => seconds > 1200,       // Trên 20 phút
                            _ => true
                        };
                    }).ToList();
                }

                // Lưu lại filter để View highlight
                ViewData["UploadTime"] = uploadTime.ToLower();
                ViewData["Duration"] = duration.ToLower();

                // ============================================
                // 3. CHỈ LÚC NÀY MỚI THỰC HIỆN QUERY
                // ============================================
                viewModel.Videos = videoQuery
                    .OrderByDescending(v => v.Viewers.Select(vv => vv.NumberOfVisit).Sum())
                    .Take(20)
                    .ToList();

                // Tìm kiếm Channel (gần như giữ nguyên)
                viewModel.Channels = await Context.Channels
                    .Include(c => c.Subscribers)
                    .Where(c => c.ChannelName.ToLower().Contains(q))
                    .Take(5)
                    .ToListAsync();
            }

            // ======================================================================
            // 4. FUZZY SEARCH (nếu exact + filter không tìm thấy gì)
            // ======================================================================
            if (!viewModel.Videos.Any() && viewModel.Channels.Count == 0)
            {
                var candidateVideos = await Context.Videos
                    .Include(v => v.Channel)
                    .OrderByDescending(v => v.UploadDate)
                    .Take(2000)
                    .Select(v => new
                    {
                        v.Id,
                        v.Title,
                        ChannelName = v.Channel.ChannelName,
                        v.UploadDate,
                        v.Description,
                        v.Category.CategoryName,
                        //v.RecognizedCelebrities
                    })
                    .ToListAsync();

                var fuzzyResults = new List<FuzzySearchResult>();

                foreach (var video in candidateVideos)
                {
                    double scoreTitle = FuzzySearchHelper.CalculateFuzzyScore(video.Title, query);
                    double scoreChannel = FuzzySearchHelper.CalculateFuzzyScore(video.ChannelName, query);
                    double scoreDescription = FuzzySearchHelper.CalculateFuzzyScore(video.Description, query);
                    //double scoreCelebrity = FuzzySearchHelper.CalculateFuzzyScore(video.RecognizedCelebrities, query);
                    double scoreCategory = FuzzySearchHelper.CalculateFuzzyScore(video.CategoryName, query);

                    double maxMetadataScore = Math.Max(scoreDescription, scoreCategory);
                    //double finalScore = Math.Max(scoreTitle, scoreChannel);
                    double finalScore = Math.Max(Math.Max(scoreTitle, scoreChannel), maxMetadataScore);

                    if (finalScore >= 60)
                    {
                        fuzzyResults.Add(new FuzzySearchResult
                        {
                            VideoId = video.Id,
                            Score = finalScore
                        });
                    }
                }

                var topFuzzyIds = fuzzyResults
                    .OrderByDescending(r => r.Score)
                    .Take(20)
                    .Select(r => r.VideoId)
                    .ToList();

                if (topFuzzyIds.Any())
                {
                    viewModel.Videos = await Context.Videos
                        .Include(v => v.Category)
                        .Include(v => v.Channel)
                        .Where(v => topFuzzyIds.Contains(v.Id))
                        .ToListAsync();

                    ViewBag.IsFuzzyMatch = true;
                }
            }

            // ======================================================================
            // 5. RECOMMENDATIONS + LOẠI BỎ TRÙNG
            // ======================================================================
            viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);

            if (viewModel.Videos.Any())
            {
                var resultIds = viewModel.Videos.Select(v => v.Id).ToHashSet();
                viewModel.RecommendedVideos = viewModel.RecommendedVideos
                    .Where(v => !resultIds.Contains(v.Id))
                    .Take(12)
                    .ToList();
            }

            ViewBag.IsFallback = (viewModel.Videos.Count == 0 && viewModel.Channels.Count == 0);
            return View(viewModel);
        }
        // Hàm Helper chuyển đổi chuỗi "mm:ss" hoặc "h:mm:ss" sang giây
        private double ParseDurationToSeconds(string durationStr)
        {
            if (string.IsNullOrEmpty(durationStr)) return 0;
            try
            {
                var parts = durationStr.Split(':').Select(double.Parse).ToList();
                if (parts.Count == 3) return parts[0] * 3600 + parts[1] * 60 + parts[2]; // h:mm:ss
                if (parts.Count == 2) return parts[0] * 60 + parts[1]; // mm:ss
                return 0;
            }
            catch { return 0; }
        }
        // --- CẬP NHẬT SearchByImage ---
        [HttpPost]
        public async Task<IActionResult> SearchByImage(IFormFile image)
        {
            var viewModel = new SearchViewModel();
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";
            ViewBag.IsFallback = false;

            // Kiểm tra File Size/Format (giữ nguyên logic kiểm tra)
            if (image == null || image.Length == 0 || image.Length > 5 * 1024 * 1024 || !new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(image.FileName).ToLowerInvariant()))
            {
                ViewData["Query"] = "Hình ảnh không hợp lệ hoặc quá lớn (giới hạn 5MB)";
                ViewBag.IsFallback = true;
                viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);
                return View("Index", viewModel);
            }

            // Chuyển đổi hình ảnh sang base64
            string imageBase64;
            using (var memoryStream = new MemoryStream())
            {
                await image.CopyToAsync(memoryStream);
                imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            List<string> recognizedCelebrities = new List<string>();

            // 1. GỌI PYTHON NHẬN DIỆN (Port 5000)
            try
            {
                var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/"), Timeout = TimeSpan.FromMinutes(5) };
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

            // 2. Xử lý kết quả tìm kiếm chính
            if (recognizedCelebrities.Any())
            {
                var celebIds = await _context.Celebrities
                    .Where(c => recognizedCelebrities.Contains(c.Name))
                    .Select(c => c.Id)
                    .ToListAsync();

                viewModel.Videos = await _context.Videos
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Where(v => v.RecognizeCelebrities.Any(rc => celebIds.Contains(rc.CelebrityId ?? Guid.Empty)))
                    .ToListAsync();

                ViewData["Query"] = $"Tìm kiếm bằng hình ảnh: {string.Join(", ", recognizedCelebrities)}";

                if (!viewModel.Videos.Any())
                {
                    // Trường hợp tìm được celeb nhưng DB không có video nào chứa celeb đó
                    ViewBag.IsFallback = true;
                }
            }
            else if (!ViewBag.IsFallback)
            {
                // Trường hợp Python nhận diện thành công nhưng không tìm thấy khuôn mặt nào
                ViewData["Query"] = "Không nhận diện được người nổi tiếng";
                ViewBag.IsFallback = true;
            }

            // 3. LUÔN LẤY ĐỀ XUẤT (RECOMMENDATION ENGINE)
            viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);

            // 4. LỌC TRÙNG LẶP
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

        // Phương thức LoadFallbackResults không còn cần thiết vì logic đã nằm trong SearchByImage và GetRecommendationsAsync
    }
}