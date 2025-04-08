using Database_Video.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Member;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]
    public class MemberController : CoreController
    {
        public async Task<IActionResult> Channel(Guid id)
        {
            var fetchedChannel = await Context.Channels
                .Where(x => x.Id == id)
                .Select(x => new MemberChannelViewModel
                {
                    ChannelId = x.Id,
                    Name = x.ChannelName,
                    About = x.About,
                    CreatedAt = x.CreatedDate ?? DateTime.UtcNow,
                    NumberOfAvailableVideos = x.Videos.Count(),
                    NumberOfSubscribers = x.Subscribers.Count(),
                    UserIsSubscribed = x.Subscribers.Any(s => s.AppUserId == User.GetUserId()),
                }).FirstOrDefaultAsync();

            if (fetchedChannel != null)
            {
                return View(fetchedChannel);
            }

            TempData["notification"] = "false;Not Found;Requested channel was not found";
            return RedirectToAction("Index", "Home");
        }
        [HttpPost]
        public async Task<IActionResult> SubscribeChannel(Guid channelId)
        {
            var channel = await UnitOfWork.ChannelRepo.GetFirstOrDefaultAsync(x => x.Id == channelId, "Subscribers");

            if (channel != null)
            {
                var userId = User.GetUserId();

                var fetchedSubscribe = channel.Subscribers.Where(x => x.ChannelId == channelId && x.AppUserId == userId).FirstOrDefault();

                if (fetchedSubscribe == null)
                {
                    // Subscribe
                    channel.Subscribers.Add(new Subscribe(userId, channelId));
                }
                else
                {
                    // Unsubscribe
                    channel.Subscribers.Remove(fetchedSubscribe);
                }

                await UnitOfWork.CompleteAsync();
                return RedirectToAction("Channel", new { id = channelId });
            }

            TempData["notification"] = "false;Not Found;Requested channel was not found";
            return RedirectToAction("Index", "Home");

        }
        #region API Endpoints

        [HttpGet]
        public async Task<IActionResult> GetMemberChannelVideos(Guid channelId)
        {
            var channelVideos = await Context.Videos
             .Where(x => x.ChannelId == channelId)
             .Select(x => new
             {
                 x.Id,
                 x.Title,
                 x.Thumbnail,
                 CreatedAtTimeAgo = SD.TimeAgo(x.UploadDate),
                 x.UploadDate,
                 NumberOfViews = x.Viewers.Count(),
             })
             .ToListAsync();

            return Json(new ApiResponse(200, result: channelVideos));

        }
        #endregion
    }
}
