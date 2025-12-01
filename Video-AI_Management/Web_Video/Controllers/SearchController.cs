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
using Microsoft.Extensions.Caching.Memory; // Cần thiết cho việc tính Views

namespace Web_Video.Controllers
{
    public class SearchController : CoreController
    {
        private readonly DataContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache; // Inject Cache

        public SearchController(DataContext context, IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
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
        // [SỬA ĐỔI] Thêm tham số List<Guid> excludeIds
        private async Task<List<Video>> GetRecommendationsAsync(string userId, List<Guid> excludeIds, Guid? currentVideoId = null)
        {
            const int TIMEOUT_SECONDS = 3; // Giảm timeout xuống 3s để không treo user
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS);

                var payload = new
                {
                    userId = userId,
                    currentVideoId = currentVideoId,
                    excludeIds = excludeIds ?? new List<Guid>()
                };

                // Gọi Python
                var response = await httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend", payload);

                if (response.IsSuccessStatusCode)
                {
                    var apiResult = await response.Content.ReadFromJsonAsync<PythonRecommendResponse>();
                    var videoIds = apiResult.recommendations;

                    if (videoIds != null && videoIds.Any())
                    {
                        // [TỐI ƯU CỰC MẠNH TẠI ĐÂY]
                        // 1. Chỉ lấy những video có ID trả về
                        // 2. KHÔNG BAO GIỜ Include(Viewers)
                        // 3. Dùng AsNoTracking()
                        var recVideos = await _context.Videos
                            .AsNoTracking()
                            .Include(x => x.Channel)
                            .Include(x => x.Category)
                            .Where(x => videoIds.Contains(x.Id))
                            .ToListAsync();

                        // Sắp xếp lại theo thứ tự Python trả về (giữ nguyên logic của bạn)
                        var orderedVideos = videoIds
                            .Join(recVideos, id => id, v => v.Id, (id, v) => v)
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
                                // [QUAN TRỌNG] Lấy từ cột CachedViews, KHÔNG tính tổng lại
                                Views = v.CachedViews
                            })
                            .ToList();

                        // Loại bỏ những video đã có trong danh sách hiển thị
                        return recVideos.Where(v => orderedVideos.Select(o => o.Id).Contains(v.Id)).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log lỗi nhẹ, không làm phiền user
                Console.WriteLine($"[Recommendation Skip] {ex.Message}");
            }

            // Fallback: Nếu lỗi Python, trả về rỗng để code chính tự xử lý (hoặc lấy random nhẹ)
            return new List<Video>();
        }

        [HttpGet]
        public async Task<IActionResult> Index(string query, string uploadTime = "any", string duration = "any")
        {
            // --- 1. CHUẨN BỊ DỮ LIỆU VIEW ---
            ViewData["Query"] = query;
            ViewData["UploadTime"] = uploadTime?.ToLower() ?? "any";
            ViewData["Duration"] = duration?.ToLower() ?? "any";
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

            // --- 2. KIỂM TRA CACHE (TỐI ƯU TỐC ĐỘ SỐ 1) ---
            // Tạo key cache duy nhất dựa trên query và bộ lọc
            string cacheKey = $"search_{query?.Trim().ToLower()}_{uploadTime}_{duration}";

            // Nếu đã có trong RAM, trả về ngay lập tức (0 giây)
            if (_cache.TryGetValue(cacheKey, out SearchViewModel cachedModel))
            {
                // Vẫn phải lấy recommendation riêng cho từng user (không cache phần này chung)
                if (User.Identity.IsAuthenticated)
                {
                    var cachedIds = cachedModel.Videos.Select(v => v.Id).ToList();
                    cachedModel.RecommendedVideos = await GetRecommendationsAsync(userId, cachedIds);
                }
                return View(cachedModel);
            }

            var viewModel = new SearchViewModel();

            // --- 3. XÂY DỰNG QUERY (KHÔNG LOAD DỮ LIỆU NGAY) ---
            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.ToLower().Trim();

                // Khởi tạo Query - QUAN TRỌNG: Không dùng Include(Viewers)
                var videoQuery = _context.Videos
                    .AsNoTracking() // Tắt theo dõi để đọc nhanh hơn
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .AsQueryable();

                // A. Lọc theo từ khóa
                videoQuery = videoQuery.Where(v =>
                    (v.Title != null && v.Title.ToLower().Contains(q)) ||
                    (v.Channel != null && v.Channel.ChannelName.ToLower().Contains(q)) ||
                    (v.Description != null && v.Description.ToLower().Contains(q)) ||
                    (v.RecognizedCelebrities != null && v.RecognizedCelebrities.ToLower().Contains(q))
                );

                // B. Lọc thời gian (Tính toán mốc thời gian trước khi đưa vào câu lệnh SQL)
                if (uploadTime != "any")
                {
                    DateTime now = DateTime.UtcNow;
                    DateTime cutoffDate = DateTime.MinValue;

                    switch (uploadTime.ToLower())
                    {
                        case "last_hour": cutoffDate = now.AddHours(-1); break;
                        case "today": cutoffDate = now.AddDays(-1); break;
                        case "this_week": cutoffDate = now.AddDays(-7); break;
                        case "this_month": cutoffDate = now.AddDays(-30); break;
                        case "this_year": cutoffDate = now.AddDays(-365); break;
                    }

                    if (cutoffDate > DateTime.MinValue)
                    {
                        videoQuery = videoQuery.Where(v => v.UploadDate >= cutoffDate);
                    }
                }

                // C. Lọc thời lượng (Dùng cột DurationSeconds đã tính sẵn trong DB)
                // Lưu ý: Nếu bạn chưa có cột DurationSeconds trong DB, đoạn này sẽ lỗi.
                if (duration != "any")
                {
                    switch (duration.ToLower())
                    {
                        case "short": // < 4 phút
                            videoQuery = videoQuery.Where(v => v.DurationSeconds < 240);
                            break;
                        case "medium": // 4 - 20 phút
                            videoQuery = videoQuery.Where(v => v.DurationSeconds >= 240 && v.DurationSeconds <= 1200);
                            break;
                        case "long": // > 20 phút
                            videoQuery = videoQuery.Where(v => v.DurationSeconds > 1200);
                            break;
                    }
                }

                // --- 4. THỰC THI QUERY ---
                // Sắp xếp theo CachedViews (đã tính ngầm) thay vì tính Sum trực tiếp
                viewModel.Videos = await videoQuery
                    .OrderByDescending(v => v.CachedViews)
                    .Take(50) // Giới hạn 50 kết quả đầu tiên để load nhanh
                    .ToListAsync();

                // Lấy Channel (giữ nguyên logic cũ nhưng thêm AsNoTracking)
                viewModel.Channels = await _context.Channels
                    .AsNoTracking()
                    .Include(c => c.Subscribers)
                    .Where(c => c.ChannelName.ToLower().Contains(q))
                    .Take(5)
                    .ToListAsync();
            }

