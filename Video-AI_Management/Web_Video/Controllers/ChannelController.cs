using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
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
        public async Task<IActionResult> Index(string stringModel)
        {
            var model = new ChannelAddEditViewModel();
            HttpContext.Session.GetString("ChannelModelFromSession");
            if (!string.IsNullOrEmpty(stringModel))
            {
                model = JsonConvert.DeserializeObject<ChannelAddEditViewModel>(stringModel);
                if (model.Errors.Count > 0)
                {
                    foreach (var error in model.Errors)
                    {
                        ModelState.AddModelError(error.Key, error.ErrorMessage);
                    }
                    HttpContext.Session.Remove("ChannelModelFromSession");
                    return View(model);
                }
            }
            var channel = await UnitOfWork.ChannelRepo.GetFirstOrDefaultAsync(x => x.AppUserId == User.GetUserId(),includeProperties: "Subscribers");
            if (channel != null)
            {
                model.Name = channel.ChannelName;
                model.About = channel.About;
                model.SubcribersCount = channel.Subscribers.Count();
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
                    if (item.Value.Errors.Count > 0)
                    {
                        model.Errors.Add(new ModelErrorViewModel
                        {
                            Key = item.Key,
                            ErrorMessage = item.Value.Errors.Select(x => x.ErrorMessage).FirstOrDefault()
                        });
                    }
                }
                HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                return RedirectToAction("Index");
            }

            var channelNameExists = await UnitOfWork.ChannelRepo.AnyAsync(x => x.ChannelName.ToLower() == model.Name.ToLower());
            if (channelNameExists)
            {
                model.Errors.Add(new ModelErrorViewModel
                {
                    Key = "Name",
                    ErrorMessage = $"Channel name of {model.Name} is taken. Please try another name."
                });
                //return RedirectToAction("Index", new { stringModel = JsonConvert.SerializeObject(model) });
                HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                return RedirectToAction("Index");
            }
            var channelToAdd = new Channel
            {
                AppUserId = User.GetUserId(),
                ChannelName = model.Name,
                About = model.About
            };
            UnitOfWork.ChannelRepo.Add(channelToAdd);
            await UnitOfWork.CompleteAsync();

            TempData["notification"] = "true;Channel created successfully; Your channel has been created and you can upload clips now";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> EditChannel(ChannelAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                var channel = await UnitOfWork.ChannelRepo.GetFirstOrDefaultAsync(x => x.AppUserId == User.GetUserId());
                if (channel != null)
                {
                    channel.ChannelName = model.Name;
                    channel.About = model.About;
                    await UnitOfWork.CompleteAsync();

                    TempData["notification"] = "true;Channel updated; Your channel has been updated";
                    return RedirectToAction("Index");
                }
            }
            TempData["notification"] = "false;Channel not found; Your channel was not found";
            return RedirectToAction("Index");
        }
    }
}

//PHẦN CODE MẪU CHO TAB ANALYTICS

//public class ChannelController : Controller
//{
//    private readonly IChannelService _channelService;
//    private readonly IVideoService _videoService;

//    public ChannelController(IChannelService channelService, IVideoService videoService)
//    {
//        _channelService = channelService;
//        _videoService = videoService;
//    }

//    // ... Các action khác ...

//    [HttpGet]
//    public IActionResult GetAnalytics(string timeFilter)
//    {
//        // Xác định khoảng thời gian dựa trên timeFilter
//        DateTime startDate;
//        switch (timeFilter)
//        {
//            case "7":
//                startDate = DateTime.Now.AddDays(-7);
//                break;
//            case "28":
//                startDate = DateTime.Now.AddDays(-28);
//                break;
//            case "90":
//                startDate = DateTime.Now.AddDays(-90);
//                break;
//            case "all":
//            default:
//                startDate = DateTime.MinValue; // Lấy tất cả thời gian
//                break;
//        }

//        // Lấy dữ liệu từ service (giả lập hoặc thực tế)
//        var videos = _videoService.GetVideosForChannel(User.Identity.Name)
//            .Where(v => v.CreatedAt >= startDate)
//            .ToList();

//        var totalViews = videos.Sum(v => v.Views);
//        var totalLikes = videos.Sum(v => v.Likes);
//        var totalComments = videos.Sum(v => v.Comments);
//        var subscribers = _channelService.GetChannelByUser(User.Identity.Name).SubcribersCount;

//        // Dữ liệu cho biểu đồ Views Over Time
//        var viewsOverTime = new
//        {
//            labels = new List<string>(),
//            data = new List<int>()
//        };
//        if (timeFilter == "7")
//        {
//            for (int i = 6; i >= 0; i--)
//            {
//                var date = DateTime.Now.AddDays(-i).Date;
//                viewsOverTime.labels.Add(date.ToString("MMM d"));
//                viewsOverTime.data.Add(videos.Where(v => v.CreatedAt.Date == date).Sum(v => v.Views));
//            }
//        }
//        else if (timeFilter == "28")
//        {
//            for (int i = 27; i >= 0; i -= 4)
//            {
//                var date = DateTime.Now.AddDays(-i).Date;
//                viewsOverTime.labels.Add(date.ToString("MMM d"));
//                viewsOverTime.data.Add(videos.Where(v => v.CreatedAt.Date >= date && v.CreatedAt.Date < date.AddDays(4)).Sum(v => v.Views));
//            }
//        }
//        else if (timeFilter == "90")
//        {
//            for (int i = 89; i >= 0; i -= 10)
//            {
//                var date = DateTime.Now.AddDays(-i).Date;
//                viewsOverTime.labels.Add(date.ToString("MMM d"));
//                viewsOverTime.data.Add(videos.Where(v => v.CreatedAt.Date >= date && v.CreatedAt.Date < date.AddDays(10)).Sum(v => v.Views));
//            }
//        }
//        else
//        {
//            var firstVideoDate = videos.Any() ? videos.Min(v => v.CreatedAt).Date : DateTime.Now.Date;
//            var days = (DateTime.Now.Date - firstVideoDate).Days;
//            for (int i = days; i >= 0; i -= Math.Max(1, days / 10))
//            {
//                var date = DateTime.Now.AddDays(-i).Date;
//                viewsOverTime.labels.Add(date.ToString("MMM d"));
//                viewsOverTime.data.Add(videos.Where(v => v.CreatedAt.Date >= date && v.CreatedAt.Date < date.AddDays(Math.Max(1, days / 10))).Sum(v => v.Views));
//            }
//        }

//        // Dữ liệu cho biểu đồ Traffic Sources (giả lập)
//        var trafficSources = new
//        {
//            labels = new[] { "Direct", "Search", "External", "Social" },
//            data = new[] { 40, 30, 20, 10 } // Giả lập tỷ lệ phần trăm
//        };

//        var result = new
//        {
//            totalViews,
//            subscribers,
//            likes = totalLikes,
//            comments = totalComments,
//            viewsOverTime,
//            trafficSources
//        };

//        return Json(result);
//    }

//    [HttpGet]
//    public IActionResult GetTotalViews()
//    {
//        var videos = _videoService.GetVideosForChannel(User.Identity.Name).ToList();
//        var totalViews = videos.Sum(v => v.Views);
//        return Json(new { totalViews });
//    }
//}