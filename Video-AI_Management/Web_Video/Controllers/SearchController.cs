using Database_Video.Entities;
using DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Net.Http;
using System;
using System.Net.Http.Json;
using Newtonsoft.Json; // Thêm để serialize/deserialize TempData nếu cần

namespace Web_Video.Controllers
{
    public class SearchController : CoreController
    {
        private readonly DataContext _context;

        public SearchController(DataContext context)
        {
            _context = context;
        }

        // ViewModel để chứa kết quả tìm kiếm
        public class SearchViewModel
        {
            public List<Video> Videos { get; set; } = new List<Video>();
            public List<Channel> Channels { get; set; } = new List<Channel>();
        }

        // Xử lý yêu cầu GET: /Search?query=...
        [HttpGet]
        public async Task<IActionResult> Index(string query)
        {
            var viewModel = new SearchViewModel();

            if (string.IsNullOrWhiteSpace(query))
            {
                ViewData["Query"] = "Không có từ khóa tìm kiếm";
                return View(viewModel);
            }

            // Tìm kiếm video
            var videos = await _context.Videos
                .Include(v => v.Category)
                .Include(v => v.Channel)
                .Where(v => (v.Title != null && v.Title.ToLower().Contains(query.ToLower())) ||
                            (v.Channel != null && v.Channel.ChannelName != null && v.Channel.ChannelName.ToLower().Contains(query.ToLower())))
                .ToListAsync();

            // Tìm kiếm kênh
            var channels = await _context.Channels
                .Where(c => c.ChannelName != null && c.ChannelName.ToLower().Contains(query.ToLower()))
                .ToListAsync();

            viewModel.Videos = videos ?? new List<Video>();
            viewModel.Channels = channels ?? new List<Channel>();

            ViewData["Query"] = query;
            Console.WriteLine($"Search GET: Query='{query}', Videos={videos.Count}, Channels={channels.Count}");
            return View(viewModel);
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