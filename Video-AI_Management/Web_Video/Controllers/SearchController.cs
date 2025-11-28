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

        //// Xử lý yêu cầu GET: /Search?query=...
        //[HttpGet]
        //public async Task<IActionResult> Index(string query, string uploadTime = "any", string duration = "any")
        //{
        //    var viewModel = new SearchViewModel();
        //    ViewData["Query"] = query;
        //    string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

        //    // 1. TÌM KIẾM CHÍNH XÁC (SEARCH INTENT)
        //    if (!string.IsNullOrWhiteSpace(query))
        //    {
        //        // Chuyển truy vấn về chữ thường để đảm bảo tìm kiếm case-insensitive trên mọi môi trường
        //        string q = query.ToLower();
        //        viewModel.Videos = await Context.Videos
        //            .Include(v => v.Category)
        //            .Include(v => v.Channel)
        //            .Where(v =>
        //                (v.Title != null && v.Title.ToLower().Contains(q)) ||
        //                (v.Channel != null && v.Channel.ChannelName.ToLower().Contains(q)) ||
        //                (v.Description != null && v.Description.ToLower().Contains(q)) ||
        //                (v.RecognizedCelebrities != null && v.RecognizedCelebrities.ToLower().Contains(q))
        //            )
        //            .OrderByDescending(v => v.Viewers.Select(vv => vv.NumberOfVisit).Sum()) // Sắp xếp theo Views tổng
        //            .Take(20)
        //            .ToListAsync();


        //        viewModel.Channels = await Context.Channels
        //            .Include(c => c.Subscribers)
        //            .Where(c => c.ChannelName.Contains(query))
        //            .Take(5)
        //            .ToListAsync();
        //    }
        //    // ----------------------------------------------------
        //    // BƯỚC MỚI: TÌM KIẾM FUZZY (Nếu tìm kiếm chính xác thất bại)
        //    // ----------------------------------------------------
        //    if (!viewModel.Videos.Any() && viewModel.Channels.Count == 0)
        //    {
        //        // 1. Lấy dữ liệu video tối thiểu (ID, Title, ChannelName) để tính Fuzzy Score trên RAM
        //        // Lấy khoảng 200 video gần nhất/phổ biến nhất để tránh quá tải RAM/CPU
        //        // Nếu bạn có ít video (ví dụ < 1000), bạn có thể lấy hết.
        //        var candidateVideos = await Context.Videos
        //            .Include(v => v.Channel)
        //            .OrderByDescending(v => v.UploadDate) // Lấy các video mới nhất để ưu tiên
        //            .Take(2000)
        //            .Select(v => new
        //            {
        //                v.Id,
        //                v.Title,
        //                ChannelName = v.Channel.ChannelName,
        //                v.UploadDate // Giữ lại để sắp xếp khi cần
        //            })
        //            .ToListAsync();

        //        var fuzzyResults = new List<FuzzySearchResult>();

        //        // 2. Tính toán điểm số Fuzzy Search cho từng video
        //        foreach (var video in candidateVideos)
        //        {
        //            double scoreTitle = FuzzySearchHelper.CalculateFuzzyScore(video.Title, query);
        //            double scoreChannel = FuzzySearchHelper.CalculateFuzzyScore(video.ChannelName, query);

        //            // Chọn điểm số cao nhất từ Tiêu đề hoặc Tên kênh
        //            double finalScore = Math.Max(scoreTitle, scoreChannel);

        //            if (finalScore >= 70) // Ngưỡng chấp nhận: 70/100
        //            {
        //                fuzzyResults.Add(new FuzzySearchResult
        //                {
        //                    VideoId = video.Id,
        //                    Score = finalScore
        //                });
        //            }
        //        }

        //        // 3. Lấy 20 video có điểm số cao nhất
        //        var topFuzzyIds = fuzzyResults
        //            .OrderByDescending(r => r.Score)
        //            .Take(20)
        //            .Select(r => r.VideoId)
        //            .ToList();

        //        if (topFuzzyIds.Any())
        //        {
        //            // Lấy lại chi tiết Entity Video từ DB (Hydrate)
        //            viewModel.Videos = await Context.Videos
        //                .Include(v => v.Category)
        //                .Include(v => v.Channel)
        //                .Where(v => topFuzzyIds.Contains(v.Id))
        //                .ToListAsync();

        //            // Đặt cờ để View hiển thị thông báo "Kết quả gần đúng"
        //            ViewBag.IsFuzzyMatch = true;
        //        }
        //    }
        //    // Kết thúc logic Fuzzy Search

        //    // 2. LẤY DANH SÁCH ĐỀ XUẤT (RECOMMENDATION ENGINE)
        //    viewModel.RecommendedVideos = await GetRecommendationsAsync(userId);

        //    // 3. LỌC TRÙNG LẶP
        //    if (viewModel.Videos.Any())
        //    {
        //        var searchResultIds = viewModel.Videos.Select(v => v.Id).ToHashSet();
        //        viewModel.RecommendedVideos = viewModel.RecommendedVideos
        //            .Where(v => !searchResultIds.Contains(v.Id))
        //            .Take(12) // Lấy 12 video đề xuất
        //            .ToList();
        //    }

        //    // Cờ hiển thị giao diện (dùng cho View)
        //    ViewBag.IsFallback = (viewModel.Videos.Count == 0 && viewModel.Channels.Count == 0);
        //    return View(viewModel);
        //}
        [HttpGet]
        public async Task<IActionResult> Index(string query, string uploadTime = "any", string duration = "any")
        {
            var viewModel = new SearchViewModel();
            ViewData["Query"] = query;
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

            // Nếu có query — chạy tìm kiếm Exact trước
            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.ToLower();

                // ============================================
                // 1. BẮT ĐẦU BẰNG VIDEO QUERY CƠ BẢN
                // ============================================
                IQueryable<Video> videoQuery = Context.Videos
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Where(v =>
                        (v.Title != null && v.Title.ToLower().Contains(q)) ||
                        (v.Channel != null && v.Channel.ChannelName.ToLower().Contains(q)) ||
                        (v.Description != null && v.Description.ToLower().Contains(q)) ||
                        (v.RecognizedCelebrities != null && v.RecognizedCelebrities.ToLower().Contains(q))
                    );

                // ============================================
                // 2. ÁP DỤNG BỘ LỌC TÌM KIẾM NÂNG CAO
                // ============================================
                DateTime now = DateTime.UtcNow;

                // A. Lọc theo thời gian upload
                switch (uploadTime.ToLower())
                {
                    case "last_hour":
                        videoQuery = videoQuery.Where(v => v.UploadDate >= now.AddHours(-1));
                        break;
                    case "today":
                        videoQuery = videoQuery.Where(v => v.UploadDate >= now.Date);
                        break;
                    case "this_week":
                        videoQuery = videoQuery.Where(v => v.UploadDate >= now.AddDays(-7));
                        break;
                    case "this_month":
                        videoQuery = videoQuery.Where(v => v.UploadDate >= now.AddMonths(-1));
                        break;
                    case "this_year":
                        videoQuery = videoQuery.Where(v => v.UploadDate >= now.AddYears(-1));
                        break;
                }

                // B. Lọc theo thời lượng
                switch (duration.ToLower())
                {
                    case "short":
                        videoQuery = videoQuery.Where(v =>
                            v.Duration != null && string.Compare(v.Duration, "04:00") < 0
                        );
                        break;

                    case "medium":
                        videoQuery = videoQuery.Where(v =>
                            v.Duration != null &&
                            string.Compare(v.Duration, "04:00") >= 0 &&
                            string.Compare(v.Duration, "20:00") < 0
                        );
                        break;

                    case "long":
                        videoQuery = videoQuery.Where(v =>
                            v.Duration != null && string.Compare(v.Duration, "20:00") >= 0
                        );
                        break;
                }

                // Lưu lại filter để View highlight
                ViewData["UploadTime"] = uploadTime.ToLower();
                ViewData["Duration"] = duration.ToLower();

                // ============================================
                // 3. CHỈ LÚC NÀY MỚI THỰC HIỆN QUERY
                // ============================================
                viewModel.Videos = await videoQuery
                    .OrderByDescending(v => v.Viewers.Select(vv => vv.NumberOfVisit).Sum())
                    .Take(20)
                    .ToListAsync();

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
                        v.UploadDate
                    })
                    .ToListAsync();

                var fuzzyResults = new List<FuzzySearchResult>();

                foreach (var video in candidateVideos)
                {
                    double scoreTitle = FuzzySearchHelper.CalculateFuzzyScore(video.Title, query);
                    double scoreChannel = FuzzySearchHelper.CalculateFuzzyScore(video.ChannelName, query);

                    double finalScore = Math.Max(scoreTitle, scoreChannel);

                    if (finalScore >= 70)
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