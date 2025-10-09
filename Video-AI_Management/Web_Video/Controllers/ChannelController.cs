using DataAccess.Data;
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

            var channel = await _context.Channels
                .Include(c => c.Subscribers)
                .Include(c => c.Videos)
                .FirstOrDefaultAsync(x => x.AppUserId == User.GetUserId());

            if (channel != null)
            {
                model.Id = channel.Id;
                model.Name = channel.ChannelName;
                model.About = channel.About;
                model.CreatedDate = channel.CreatedDate ?? DateTime.UtcNow;
                model.AvatarUrl = channel.ChannelPicture;
                model.BannerUrl = channel.BannerPicture ?? "https://images.unsplash.com/photo-1579546929518-9e396f3cc809?w=1920";
                model.SubcribersCount = channel.Subscribers.Count;
                model.TotalVideos = channel.Videos.Count;
                model.TotalViews = channel.Videos.Sum(v => v.Views ?? 0);
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
        public async Task<IActionResult> GetAnalytics(string timeFilter)
        {
            try
            {
                var userId = User.GetUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated." });
                }

                var channel = await _context.Channels
                    .Include(c => c.Videos)
                    .ThenInclude(v => v.LikeDislikes)
                    .Include(c => c.Videos)
                    .ThenInclude(v => v.Comments)
                    .Include(c => c.Subscribers)
                    .FirstOrDefaultAsync(c => c.AppUserId == userId);

                if (channel == null)
                {
                    return NotFound(new { message = "Channel not found." });
                }

                DateTime startDate;
                int groupByDays;
                switch (timeFilter?.ToLower())
                {
                    case "7": startDate = DateTime.UtcNow.AddDays(-7); groupByDays = 1; break;
                    case "28": startDate = DateTime.UtcNow.AddDays(-28); groupByDays = 4; break;
                    case "90": startDate = DateTime.UtcNow.AddDays(-90); groupByDays = 10; break;
                    case "all": default: startDate = DateTime.MinValue; groupByDays = 30; break;
                }

                var filteredVideos = channel.Videos.Where(v => v.UploadDate >= startDate).ToList();
                var totalViews = filteredVideos.Sum(v => v.Views ?? 0);
                var totalLikes = filteredVideos.SelectMany(v => v.LikeDislikes).Count(ld => ld.Liked == true);
                var totalComments = filteredVideos.SelectMany(v => v.Comments).Count();
                var totalSubscribers = channel.Subscribers.Count;

                var previousStartDate = startDate.AddMonths(-1);
                var previousEndDate = startDate;
                var previousVideos = channel.Videos.Where(v => v.UploadDate >= previousStartDate && v.UploadDate < previousEndDate).ToList();

                var previousViews = previousVideos.Sum(v => v.Views ?? 0);
                var previousLikes = previousVideos.SelectMany(v => v.LikeDislikes).Count(ld => ld.Liked == true);
                var previousComments = previousVideos.SelectMany(v => v.Comments).Count();

                double viewsChange = previousViews > 0 ? Math.Round(((double)(totalViews - previousViews) / previousViews) * 100, 1) : 0;
                double subscribersChange = 0;
                double likesChange = previousLikes > 0 ? Math.Round(((double)(totalLikes - previousLikes) / previousLikes) * 100, 1) : 0;
                double commentsChange = previousComments > 0 ? Math.Round(((double)(totalComments - previousComments) / previousComments) * 100, 1) : 0;

                var viewsOverTimeLabels = new List<string>();
                var viewsOverTimeData = new List<int>();

                if (timeFilter != "all")
                {
                    var endDate = DateTime.UtcNow.Date;
                    for (var date = startDate.Date; date <= endDate; date = date.AddDays(groupByDays))
                    {
                        var nextDate = date.AddDays(groupByDays);
                        viewsOverTimeLabels.Add(date.ToString("MMM d"));
                        viewsOverTimeData.Add(filteredVideos.Where(v => v.UploadDate.Date >= date && v.UploadDate.Date < nextDate).Sum(v => v.Views ?? 0));
                    }
                }
                else
                {
                    var firstVideoDate = filteredVideos.Any() ? filteredVideos.Min(v => v.UploadDate.Date) : DateTime.UtcNow.Date;
                    var totalDays = (DateTime.UtcNow.Date - firstVideoDate).Days;
                    groupByDays = Math.Max(1, totalDays / 10);
                    for (var i = 0; i <= totalDays; i += groupByDays)
                    {
                        var date = firstVideoDate.AddDays(i);
                        var nextDate = date.AddDays(groupByDays);
                        viewsOverTimeLabels.Add(date.ToString("MMM d"));
                        viewsOverTimeData.Add(filteredVideos.Where(v => v.UploadDate.Date >= date && v.UploadDate.Date < nextDate).Sum(v => v.Views ?? 0));
                    }
                }

                var viewsOverTime = new { Labels = viewsOverTimeLabels, Data = viewsOverTimeData };
                var trafficSources = new { Labels = new[] { "Direct", "Search", "External", "Social" }, Data = new[] { 40, 30, 20, 10 } };

                return Json(new
                {
                    statusCode = 200,
                    result = new
                    {
                        totalViews,
                        subscribers = totalSubscribers,
                        likes = totalLikes,
                        comments = totalComments,
                        viewsChange,
                        subscribersChange,
                        likesChange,
                        commentsChange,
                        viewsOverTime,
                        trafficSources
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = $"Error in GetAnalytics: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetVideosForChannelGrid(int pageNumber = 1, int pageSize = 10, string sortBy = "")
        {
            try
            {
                Console.WriteLine($"GetVideosForChannelGrid called: pageNumber={pageNumber}, pageSize={pageSize}, sortBy={sortBy}");
                var userId = User.GetUserId();
                Console.WriteLine($"UserId: {userId}");
                if (string.IsNullOrEmpty(userId))
                {
                    Console.WriteLine("Unauthorized: User not authenticated");
                    return Unauthorized(new { statusCode = 401, message = "User not authenticated." });
                }

                var userChannelId = await UnitOfWork.ChannelRepo.GetChannelIdByUserId(userId);
                Console.WriteLine($"UserChannelId: {userChannelId}");
                if (userChannelId == Guid.Empty)
                {
                    Console.WriteLine("Not found: Channel not found for user");
                    return NotFound(new { statusCode = 404, message = "Channel not found." });
                }

                var parameters = new BaseParameters
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    SortBy = sortBy
                };

                var videosForGrid = await UnitOfWork.VideoRepo.GetVideosForChannelGridAsync(userChannelId, parameters);
                Console.WriteLine($"Videos returned: {videosForGrid?.Count ?? 0}");
                if (videosForGrid == null || videosForGrid == null)
                {
                    Console.WriteLine("No videos found or repository returned null");
                    return Json(new
                    {
                        statusCode = 200,
                        result = new
                        {
                            items = new List<object>(),
                            totalItemsCount = 0,
                            pageNumber,
                            totalPages = 0
                        }
                    });
                }

                return Json(new
                {
                    statusCode = 200,
                    result = new
                    {
                        items = videosForGrid,
                        totalItemsCount = videosForGrid.TotalItemsCount,
                        pageNumber = videosForGrid.PageNumber,
                        totalPages = videosForGrid.TotalPages
                    }
                });
            }
            catch (Exception ex)
            {
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
                    return Unauthorized(new { statusCode = 401, message = "User not authenticated." });
                }

                var channel = await _context.Channels
                    .Include(c => c.Videos)
                    .Include(c => c.Subscribers)
                    .FirstOrDefaultAsync(c => c.AppUserId == userId);

                if (channel == null)
                {
                    return NotFound(new { statusCode = 404, message = "Channel not found." });
                }

                var totalViews = channel.Videos.Sum(v => v.Views ?? 0);
                var subscribers = channel.Subscribers.Count;

                return Json(new { statusCode = 200, totalViews, subscribers });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = $"Error in GetTotalViews: {ex.Message}" });
            }
        }
    }
}