using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Database_Video.DTOs;
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

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index(string page)
        {
            var toReturn = new HomeViewModel();
            if (User.Identity.IsAuthenticated)
            {
                toReturn.Page = page;
                if (page == null || page == "Home")
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
            }
            return View(toReturn);
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
        public async Task<IActionResult> GetSubscriptions()
        {
            var userSubscribedChannels = await Context.Subscribes
               .Where(x => x.AppUserId == User.GetUserId())
               // project the result into an anonymous object
               .Select(x => new
               {
                   Id = x.ChannelId,
                   ChannelName = x.Channel.ChannelName,
                   VideosCount = x.Channel.Videos.Count
               }).ToListAsync();

            return Json(new ApiResponse(200, result: userSubscribedChannels));
        }

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            var userWatchedVideoHistory = await Context.VideoViews
                .Where(x => x.AppUserId == User.GetUserId())
                // project the result into an anonymous object
                .Select(x => new
                {
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    LastVisitTimeAgo = SD.TimeAgo(x.LastVisit),
                    x.LastVisit
                }).ToListAsync();

            return Json(new ApiResponse(200, result: userWatchedVideoHistory));
        }

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetLikesDislikesVideos(bool liked)
        {
            var userLikedDislikedVideos = await Context.LikeDislikes
                .Where(x => x.AppUserId == User.GetUserId() && x.Liked == liked)
                // project the result into an anonymous object
                .Select(x => new
                {
                    Id = x.VideoId,
                    x.Video.Title,
                    x.Video.Thumbnail,
                    ChannelName = x.Video.Channel.ChannelName,
                    ChannelId = x.Video.Channel.Id,
                    CreatedAtTimeAgo = SD.TimeAgo(x.Video.UploadDate),
                    x.Video.UploadDate
                }).ToListAsync();

            return Json(new ApiResponse(200, result: userLikedDislikedVideos));
        }
    }
    #endregion
}
