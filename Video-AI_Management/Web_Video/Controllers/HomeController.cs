using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Home;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    public class HomeController : CoreController
    {
        private readonly ILogger<HomeController> _logger;
        private IHttpClientFactory _httpClientFactory;
        public HomeController(DataContext context, ILogger<HomeController> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> Index()
        {
            var toReturn = new HomeViewModel();
            if (User.Identity.IsAuthenticated)
            {
                var allCategories = await UnitOfWork.CategoryRepo.GetAllAsync();
                var categoryList = allCategories.Select(category => new SelectListItem
                {
                    Text = category.CategoryName,
                    Value = category.Id.ToString()
                }).ToList();

                categoryList.Insert(0, new SelectListItem
                {
                    Text = "All",
                    Value = "0",
                    Selected = true
                });
                toReturn.CategoryDropdown = categoryList;
            }
            return View(toReturn);
        }

        public IActionResult History()
        {
            return View(new HomeViewModel());
        }

        public IActionResult Liked()
        {
            return View(new HomeViewModel());
        }

        public IActionResult Subscriptions()
        {
            return View(new HomeViewModel());
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #region API Endpoints
        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpGet]
        public async Task<IActionResult> GetVideosForHomeGrid(HomeParameters parameters)
        {
            // NẾU CÓ TÌM KIẾM HOẶC CHỌN CATEGORY -> Dùng logic cũ (filter)
            if (!string.IsNullOrEmpty(parameters.SearchBy) || parameters.CategoryId != Guid.Empty)
            {
                var items = await UnitOfWork.VideoRepo.GetVideosForHomeGridAsync(parameters);
                return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(items, items.TotalItemsCount, items.PageNumber, items.PageSize, items.TotalPages)));
            }

            // NẾU LÀ TRANG CHỦ MẶC ĐỊNH -> GỌI AI PYTHON ĐỂ CÁ NHÂN HÓA
            try
            {
                string userId = User.GetUserId();
                var httpClient = _httpClientFactory.CreateClient(); // Cần inject IHttpClientFactory vào HomeController

                // Gọi Python API: Chỉ gửi UserID, không gửi currentVideoId (vì đang ở Home)
                var payload = new { userId = userId, currentVideoId = (Guid?)null };
                var response = await httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend", payload);

                if (response.IsSuccessStatusCode)
                {
                    var apiResult = await response.Content.ReadFromJsonAsync<Web_Video.Controllers.VideoController.PythonRecommendResponse>();
                    var videoIds = apiResult.recommendations;

                    if (videoIds != null && videoIds.Any())
                    {
                        // Query DB lấy video theo danh sách ID từ Python trả về
                        var personalizedVideos = await Context.Videos
                            .Include(x => x.Channel)
                            .Include(x => x.Category)
                            .Where(x => videoIds.Contains(x.Id))
                            .Select(x => new VideoForHomeGridDto
                            {
                                Id = x.Id,
                                Thumbnail = x.Thumbnail,
                                Duration = x.Duration ?? "0:00",
                                Title = x.Title,
                                Description = x.Description,
                                CreatedAt = x.UploadDate,
                                ChannelName = x.Channel.ChannelName,
                                ChannelId = x.Channel.Id,
                                CategoryId = x.Category.Id,
                                Views = x.Viewers.Sum(v => v.NumberOfVisit), // Sửa lại cách tính view
                                CreatedAtTimeAgo = SD.TimeAgo(x.UploadDate)
                            })
                            .ToListAsync();

                        // Sắp xếp lại theo thứ tự Python trả về (quan trọng để giữ độ ưu tiên)
                        personalizedVideos = personalizedVideos
                            .OrderBy(v => videoIds.IndexOf(v.Id))
                            .ToList();

                        // Chuyển đổi sang PaginatedResult (Giả lập trang 1, full size vì đây là list gợi ý)
                        var result = new PaginatedResult<VideoForHomeGridDto>(personalizedVideos, personalizedVideos.Count, 1, parameters.PageSize, 1);
                        return Json(new ApiResponse(200, result: result));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi AI Home Recommendation: {ex.Message}");
                // Fallback về logic cũ nếu AI lỗi
            }

            // FALLBACK: Nếu AI lỗi hoặc chưa có data, dùng logic cũ (Lấy mới nhất)
            var defaultItems = await UnitOfWork.VideoRepo.GetVideosForHomeGridAsync(parameters);
            return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(defaultItems, defaultItems.TotalItemsCount, defaultItems.PageNumber, defaultItems.PageSize, defaultItems.TotalPages)));
        }
        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpGet]
        public async Task<IActionResult> GetHistory(int pageNumber = 1, int pageSize = 12)
        {
            var query = Context.VideoViews
                .Where(x => x.AppUserId == User.GetUserId())
                .OrderByDescending(x => x.LastVisit)  // Sắp xếp mới nhất đầu tiên
                .Select(x => new
                {
                    VideoViewId = x.Id,  // Để xóa entry cụ thể
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    LastVisitTimeAgo = SD.TimeAgo(x.LastVisit),
                    LastVisit = x.LastVisit,
                    GroupName = GetDateGroupName(x.LastVisit),  // Thêm GroupName cho JS
                    Views = x.Video.Viewers.Select(v => v.NumberOfVisit).Sum(),  // Tổng views
                    Duration = x.Video.Duration,
                    Progress = x.ProgressSeconds ?? 0
                });

            // Dùng PaginatedList để tự động tính pagination
            var paginatedList = await PaginatedList<object>.CreateAsync(query, pageNumber, pageSize);

            // Chuyển sang PaginatedResult để trả JSON
            var paginatedResults = new PaginatedResult<object>(
                paginatedList,
                paginatedList.TotalItemsCount,
                paginatedList.PageNumber,
                paginatedList.PageSize,
                paginatedList.TotalPages
            );

            return Json(new ApiResponse(200, result: paginatedResults));
        }

        private static string GetDateGroupName(DateTime date)
        {
            var today = DateTime.UtcNow.Date;
            if (date.Date == today) return "Hôm nay";
            if (date.Date == today.AddDays(-1)) return "Hôm qua";
            if (date.Date >= today.AddDays(-7)) return "Tuần này";
            if (date.Date >= today.AddMonths(-1)) return "Tháng này";
            return date.ToString("MMMM yyyy");
        }

        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpGet]
        public async Task<IActionResult> GetLikesDislikesVideos(bool liked, int pageNumber = 1, int pageSize = 12)
        {
            var query = Context.LikeDislikes
                .Where(x => x.AppUserId == User.GetUserId() && x.Liked == liked)
                .Select(x => new
                {
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    CreatedAtTimeAgo = SD.TimeAgo(x.Video.UploadDate),
                    x.Video.UploadDate,
                    Views = x.Video.Viewers.Select(v => v.NumberOfVisit).Sum(),  // Tổng views
                });
            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.UploadDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var paginatedResults = new PaginatedResult<object>(items, totalItems, pageNumber, pageSize, totalPages);
            return Json(new ApiResponse(200, result: paginatedResults));
        }

        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpPost]
        public async Task<IActionResult> RemoveHistory(Guid videoViewId)
        {
            var videoView = await Context.VideoViews
                .FirstOrDefaultAsync(x => x.Id == videoViewId && x.AppUserId == User.GetUserId());
            if (videoView == null)
            {
                return Json(new ApiResponse(404, message: "Video not found in history."));
            }

            Context.VideoViews.Remove(videoView);
            await Context.SaveChangesAsync();
            return Json(new ApiResponse(200, message: "Video removed from history."));
        }

        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpPost]
        public async Task<IActionResult> RemoveLike(Guid videoId)
        {
            var like = await Context.LikeDislikes
                .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId() && x.VideoId == videoId && x.Liked == true);
            if (like == null)
            {
                return Json(new ApiResponse(404, message: "Video not found in liked videos."));
            }

            Context.LikeDislikes.Remove(like);
            await Context.SaveChangesAsync();
            return Json(new ApiResponse(200, message: "Video removed from liked videos."));
        }
        [HttpPost]
        public async Task<IActionResult> AddOrUpdateView(string videoId)
        {
            if (!Guid.TryParse(videoId, out var videoGuid))
            {
                return Json(new ApiResponse(400, message: "Invalid video ID."));
            }

            var videoView = await Context.VideoViews
                .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId() && x.VideoId == videoGuid);

            var now = DateTime.UtcNow;
            bool increasedVisit = false;
            if (videoView == null)
            {
                videoView = new VideoView
                {
                    AppUserId = User.GetUserId(),
                    VideoId = videoGuid,
                    LastVisit = now,
                    NumberOfVisit = 1,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                };
                Context.VideoViews.Add(videoView);
                increasedVisit = true;
            }
            else
            {
                if (now > videoView.LastVisit.AddHours(1)) // ✅ Giới hạn tăng 1 lần/giờ
                {
                    videoView.NumberOfVisit += 1;
                    increasedVisit = true;
                }
                videoView.LastVisit = now;
                videoView.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            await Context.SaveChangesAsync();

            if (increasedVisit)
            {
                var video = await Context.Videos.FirstOrDefaultAsync(x => x.Id == videoGuid);
                if (video != null)
                {
                    video.Views = Context.VideoViews.Where(vv => vv.VideoId == videoGuid).Sum(vv => vv.NumberOfVisit); // ✅ Đồng bộ với tổng
                }
                await Context.SaveChangesAsync();
            }

            return Json(new ApiResponse(200, message: "View recorded successfully."));
        }
        #endregion
    }
}
