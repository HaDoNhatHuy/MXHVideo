using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.IRepo;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        public HomeController(DataContext context, ILogger<HomeController> logger)
        {
            _logger = logger;
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
        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetVideosForHomeGrid(HomeParameters parameters)
        {
            var items = await UnitOfWork.VideoRepo.GetVideosForHomeGridAsync(parameters);
            var paginatedResults = new PaginatedResult<VideoForHomeGridDto>(items, items.TotalItemsCount, items.PageNumber, items.PageSize, items.TotalPages);
            return Json(new ApiResponse(200, result: paginatedResults));
        }

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetSubscriptions(int pageNumber = 1, int pageSize = 5)
        {
            _logger.LogInformation("GetSubscriptions called for user {UserId}, page {PageNumber}", User.GetUserId(), pageNumber);
            var query = Context.Subscribes
                .Where(x => x.AppUserId == User.GetUserId())
                .Select(x => new
                {
                    id = x.ChannelId,
                    channelName = x.Channel.ChannelName ?? "Unknown Channel",
                    channelPicture = x.Channel.ChannelPicture ?? "/avatarUser/avt-default.jpg"
                });

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var items = await query
                .OrderBy(x => x.channelName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            _logger.LogInformation("Subscriptions count: {Count}, total pages: {TotalPages}", items.Count, totalPages);
            return Json(new
            {
                statusCode = 200,
                result = new
                {
                    items = items,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    currentPage = pageNumber
                }
            });
        }

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetHistory(int pageNumber = 1, int pageSize = 12)
        {
            var query = Context.VideoViews
                .Where(x => x.AppUserId == User.GetUserId())
                .Select(x => new
                {
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    LastVisitTimeAgo = SD.TimeAgo(x.LastVisit),
                    x.LastVisit,
                    Views = x.Video.Views
                });
            var totalItems = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.LastVisit)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            var paginatedResults = new PaginatedResult<object>(items, totalItems, pageNumber, pageSize, totalPages);
            return Json(new ApiResponse(200, result: paginatedResults));
        }

        [Authorize(Roles = $"{SD.UserRole}")]
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
                    Views = x.Video.Views
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

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpPost]
        public async Task<IActionResult> RemoveHistory(Guid videoId)
        {
            var videoView = await Context.VideoViews
                .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId() && x.VideoId == videoId);
            if (videoView == null)
            {
                return Json(new ApiResponse(404, message: "Video not found in history."));
            }

            Context.VideoViews.Remove(videoView);
            await Context.SaveChangesAsync();
            return Json(new ApiResponse(200, message: "Video removed from history."));
        }

        [Authorize(Roles = $"{SD.UserRole}")]
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

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpPost]
        public async Task<IActionResult> AddOrUpdateView(string videoId)
        {
            if (!Guid.TryParse(videoId, out var videoGuid))
            {
                return Json(new ApiResponse(400, message: "Invalid video ID."));
            }

            var videoView = await Context.VideoViews
                .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId() && x.VideoId == videoGuid);

            if (videoView == null)
            {
                videoView = new VideoView
                {
                    AppUserId = User.GetUserId(),
                    VideoId = videoGuid,
                    LastVisit = DateTime.UtcNow,
                    NumberOfVisit = 1,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                };
                Context.VideoViews.Add(videoView);
            }
            else
            {
                videoView.LastVisit = DateTime.UtcNow;
                videoView.NumberOfVisit += 1;
            }

            var video = await Context.Videos.FirstOrDefaultAsync(x => x.Id == videoGuid);
            if (video != null)
            {
                video.Views = (video.Views ?? 0) + 1;
            }

            await Context.SaveChangesAsync();
            return Json(new ApiResponse(200, message: "View recorded successfully."));
        }
        #endregion
    }
}