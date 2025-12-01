using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.IRepo;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels;
using Web_Video.ViewModels.Channel;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]

    public class ChannelController : CoreController
    {
        private readonly DataContext _context;
        private readonly IUnitOfWork UnitOfWork;

        public ChannelController(DataContext context, IUnitOfWork unitOfWork)
        {
            _context = context;
            UnitOfWork = unitOfWork;
        }

        public async Task<IActionResult> Index(string stringModel)
        {
            ViewData["CurrentPage"] = "Channel";
            var model = new ChannelAddEditViewModel();

            if (!string.IsNullOrEmpty(stringModel))
            {
                model = JsonConvert.DeserializeObject<ChannelAddEditViewModel>(stringModel);
                if (model.Errors.Any())
                {
                    foreach (var error in model.Errors)
                    {
                        ModelState.AddModelError(error.Key, error.ErrorMessage);
                    }
                    HttpContext.Session.Remove("ChannelModelFromSession");
                    return View(model);
                }
            }

            //var channel = await _context.Channels
            //    .Include(c => c.Subscribers)
            //    .Include(c => c.Videos)
            //    .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId());

            //if (channel != null)
            //{
            //    model.Id = channel.Id;
            //    model.Name = channel.ChannelName;
            //    model.About = channel.About;
            //    model.CreatedDate = channel.CreatedDate ?? DateTime.UtcNow;
            //    model.AvatarUrl = channel.ChannelPicture;
            //    model.BannerUrl = channel.BannerPicture ?? "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=1920";
            //    model.SubcribersCount = channel.Subscribers.Count;
            //    model.TotalVideos = channel.Videos.Count;
            //    model.TotalViews = channel.Videos.Sum(v => v.Views ?? 0);
            //}
            // --- ĐOẠN CODE MỚI TỐI ƯU (THÊM VÀO) ---
            // Chỉ lấy những trường cần thiết, SQL Server sẽ tự tính toán Count và Sum
            var channelData = await _context.Channels
                .Where(x => x.AppUserId == User.GetUserId())
                .Select(x => new
                {
                    x.Id,
                    x.ChannelName,
                    x.About,
                    x.CreatedDate,
                    x.ChannelPicture,
                    x.BannerPicture,
                    // Tính toán trực tiếp trong Database
                    SubcribersCount = x.Subscribers.Count(),
                    TotalVideos = x.Videos.Count(),
                    // Tính tổng view, xử lý null bằng Coalesce (?? 0)
                    TotalViews = x.Videos.Sum(v => (long)(v.Views ?? 0))
                })
                .FirstOrDefaultAsync();

            if (channelData != null)
            {
                model.Id = channelData.Id;
                model.Name = channelData.ChannelName;
                model.About = channelData.About;
                model.CreatedDate = channelData.CreatedDate ?? DateTime.UtcNow;
                model.AvatarUrl = channelData.ChannelPicture;
                // Logic banner mặc định giữ nguyên
                model.BannerUrl = !string.IsNullOrEmpty(channelData.BannerPicture)
                    ? channelData.BannerPicture
                    : "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=1920";

                model.SubcribersCount = channelData.SubcribersCount;
                model.TotalVideos = channelData.TotalVideos;
                model.TotalViews = channelData.TotalViews;
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CreateChannel(ChannelAddEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var item in ModelState)
                {
                    if (item.Value.Errors.Any())
                    {
                        model.Errors.Add(new ModelErrorViewModel
                        {
                            Key = item.Key,
                            ErrorMessage = item.Value.Errors.FirstOrDefault()?.ErrorMessage
                        });
                    }
                }
                HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                return RedirectToAction("Index");
            }

            try
            {
                var channelNameExists = await _context.Channels.AnyAsync(x => x.ChannelName.ToLower() == model.Name.ToLower());
                if (channelNameExists)
                {
                    model.Errors.Add(new ModelErrorViewModel
                    {
                        Key = "Name",
                        ErrorMessage = $"Channel name '{model.Name}' is already taken. Please choose another name."
                    });
                    HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                    return RedirectToAction("Index");
                }

                var channelToAdd = new Channel
                {
                    AppUserId = User.GetUserId(),
                    ChannelName = model.Name,
                    About = model.About,
                    ChannelPicture = "/avatarUser/avt-default.jpg",
                    BannerPicture = "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=1920",
                    CreatedDate = DateTime.UtcNow
                };

                if (model.Avatar != null && model.Avatar.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/avatarUser");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Avatar.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Avatar.CopyToAsync(fileStream);
                    }
                    channelToAdd.ChannelPicture = $"/avatarUser/{uniqueFileName}";
                }

                if (model.Banner != null && model.Banner.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/bannerUser");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Banner.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Banner.CopyToAsync(fileStream);
                    }
                    channelToAdd.BannerPicture = $"/bannerUser/{uniqueFileName}";
                }

                _context.Channels.Add(channelToAdd);
                await _context.SaveChangesAsync();

                TempData["notification"] = "true;Channel created successfully;Your channel has been created and you can upload videos now.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["notification"] = $"false;Error;An error occurred while creating the channel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditChannel(ChannelAddEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                foreach (var item in ModelState)
                {
                    if (item.Value.Errors.Any())
                    {
                        model.Errors.Add(new ModelErrorViewModel
                        {
                            Key = item.Key,
                            ErrorMessage = item.Value.Errors.FirstOrDefault()?.ErrorMessage
                        });
                    }
                }
                HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                return RedirectToAction("Index");
            }

            try
            {
                var channel = await _context.Channels.FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId());
                if (channel == null)
                {
                    TempData["notification"] = "false;Channel not found;Your channel was not found.";
                    return RedirectToAction("Index");
                }

                var channelNameExists = await _context.Channels.AnyAsync(x => x.ChannelName.ToLower() == model.Name.ToLower() && x.Id != channel.Id);
                if (channelNameExists)
                {
                    model.Errors.Add(new ModelErrorViewModel
                    {
                        Key = "Name",
                        ErrorMessage = $"Channel name '{model.Name}' is already taken. Please choose another name."
                    });
                    HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                    return RedirectToAction("Index");
                }

                channel.ChannelName = model.Name;
                channel.About = model.About;

                if (model.Avatar != null && model.Avatar.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/avatarUser");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Avatar.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Avatar.CopyToAsync(fileStream);
                    }
                    if (!string.IsNullOrEmpty(channel.ChannelPicture) && channel.ChannelPicture != "/avatarUser/avt-default.jpg")
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", channel.ChannelPicture.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }
                    channel.ChannelPicture = $"/avatarUser/{uniqueFileName}";
                }

                if (model.Banner != null && model.Banner.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/bannerUser");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(model.Banner.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.Banner.CopyToAsync(fileStream);
                    }
                    if (!string.IsNullOrEmpty(channel.BannerPicture) && !channel.BannerPicture.StartsWith("https://"))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", channel.BannerPicture.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }
                    channel.BannerPicture = $"/bannerUser/{uniqueFileName}";
                }

                await _context.SaveChangesAsync();
                TempData["notification"] = "true;Channel updated;Your channel has been updated successfully.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["notification"] = $"false;Error;An error occurred while updating the channel: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAnalytics(int days = 28)
        {
            string userId = User.GetUserId();
            var channel = await Context.Channels
                .Include(c => c.Videos)
                .ThenInclude(v => v.Viewers)
                .FirstOrDefaultAsync(c => c.AppUserId == userId);

            if (channel == null)
            {
                // Trả về dữ liệu trống nếu không có kênh
                return Json(new ApiResponse(200, result: new
                {
                    totalViews = 0,
                    subscribers = 0,
                    likes = 0,
                    comments = 0,
                    viewsChange = 0,
                    subscribersChange = 0,
                    likesChange = 0,
                    commentsChange = 0,
                    viewsOverTime = new { Labels = Array.Empty<string>(), Data = Array.Empty<int>() },
                    trafficSources = new { Labels = Array.Empty<string>(), Data = Array.Empty<int>() }
                }));
            }

            DateTime endDate = DateTime.UtcNow.Date;
            DateTime startDate = endDate.AddDays(-days);

            // Khởi tạo các list/dictionary cần thiết
            var viewsOverTimeLabels = new List<string>();
            var viewsOverTimeData = new List<int>();
            var trafficSourceCounts = new Dictionary<string, int>
    {
        {"Direct", 0}, {"Search", 0}, {"Social", 0}, {"External Website", 0}, {"Other", 0}
    };

            // Lấy toàn bộ lượt xem của kênh trong khoảng thời gian để xử lý 2 biểu đồ
            var allChannelViews = await Context.VideoViews
                .Include(vv => vv.Video)
                .Where(vv => vv.Video.ChannelId == channel.Id && vv.ViewDate >= startDate)
                .ToListAsync();

            // ===============================================
            // 1. BIỂU ĐỒ LƯỢT XEM THEO THỜI GIAN (Views Over Time)
            // ===============================================

            int groupByDays = (days > 90) ? 30 : 1;

            for (var date = startDate.Date; date < endDate; date = date.AddDays(groupByDays))
            {
                var nextDate = date.AddDays(groupByDays);

                // Nhãn
                viewsOverTimeLabels.Add(groupByDays > 1 ? date.ToString("MMM yyyy") : date.ToString("MMM d"));

                // Tính tổng lượt xem thực tế từ VideoViews trong khoảng thời gian này
                var viewsInPeriod = allChannelViews
                    .Where(vv => vv.ViewDate >= date && vv.ViewDate < nextDate)
                    .Sum(vv => vv.NumberOfVisit);

                viewsOverTimeData.Add(viewsInPeriod);
            }

            // ===============================================
            // 2. BIỂU ĐỒ NGUỒN LƯU LƯỢNG TRUY CẬP (Traffic Sources)
            // ===============================================

            foreach (var view in allChannelViews)
            {
                string source = GetTrafficSource(view.RefererUrl);
                int totalVisits = view.NumberOfVisit;

                if (trafficSourceCounts.ContainsKey(source))
                {
                    trafficSourceCounts[source] += totalVisits;
                }
                else
                {
                    trafficSourceCounts["Other"] += totalVisits;
                }
            }

            var trafficSourcesLabels = trafficSourceCounts.Keys.ToList();
            var trafficSourcesData = trafficSourceCounts.Values.ToList();

            // ===============================================
            // 3. TÍNH CÁC METRICS CƠ BẢN
            // ===============================================

            // Tính tổng lượt xem (totalViews) và các metric khác
            long totalViews = channel.Videos.Sum(v => v.Viewers.Sum(vv => vv.NumberOfVisit));
            int subscribers = channel.Subscribers.Count;

            var videosInChannel = await Context.Videos
                .Include(v => v.LikeDislikes)
                .Include(v => v.Comments)
                .Where(v => v.ChannelId == channel.Id)
                .ToListAsync();

            long totalLikes = videosInChannel.Sum(v => v.LikeDislikes.Count(ld => ld.Liked == true));
            long totalComments = videosInChannel.Sum(v => v.Comments.Count);

            // Placeholder cho Change Percentages (Giữ nguyên logic cũ nếu không có thay đổi)
            int viewsChange = 0;
            int subscribersChange = 0;
            int likesChange = 0;
            int commentsChange = 0;

            var viewsOverTimeResult = new { Labels = viewsOverTimeLabels, Data = viewsOverTimeData };
            var trafficSourcesResult = new { Labels = trafficSourcesLabels, Data = trafficSourcesData };

            // Trả về kết quả
            return Json(new ApiResponse(200, result: new
            {
                totalViews = totalViews,
                subscribers = subscribers,
                likes = totalLikes,
                comments = totalComments,
                viewsChange = viewsChange,
                subscribersChange = subscribersChange,
                likesChange = likesChange,
                commentsChange = commentsChange,
                viewsOverTime = viewsOverTimeResult,
                trafficSources = trafficSourcesResult
            }));
        }
        // HÀM HELPER: Phân loại Referer
        private string GetTrafficSource(string refererUrl)
        {
            if (string.IsNullOrEmpty(refererUrl) || refererUrl.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                return "Direct";

            string url = refererUrl.ToLower();

            if (url.Contains("google.com/search") || url.Contains("bing.com") || url.Contains("yahoo.com"))
                return "Search";

            if (url.Contains("facebook.com") || url.Contains("twitter.com") || url.Contains("instagram.com") || url.Contains("linkedin.com"))
                return "Social";

            // Nếu là các trang web khác (External)
            if (Uri.TryCreate(refererUrl, UriKind.Absolute, out Uri uri))
            {
                // Đảm bảo không phải là trang nội bộ (localhost/domain chính)
                if (!uri.Host.Contains(HttpContext.Request.Host.Host) && !uri.Host.Contains("localhost"))
                {
                    return "External Website";
                }
            }
            return "Other";
        }
        // Đặt DTO này trong nơi chứa DTOs hoặc trong ChannelController.cs
        public class ChannelVideosParameters
        {
            public int PageNumber { get; set; } = 1;
            public int PageSize { get; set; } = 10;
            public string SortBy { get; set; } = "";
            public List<Guid> ExcludeIds { get; set; } = new List<Guid>(); // QUAN TRỌNG
        }

        [HttpPost]
        public async Task<IActionResult> GetVideosForChannelGrid([FromBody] ChannelVideosParameters parameters)
        {
            try
            {
                var userId = User.GetUserId();
                // Lấy ChannelId của người dùng hiện tại [6]
                var channelId = await UnitOfWork.ChannelRepo.GetChannelIdByUserId(userId);

                if (channelId == Guid.Empty)
                {
                    return Json(new ApiResponse(404, message: "Channel not found"));
                }

                // Lấy IQueryable ban đầu
                var query = Context.Videos
                    .Include(x => x.Category)
                    .Include(x => x.Viewers)
                    .Where(x => x.ChannelId == channelId)
                    .AsNoTracking();

                // QUAN TRỌNG: Lọc ra các video đã được hiển thị trên Frontend
                if (parameters.ExcludeIds != null && parameters.ExcludeIds.Any())
                {
                    query = query.Where(x => !parameters.ExcludeIds.Contains(x.Id));
                }

                // Áp dụng sắp xếp (Nếu SortBy rỗng, mặc định sắp xếp theo UploadDate)
                // Việc sắp xếp ổn định là cần thiết cho pagination (Skip/Take)
                query = query.OrderByDescending(x => x.UploadDate);

                // Chuyển đổi sang DTO và áp dụng phân trang (dùng PaginatedList)
                var dtoQuery = query.Select(x => new VideoGridChannelDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail != null ? x.Thumbnail.Replace("\\", "/") : "/avatarUser/avt-default.jpg",
                    Duration = x.Duration ?? "0:00",
                    CreatedAt = x.UploadDate,
                    CategoryName = x.Category.CategoryName,
                    // Tính tổng Views từ VideoViews [7, 8]
                    Views = x.Viewers.Sum(v => v.NumberOfVisit),
                    Comments = x.Comments.Count(),
                    Likes = x.LikeDislikes.Count(l => l.Liked == true),
                    Dislikes = x.LikeDislikes.Count(l => l.Liked == false),
                    // Dislikes cần được tính tương tự nếu cần
                    // ...
                });

                // Tạo danh sách phân trang
                var paginatedResults = await PaginatedList<VideoGridChannelDto>.CreateAsync(
                     dtoQuery,
                     parameters.PageNumber,
                     parameters.PageSize
                );

                // Trả về kết quả
                return Json(new ApiResponse(200, result: new
                {
                    items = paginatedResults,
                    totalItemsCount = paginatedResults.TotalItemsCount,
                    pageNumber = paginatedResults.PageNumber,
                    totalPages = paginatedResults.TotalPages
                }));
            }
            catch (Exception ex)
            {
                // [9]: Log lỗi chi tiết
                Console.WriteLine($"Error in GetVideosForChannelGrid: {ex.Message}\n{ex.StackTrace}");
                return StatusCode(500, new { statusCode = 500, message = $"Error: {ex.Message}" });
            }
        }
        [HttpGet]
        public async Task<IActionResult> GetTotalViews()
        {
            try
            {
                var userId = User.GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { statusCode = 401, message = "User not authenticated." });
                }

                var channel = await _context.Channels
                    .Include(c => c.Videos)
                    .Include(c => c.Subscribers)
                    .FirstOrDefaultAsync(c => c.AppUserId == userId);

                int totalViews = 0;
                int subscribers = 0;
                if (channel != null)
                {
                    totalViews = channel.Videos.Sum(v => v.Views ?? 0);
                    subscribers = channel.Subscribers.Count;
                }

                return Json(new { statusCode = 200, totalViews, subscribers });
            }
            catch (Exception ex)
            {
                return Json(new { statusCode = 500, message = $"Error: {ex.Message}" });
            }
        }
    }
}