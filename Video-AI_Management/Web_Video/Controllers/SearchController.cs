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
            // THÊM MỚI: Luôn chứa video đề xuất
            public List<Video> RecommendedVideos { get; set; } = new List<Video>();
        }

        // Xử lý yêu cầu GET: /Search?query=...
        // Helper Class để hứng kết quả JSON từ Python
        public class PythonRecommendResponse
        {
            public string user_id { get; set; }
            public List<Guid> recommendations { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> Index(string query)
        {
            var viewModel = new SearchViewModel();
            ViewData["Query"] = query;
            string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";

            // 1. TÌM KIẾM CHÍNH XÁC (SEARCH INTENT)
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Tìm Video khớp tiêu đề hoặc tên kênh
                viewModel.Videos = await Context.Videos
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Where(v => v.Title.Contains(query) || v.Channel.ChannelName.Contains(query))
                    .OrderByDescending(v => v.Views) // Ưu tiên video nhiều view
                    .Take(20) // Giới hạn 20 kết quả tìm kiếm đầu tiên
                    .ToListAsync();

                // Tìm Channel khớp tên
                viewModel.Channels = await Context.Channels
                    .Include(c => c.Subscribers)
                    .Where(c => c.ChannelName.Contains(query))
                    .Take(5)
                    .ToListAsync();
            }

            // 2. LẤY DANH SÁCH ĐỀ XUẤT (RECOMMENDATION ENGINE)
            // Mục tiêu: Lấp đầy trang và gợi ý thêm nội dung
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(2); // Timeout nhanh để không làm chậm trang

                // Gọi Python API (dùng logic tối ưu ở câu trả lời trước)
                var payload = new { userId = userId, currentVideoId = (Guid?)null };
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
                            .Include(x => x.Viewers) // Để tính view nếu cần
                            .Where(x => videoIds.Contains(x.Id))
                            .ToListAsync();

                        // Sắp xếp lại theo thứ tự Python trả về (độ ưu tiên)
                        viewModel.RecommendedVideos = videoIds
                            .Join(recVideos, id => id, v => v.Id, (id, v) => v)
                            .ToList();
                    }
                }
            }
            catch (Exception)
            {
                // Nếu Python lỗi, Fallback về Random
                viewModel.RecommendedVideos = await Context.Videos
                    .Include(v => v.Channel)
                    .OrderBy(x => Guid.NewGuid()) // Random
                    .Take(12)
                    .ToListAsync();
            }

            // 3. LỌC TRÙNG LẶP
            // Loại bỏ những video đã xuất hiện trong kết quả tìm kiếm ra khỏi danh sách đề xuất
            if (viewModel.Videos.Any())
            {
                var searchResultIds = viewModel.Videos.Select(v => v.Id).ToHashSet();
                viewModel.RecommendedVideos = viewModel.RecommendedVideos
                    .Where(v => !searchResultIds.Contains(v.Id))
                    .Take(12) // Lấy 12 video đề xuất
                    .ToList();
            }

            // Nếu không tìm thấy gì cả, và Python cũng tạch -> Đảm bảo không null
            if (viewModel.RecommendedVideos == null) viewModel.RecommendedVideos = new List<Video>();

            // Cờ hiển thị giao diện (nếu tìm không ra thì báo Fallback)
            ViewBag.IsFallback = (viewModel.Videos.Count == 0 && viewModel.Channels.Count == 0);

            return View(viewModel);
        }

        private async Task<IActionResult> LoadFallbackResults(string message)
        {
            ViewData["Query"] = message;
            ViewBag.IsFallback = true; // Cờ quan trọng để JS biết mà load recommend

            // Lấy random video làm đề xuất ban đầu
            var fallbackVideos = await _context.Videos
                .Include(v => v.Channel).Include(v => v.Category)
                .OrderBy(r => Guid.NewGuid())
                .Take(12)
                .ToListAsync();

            return View("Index", new SearchViewModel { Videos = fallbackVideos, Channels = new List<Channel>() });
        }

        [HttpPost]
        public async Task<IActionResult> SearchByImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
            {
                ViewData["Query"] = "Hình ảnh không hợp lệ";
                Console.WriteLine("SearchByImage: Invalid image");
                return View("Index", new SearchViewModel());
            }

            // Kiểm tra kích thước file (giới hạn 5MB)
            if (image.Length > 5 * 1024 * 1024)
            {
                ViewData["Query"] = "Hình ảnh quá lớn (giới hạn 5MB)";
                Console.WriteLine("SearchByImage: Image too large");
                return View("Index", new SearchViewModel());
            }

            // Kiểm tra định dạng ảnh (jpg, png, jpeg)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                ViewData["Query"] = "Định dạng file không được hỗ trợ";
                Console.WriteLine("SearchByImage: Unsupported file format");
                return View("Index", new SearchViewModel());
            }

            // Chuyển đổi hình ảnh sang base64
            string imageBase64;
            using (var memoryStream = new MemoryStream())
            {
                await image.CopyToAsync(memoryStream);
                imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
            }

            // Gọi API Python để nhận diện người nổi tiếng
            try
            {
                var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/"), Timeout = TimeSpan.FromMinutes(5) };
                var requestBody = new { image_base64 = imageBase64 };
                var response = await client.PostAsJsonAsync("recognize_image", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    ViewData["Query"] = "Lỗi khi nhận diện hình ảnh";
                    Console.WriteLine($"SearchByImage: Python API error, status={response.StatusCode}");
                    return View("Index", new SearchViewModel());
                }

                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
                var recognizedCelebrities = result?["celebrities"] ?? new string[0];

                if (recognizedCelebrities.Length == 0)
                {
                    ViewData["Query"] = "Không nhận diện được người nổi tiếng";
                    Console.WriteLine("SearchByImage: No celebrities recognized");
                    return View("Index", new SearchViewModel());
                }

                // Truy vấn video chứa bất kỳ người nổi tiếng nào được nhận diện
                var celebIds = await _context.Celebrities
                    .Where(c => recognizedCelebrities.Contains(c.Name))
                    .Select(c => c.Id)
                    .ToListAsync();

                var videos = await _context.Videos
                    .Include(v => v.Category)
                    .Include(v => v.Channel)
                    .Where(v => v.RecognizeCelebrities.Any(rc => celebIds.Contains(rc.CelebrityId ?? Guid.Empty)))
                    .ToListAsync();

                // Tùy chọn: Truy vấn kênh (nếu kênh liên kết với người nổi tiếng)
                var channels = new List<Channel>(); // Thêm logic nếu cần

                var viewModel = new SearchViewModel
                {
                    Videos = videos ?? new List<Video>(),
                    Channels = channels ?? new List<Channel>()
                };

                ViewData["Query"] = $"Tìm kiếm bằng hình ảnh: {string.Join(", ", recognizedCelebrities)}";
                Console.WriteLine($"SearchByImage: Recognized={string.Join(", ", recognizedCelebrities)}, Videos={videos.Count}, Channels={channels.Count}");
                return View("Index", viewModel);
            }
            catch (Exception ex)
            {
                ViewData["Query"] = "Lỗi khi xử lý tìm kiếm bằng hình ảnh";
                Console.WriteLine($"SearchByImage: Exception - {ex.Message}");
                return View("Index", new SearchViewModel());
            }
        }
    }
}