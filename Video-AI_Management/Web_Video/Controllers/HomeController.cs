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
using System.Collections.Generic;
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
using static Web_Video.Controllers.VideoController;

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
        // Endpoint xử lý Block Video/Channel
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> BlockContent(Guid targetId, string type) // Type: "Video" or "Channel"
        {
            var userId = User.GetUserId();
            var existingBlock = await Context.Set<UserBlock>()
                .FirstOrDefaultAsync(b => b.AppUserId == userId && b.TargetId == targetId && b.Type == type);

            if (existingBlock == null)
            {
                Context.Set<UserBlock>().Add(new UserBlock
                {
                    AppUserId = userId,
                    TargetId = targetId,
                    Type = type
                });
                await Context.SaveChangesAsync();
            }
            return Json(new { isSuccess = true, message = "Content removed from recommendations." });
        }
        #region API Endpoints
        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        //[HttpGet]
        [HttpPost] // Đổi sang POST để gửi list ID lớn
        public async Task<IActionResult> GetVideosForHomeGrid([FromBody] HomeParameters parameters)
        {
            // NẾU CÓ TÌM KIẾM HOẶC CHỌN CATEGORY -> Dùng logic cũ (filter)
            if (!string.IsNullOrEmpty(parameters.SearchBy) && parameters.SearchBy.ToLower() != "all" || parameters.CategoryId != Guid.Empty)
            {
                var items = await UnitOfWork.VideoRepo.GetVideosForHomeGridAsync(parameters);
                return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(items,
                    items.TotalItemsCount, items.PageNumber, items.PageSize, items.TotalPages)));
            }

            // --- BƯỚC 1: GỌI HỆ THỐNG GỢI Ý CÁ NHÂN HÓA (PYTHON AI) ---
            try
            {
                string userId = User.Identity.IsAuthenticated ? User.GetUserId() : "";
                var httpClient = _httpClientFactory.CreateClient();

                // THÊM excludeIds (danh sách video đã hiển thị trên FE)
                var payload = new
                {
                    userId = userId,
                    currentVideoId = (Guid?)null,
                    excludeIds = parameters.ExcludeIds ?? new List<Guid>()
                };

                httpClient.Timeout = TimeSpan.FromSeconds(4);

                var response = await httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend", payload);

                if (response.IsSuccessStatusCode)
                {
                    var apiResult = await response.Content.ReadFromJsonAsync<PythonRecommendResponse>();
                    var videoIds = apiResult.recommendations;

                    if (videoIds != null && videoIds.Any())
                    {
                        // LẤY TOÀN BỘ VIDEO THEO DANH SÁCH ID TỪ PYTHON
                        var videosFromDb = await Context.Videos
                            .Include(x => x.Channel)
                            .Include(x => x.Viewers)
                            .Where(x => videoIds.Contains(x.Id))
                            .ToListAsync();

                        // SẮP XẾP ĐÚNG THEO THỨ TỰ ID TỪ PYTHON
                        var orderedVideos = videoIds
                            .Join(videosFromDb, id => id, v => v.Id, (id, v) => v)
                            .Select(x => new VideoForHomeGridDto
                            {
                                Id = x.Id,
                                Title = x.Title,
                                Thumbnail = x.Thumbnail,
                                Duration = x.Duration ?? "0:00",
                                ChannelName = x.Channel.ChannelName,
                                Views = x.Viewers.Select(v => v.NumberOfVisit).Sum(), // FIX P1
                                CreatedAtTimeAgo = SD.TimeAgo(x.UploadDate),
                                ChannelAvatar = x.Channel.ChannelPicture ?? "/avatarUser/avt-default.jpg"
                            })
                            .ToList();

                        // ❌ BỎ Skip/Take (Python đã phân trang)
                        // Trả toàn bộ danh sách AI trả về
                        //return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(
                        //    orderedVideos, orderedVideos.Count, 1, orderedVideos.Count, 1)));
                        return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(
                                orderedVideos,
                                // Thiết lập tổng số mục lớn (ví dụ 1000) để đảm bảo hasMore vẫn là TRUE cho lần tải sau
                                totalItemsCount: 1000,
                                pageNumber: parameters.PageNumber, // Sử dụng PageNumber hiện tại
                                pageSize: parameters.PageSize,
                                totalPages: (int)Math.Ceiling(1000.0 / parameters.PageSize)
                            )));
                    }
                }
            }
            catch (Exception)
            {
                // Log lỗi nhưng không chặn user -> Chạy xuống Fallback
            }

            // --- BƯỚC 2: FALLBACK (Nếu Python lỗi hoặc User mới tinh) ---
            // FIX P1 (Freshness): Sử dụng Skip + Take kết hợp Guid.NewGuid() để đảm bảo phân trang ngẫu nhiên cho Fallback.
            var totalFallbackItems = await Context.Videos.CountAsync();

            var randomVideos = await Context.Videos
                .Include(x => x.Channel)
                .OrderBy(x => Guid.NewGuid()) // Random native SQL
                .Skip((parameters.PageNumber - 1) * parameters.PageSize) // Phân trang cho Fallback
                .Take(parameters.PageSize)
                .Select(x => new VideoForHomeGridDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail,
                    Duration = x.Duration ?? "0:00",
                    ChannelName = x.Channel.ChannelName,
                    // FIX P1: Tính Views bằng SUM(NumberOfVisit)
                    Views = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    CreatedAtTimeAgo = SD.TimeAgo(x.UploadDate)
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalFallbackItems / parameters.PageSize);

            return Json(new ApiResponse(200, result: new PaginatedResult<VideoForHomeGridDto>(
                randomVideos, totalFallbackItems, parameters.PageNumber, parameters.PageSize, totalPages)));
        }
        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpGet]
        public async Task<IActionResult> GetSubscriptions(int pageNumber = 1, int pageSize = 12)
        {
            var userId = User.GetUserId();

            // Lấy danh sách các kênh mà user này đã đăng ký
            var query = Context.Subscribes
                .Include(s => s.Channel) // Include thông tin kênh
                .ThenInclude(c => c.Videos) // Include video để đếm số lượng
                .Where(s => s.AppUserId == userId)
                .Select(s => new
                {
                    Id = s.ChannelId,
                    ChannelName = s.Channel.ChannelName,
                    Thumbnail = s.Channel.ChannelPicture, // Ảnh đại diện kênh
                    VideosCount = s.Channel.Videos.Count() // Số lượng video
                });

            var totalItems = await query.CountAsync();

            var items = await query
                .OrderBy(x => x.ChannelName) // Sắp xếp theo tên
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Trả về format chuẩn giống các API khác
            var paginatedResults = new PaginatedResult<object>(items, totalItems, pageNumber, pageSize, totalPages);

            // Thêm currentPage vào result để JS xử lý logic "Xem thêm"
            return Json(new ApiResponse(200, result: new
            {
                items = paginatedResults.Items,
                totalPages = paginatedResults.TotalPages,
                currentPage = paginatedResults.PageNumber,
                totalItemsCount = paginatedResults.TotalItemsCount
            }));
        }
        [Authorize(Roles = $"{SD.UserRole},{SD.AdminRole}")]
        [HttpPost] // Thay thế [HttpGet]
                   // Đọc tham số phân trang từ body (JSON object)
        public async Task<IActionResult> GetHistory([FromBody] HomeParameters parameters)
        {
            // Dùng parameters.PageNumber và parameters.PageSize
            var query = Context.VideoViews
                .Where(x => x.AppUserId == User.GetUserId())
                .OrderByDescending(x => x.LastVisit)
                .Select(x => new
                {
                    VideoViewId = x.Id,
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    LastVisitTimeAgo = SD.TimeAgo(x.LastVisit),
                    LastVisit = x.LastVisit,
                    GroupName = GetDateGroupName(x.LastVisit),
                    Views = x.Video.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    Duration = x.Video.Duration,
                    Progress = x.ProgressSeconds ?? 0
                });

            // Cập nhật lại logic phân trang để sử dụng tham số từ body
            var paginatedList = await PaginatedList<object>.CreateAsync(query, parameters.PageNumber, parameters.PageSize);

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
        [HttpPost] // Thay thế [HttpGet]
                   // 'liked' vẫn lấy từ query string. parameters lấy từ body.
        public async Task<IActionResult> GetLikesDislikesVideos(bool liked, [FromBody] HomeParameters parameters)
        {
            // Sử dụng parameters.PageNumber và parameters.PageSize
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
                    Views = x.Video.Viewers.Select(v => v.NumberOfVisit).Sum(), // Tổng views [5]
                });

            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.UploadDate)
                // Cập nhật Skip và Take để sử dụng tham số từ body
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalItems / parameters.PageSize);
            var paginatedResults = new PaginatedResult<object>(items, totalItems, parameters.PageNumber, parameters.PageSize, totalPages);
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
