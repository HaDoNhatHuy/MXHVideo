using DataAccess.Data;
using Database_Video.Entities;
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

        public ChannelController(DataContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index(string stringModel)
        {
            ViewData["CurrentPage"] = "Channel";
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
            var channel = await UnitOfWork.ChannelRepo.GetFirstOrDefaultAsync(x => x.AppUserId == User.GetUserId(), includeProperties: "Subscribers");
            if (channel != null)
            {
                model.Name = channel.ChannelName;
                model.About = channel.About;
                model.CreatedDate = channel.CreatedDate ?? DateTime.UtcNow;
                model.AvatarUrl = channel.ChannelPicture;
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
                HttpContext.Session.SetString("ChannelModelFromSession", JsonConvert.SerializeObject(model));
                return RedirectToAction("Index");
            }

            var channelToAdd = new Channel
            {
                AppUserId = User.GetUserId(),
                ChannelName = model.Name,
                About = model.About,
                ChannelPicture = "/avatarUser/avt-default.jpg" // Ảnh mặc định nếu không upload
            };

            // Xử lý upload ảnh đại diện kênh
            if (model.Avatar != null && model.Avatar.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/avatarUser");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.Avatar.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Avatar.CopyToAsync(fileStream);
                }

                channelToAdd.ChannelPicture = "/avatarUser/" + uniqueFileName; // Lưu đường dẫn ảnh
            }

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

                    // Xử lý upload ảnh đại diện kênh
                    if (model.Avatar != null && model.Avatar.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/avatarUser");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.Avatar.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.Avatar.CopyToAsync(fileStream);
                        }

                        // Xóa ảnh cũ nếu không phải ảnh mặc định
                        if (!string.IsNullOrEmpty(channel.ChannelPicture) && channel.ChannelPicture != "/avatarUser/avt-default.jpg")
                        {
                            var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", channel.ChannelPicture.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        channel.ChannelPicture = "/avatarUser/" + uniqueFileName; // Cập nhật đường dẫn ảnh mới
                    }

                    await UnitOfWork.CompleteAsync();

                    TempData["notification"] = "true;Channel updated; Your channel has been updated";
                    return RedirectToAction("Index");
                }
            }
            TempData["notification"] = "false;Channel not found; Your channel was not found";
            return RedirectToAction("Index");
        }
        [HttpGet]
        public async Task<IActionResult> GetAnalytics(string timeFilter)
        {
            // Xác định khoảng thời gian
            DateTime startDate;
            int groupByDays;
            switch (timeFilter?.ToLower())
            {
                case "7":
                    startDate = DateTime.UtcNow.AddDays(-7);
                    groupByDays = 1; // Nhóm theo ngày
                    break;
                case "28":
                    startDate = DateTime.UtcNow.AddDays(-28);
                    groupByDays = 4; // Nhóm theo 4 ngày
                    break;
                case "90":
                    startDate = DateTime.UtcNow.AddDays(-90);
                    groupByDays = 10; // Nhóm theo 10 ngày
                    break;
                case "all":
                default:
                    startDate = DateTime.MinValue; // Lấy tất cả dữ liệu
                    groupByDays = 30; // Nhóm mặc định theo tháng cho dữ liệu lớn
                    break;
            }

            // Lấy channelId từ user hiện tại
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not authenticated.");
            }

            var channelId = await UnitOfWork.ChannelRepo.GetChannelIdByUserId(userId);
            if (channelId == Guid.Empty)
            {
                return NotFound("Channel not found for the user.");
            }

            // Truy vấn video theo channel và khoảng thời gian
            var videosQuery = _context.Videos
                .Where(v => v.ChannelId == channelId && v.UploadDate >= startDate);
            // Tính tổng lượt xem
            //var totalViews = await videosQuery.SumAsync(v => v.Views ?? 0);
            var totalViews = _context.VideoViews
                .Where(v => v.AppUserId == userId)
                .Count();
            // Tính tổng lượt thích
            var totalLikes = await videosQuery
                .SelectMany(v => v.LikeDislikes)
                .CountAsync(ld => ld.Liked == true);
            // Tính tổng bình luận
            var totalComments = await videosQuery
                .SelectMany(v => v.Comments)
                .CountAsync();
            //// Tính tổng lượt xem, lượt thích, bình luận
            //var analytics = await videosQuery
            //    .GroupBy(_ => 1)
            //    .Select(g => new
            //    {
            //        TotalViews = g.Sum(v => v.Views ?? 0),
            //        TotalLikes = g.Sum(v => v.LikeDislikes.Count(ld => ld.Liked == true)),
            //        TotalComments = g.Sum(v => v.Comments.Count())
            //    })
            //    .FirstOrDefaultAsync();

            // Lấy số lượng subscribers
            var subscribers = await _context.Channels
                .Where(c => c.Id == channelId)
                .Select(c => c.Subscribers.Count())
                .FirstOrDefaultAsync();

            // Dữ liệu cho biểu đồ Views Over Time
            var viewsOverTime = new
            {
                Labels = new List<string>(),
                Data = new List<int>()
            };

            if (timeFilter != "all")
            {
                // Nhóm theo ngày cố định
                var endDate = DateTime.UtcNow.Date;
                for (var date = startDate.Date; date <= endDate; date = date.AddDays(groupByDays))
                {
                    var nextDate = date.AddDays(groupByDays);
                    viewsOverTime.Labels.Add(date.ToString("MMM d"));
                    viewsOverTime.Data.Add(await videosQuery
                        .Where(v => v.UploadDate.Date >= date && v.UploadDate.Date < nextDate)
                        .SumAsync(v => v.Views ?? 0));
                }
            }
            else
            {
                // Nhóm động cho "all"
                var firstVideoDate = await videosQuery.MinAsync(v => (DateTime?)v.UploadDate) ?? DateTime.UtcNow;
                var totalDays = (DateTime.UtcNow.Date - firstVideoDate.Date).Days;
                groupByDays = Math.Max(1, totalDays / 10); // Đảm bảo tối đa 10 điểm dữ liệu

                for (var i = totalDays; i >= 0; i -= groupByDays)
                {
                    var date = DateTime.UtcNow.AddDays(-i).Date;
                    var nextDate = date.AddDays(groupByDays);
                    viewsOverTime.Labels.Add(date.ToString("MMM d"));
                    viewsOverTime.Data.Add(await videosQuery
                        .Where(v => v.UploadDate.Date >= date && v.UploadDate.Date < nextDate)
                        .SumAsync(v => v.Views ?? 0));
                }
            }

            // Dữ liệu cho biểu đồ Traffic Sources (giả lập hoặc lấy từ nguồn thực nếu có)
            var trafficSources = new
            {
                Labels = new[] { "Direct", "Search", "External", "Social" },
                Data = new[] { 40, 30, 20, 10 } // Có thể thay bằng dữ liệu thực từ DB
            };

            // Kết quả trả về
            var result = new
            {
                TotalViews = totalViews,
                Subscribers = subscribers,
                Likes = totalLikes,
                Comments = totalComments,
                ViewsOverTime = viewsOverTime,
                TrafficSources = trafficSources
            };

            return Json(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetTotalViews()
        {
            var userId = User.GetUserId();
            var totalViews = _context.VideoViews
                .Where(v => v.AppUserId == userId)
                .Count();
            return Json(new { totalViews });
        }
    }
}