            // --- 5. FUZZY SEARCH (CHỈ CHẠY KHI KHÔNG TÌM THẤY GÌ) ---
            if ((viewModel.Videos == null || !viewModel.Videos.Any()) &&
    (viewModel.Channels == null || !viewModel.Channels.Any()) &&
    !string.IsNullOrWhiteSpace(query))
            {
                // [TỐI ƯU]
                // 1. Chỉ lấy 500 video mới nhất thay vì 2000 (DB lớn lấy 2000 rất chậm)
                // 2. Chỉ Select đúng các cột cần thiết để so sánh chuỗi (Projection)
                var candidateVideos = await _context.Videos
                    .AsNoTracking()
                    .OrderByDescending(v => v.UploadDate)
                    .Take(500) // Giảm xuống 500
                    .Select(v => new
                    {
                        v.Id,
                        v.Title,
                        // Lưu ý: Lấy thẳng ChannelName nếu có thể, tránh join nếu không cần thiết
                        // Nếu buộc phải join:
                        ChannelName = v.Channel.ChannelName,
                        v.Description,
                        CategoryName = v.Category.CategoryName
                    })
                    .ToListAsync();

                var fuzzyResults = new List<Guid>();

                // Logic tính điểm Fuzzy (xử lý trên RAM -> Cực nhanh với 500 items)
                foreach (var item in candidateVideos)
                {
                    double scoreTitle = FuzzySearchHelper.CalculateFuzzyScore(item.Title, query);
                    double scoreChannel = FuzzySearchHelper.CalculateFuzzyScore(item.ChannelName, query);
                    // Có thể bỏ qua description nếu muốn nhanh hơn nữa

                    if (Math.Max(scoreTitle, scoreChannel) >= 60)
                    {
                        fuzzyResults.Add(item.Id);
                    }
                }

                if (fuzzyResults.Any())
                {
                    // Lấy thông tin chi tiết của các video tìm được
                    viewModel.Videos = await _context.Videos
                        .AsNoTracking()
                        .Include(v => v.Category)
                        .Include(v => v.Channel)
                        .Where(v => fuzzyResults.Take(20).Contains(v.Id)) // Chỉ lấy top 20
                        .ToListAsync();

                    // Gán Views từ Cache
                    foreach (var v in viewModel.Videos) v.Views = v.CachedViews;

                    ViewBag.IsFuzzyMatch = true;
                }
            }

            // --- 6. LƯU KẾT QUẢ VÀO CACHE ---
            // Chỉ cache phần kết quả tìm kiếm (Videos + Channels), không cache Recommendations
            if (viewModel.Videos.Any() || viewModel.Channels.Any())
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)) // Cache tồn tại 10 phút
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));  // Xóa nếu 2 phút không ai truy cập

                _cache.Set(cacheKey, viewModel, cacheOptions);
            }

            // --- 7. RECOMMENDATIONS (LUÔN LẤY MỚI CHO TỪNG USER) ---
            var existingIds = viewModel.Videos.Select(v => v.Id).ToList();
            viewModel.RecommendedVideos = await GetRecommendationsAsync(userId, existingIds);

            // Lọc trùng lần cuối
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
        // Class DTO để nhận dữ liệu từ Client
        public class LoadMoreRequest
        {
            public List<Guid> ExcludeIds { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> GetMoreRecommendations([FromBody] LoadMoreRequest request)
        {
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

            // Gọi hàm helper với danh sách ID cần loại bỏ
            var videos = await GetRecommendationsAsync(userId, request.ExcludeIds);

            // Chuyển đổi sang DTO nhẹ để trả về JSON cho Client
            var result = videos.Select(v => new
            {
                id = v.Id,
                title = v.Title,
                thumbnail = v.Thumbnail ?? "/img/default-thumbnail.jpg",
                duration = v.Duration ?? "0:00",
                channelName = v.Channel?.ChannelName ?? "Unknown",
                channelPicture = v.Channel?.ChannelPicture ?? "/avatarUser/avt-default.jpg",
                channelId = v.Channel?.Id,
                views = v.Views ?? 0, // Đã tính sum ở Helper
                createdAtTimeAgo = SD.TimeAgo(v.UploadDate)
            }).ToList();

            return Json(new { isSuccess = true, data = result });
        }

        // Phương thức LoadFallbackResults không còn cần thiết vì logic đã nằm trong SearchByImage và GetRecommendationsAsync
    }
}