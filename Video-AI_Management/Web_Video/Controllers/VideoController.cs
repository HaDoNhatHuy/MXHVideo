using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.Services.IServices;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Video;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]
    public class VideoController : CoreController
    {
        private readonly IPhotoService _photoService;

        public VideoController(IPhotoService photoService)
        {
            _photoService = photoService;
        }
        public async Task<IActionResult> Watch(Guid id)
        {
            // with having DOT(.) we can have then include eg:"Channel.Subscribers"
            var fetchedVideo = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == id, "Channel.Subscribers,LikeDislikes");
            if (fetchedVideo != null)
            {
                string userId = User.GetUserId();
                var toReturn = new VideoWatchViewModel();
                toReturn.Id = fetchedVideo.Id;
                toReturn.Title = fetchedVideo.Title;
                toReturn.Description = fetchedVideo.Description;
                toReturn.CreatedAt = fetchedVideo.UploadDate ?? DateTime.Now;
                toReturn.ChannelId = fetchedVideo.ChannelId ?? Guid.Empty;
                toReturn.ChannelName = fetchedVideo.Channel.ChannelName;

                toReturn.IsSubscribed = fetchedVideo.Channel.Subscribers.Any(x => x.AppUserId == userId);
                toReturn.IsLiked = fetchedVideo.LikeDislikes.Any(x => x.AppUserId == userId && x.Liked == true);
                toReturn.IsDisiked = fetchedVideo.LikeDislikes.Any(x => x.AppUserId == userId && x.Liked == false);

                toReturn.SubscribersCount = fetchedVideo.Channel.Subscribers.Count();
                toReturn.ViewersCount = SD.GetRandomNumber(1000, 50000, fetchedVideo.Id.GetHashCode());
                toReturn.LikesCount = fetchedVideo.LikeDislikes.Where(x => x.Liked == true).Count();
                toReturn.DislikesCount = fetchedVideo.LikeDislikes.Where(x => x.Liked == false).Count();

                return View(toReturn);
            }
            TempData["notification"] = "false;Not Found;Requested video was not found";
            return RedirectToAction("Index", "Home");
        }
        public async Task<IActionResult> GetVideoFile(Guid videoId)
        {
            var fetchedVideoFile = await UnitOfWork.VideoFileRepo.GetFirstOrDefaultAsync(x => x.VideoId == videoId);
            if (fetchedVideoFile != null)
            {
                return File(fetchedVideoFile.Contents, fetchedVideoFile.ContentType);
            }
            TempData["notification"] = "false;Not Found;Requested video was not found";
            return RedirectToAction("Index", "Home");
        }
        [HttpPost]
        public async Task<IActionResult> DownloadVideoFile(Guid videoId)
        {
            var fetchedVideo = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == videoId, "VideoFile");
            if (fetchedVideo != null)
            {
                string fileDownloadName = fetchedVideo.Title + fetchedVideo.VideoFile.Extension;
                return File(fetchedVideo.VideoFile.Contents, fetchedVideo.VideoFile.ContentType, fileDownloadName);
            }
            TempData["notification"] = "false;Not Found;Requested video was not found";
            return RedirectToAction("Index", "Home");
        }
        public async Task<IActionResult> CreateEditVideo(Guid id)
        {
            if (!await UnitOfWork.ChannelRepo.AnyAsync(x => x.AppUserId == User.GetUserId()))
            {
                TempData["notification"] = "false;Not Found;No channel associated with your account was found";
                return RedirectToAction("Index", "Channel");
            }
            var toReturn = new VideoAddEditViewModel();
            toReturn.ImageContentTypes = string.Join(",", AcceptableContentTypes("image"));
            toReturn.VideoContentTypes = string.Join(",", AcceptableContentTypes("video"));

            if (id != Guid.Empty)
            {
                //edit video

                var userId = await UnitOfWork.VideoRepo.GetUserIdByVideoIdAsync(id);
                if (userId != User.GetUserId())
                {
                    TempData["notification"] = "false;Not Found;Requested video was not found";
                    return RedirectToAction("Index", "Channel");
                }
                var fetchedVideo = await UnitOfWork.VideoRepo.GetByIdAsync(id);
                if (fetchedVideo == null)
                {
                    TempData["notification"] = "false;Not Found;Requested video was not found";
                    return RedirectToAction("Index", "Channel");
                }
                toReturn.Id = fetchedVideo.Id;
                toReturn.Title = fetchedVideo.Title;
                toReturn.Description = fetchedVideo.Description;
                toReturn.CategoryId = fetchedVideo.CategoryId;
                toReturn.ImageUrl = fetchedVideo.Thumbnail;
            }

            toReturn.CategoryDropdown = await GetCategoryDropdownAsync();
            return View(toReturn);
        }
        [HttpPost]
        public async Task<IActionResult> CreateEditVideo(VideoAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool proceed = true;
                if (model.Id == Guid.Empty)
                {
                    //adding some security to check for create
                    if (model.ImageUpload == null)
                    {
                        ModelState.AddModelError("ImageUpload", "Please upload an image for your video");
                        proceed = false;
                    }
                    if (proceed && model.VideoUpload == null)
                    {
                        ModelState.AddModelError("VideoUpload", "Please upload a video for your video");
                        proceed = false;
                    }
                }
                if (model.ImageUpload != null)
                {
                    if (proceed && !IsAcceptableContentType("image", model.ImageUpload.ContentType))
                    {
                        ModelState.AddModelError("ImageUpload", string.Format("Invalid content type. It must be one of the following: {0}",
                            string.Join(", ", AcceptableContentTypes("image"))));
                        proceed = false;
                    }
                    if (proceed && model.ImageUpload.Length > int.Parse(Configuration["FileUpload:ImageMaxSizeInMB"]) * SD.MB)
                    {
                        ModelState.AddModelError("ImageUpload", string.Format("The uploaded file should not exceed {0} MB",
                            int.Parse(Configuration["FileUpload:ImageMaxSizeInMB"])));
                        proceed = false;
                    }
                }

                if (model.VideoUpload != null)
                {
                    if (proceed && !IsAcceptableContentType("video", model.VideoUpload.ContentType))
                    {
                        ModelState.AddModelError("VideoUpload", string.Format("Invalid content type. It must be one of the following: {0}",
                            string.Join(", ", AcceptableContentTypes("video"))));
                        proceed = false;
                    }
                    if (proceed && model.VideoUpload.Length > int.Parse(Configuration["FileUpload:VideoMaxSizeInMB"]) * SD.MB)
                    {
                        ModelState.AddModelError("VideoUpload", string.Format("The uploaded file should not exceed {0} MB",
                            int.Parse(Configuration["FileUpload:VideoMaxSizeInMB"])));
                        proceed = false;
                    }
                }
                if (proceed)
                {
                    string title = "";
                    string message = "";
                    if (model.Id == Guid.Empty)
                    {
                        //for create
                        var videoToAdd = new Video()
                        {
                            Id = Guid.NewGuid(),
                            Title = model.Title,
                            VideoFile = new VideoFile
                            {
                                ContentType = model.VideoUpload.ContentType,
                                Contents = GetContentsAsync(model.VideoUpload).GetAwaiter().GetResult(),
                                Extension = SD.GetFileExtension(model.VideoUpload.ContentType)
                            },
                            Description = model.Description,
                            CategoryId = model.CategoryId,
                            ChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(User.GetUserId()).GetAwaiter().GetResult(), //channelId of the current User
                            Thumbnail = _photoService.UploadPhotoLocally(model.ImageUpload), //some url that we are going to provide
                        };
                        UnitOfWork.VideoRepo.Add(videoToAdd);
                        title = "Created";
                        message = "New video has been created";
                    }
                    else
                    {
                        //for edit
                        var fetchedVideo = await UnitOfWork.VideoRepo.GetByIdAsync(model.Id);
                        if (fetchedVideo == null)
                        {
                            TempData["notification"] = "false;Not Found;Requested video was not found";
                            return RedirectToAction("Index", "Channel");
                        }
                        fetchedVideo.Title = model.Title;
                        fetchedVideo.Description = model.Description;
                        fetchedVideo.CategoryId = model.CategoryId;
                        if (model.ImageUpload != null)
                        {
                            //handle re uploading the image file
                            fetchedVideo.Thumbnail = _photoService.UploadPhotoLocally(model.ImageUpload, fetchedVideo.Thumbnail);
                        }
                        title = "Updated";
                        message = "Video has been updated";
                    }
                    TempData["notification"] = $"true;{title};{message}";
                    await UnitOfWork.CompleteAsync();
                    return RedirectToAction("Index", "Channel");
                }
            }
            model.CategoryDropdown = await GetCategoryDropdownAsync();
            return View(model);
        }

        #region API Endpoints
        [HttpGet]
        public async Task<IActionResult> GetVideosForChannelGrid(BaseParameters parameters)
        {
            var userChannelId = await UnitOfWork.ChannelRepo.GetChannelIdByUserId(User.GetUserId());
            var videosForGrid = await UnitOfWork.VideoRepo.GetVideosForChannelGridAsync(userChannelId, parameters);
            var paginatedResults = new PaginatedResult<VideoGridChannelDto>(videosForGrid, videosForGrid.TotalItemsCount,
                videosForGrid.PageNumber, videosForGrid.PageSize, videosForGrid.TotalPages);

            return Json(new ApiResponse(200, result: paginatedResults));
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteVideo(Guid id)
        {
            var video = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == id && x.Channel.AppUserId == User.GetUserId());
            if (video != null)
            {
                UnitOfWork.VideoRepo.Remove(video);
                await UnitOfWork.CompleteAsync();
                return Json(new ApiResponse(200, "Deleted", "Your video of " + video.Title + " has been deleted"));
            }
            return Json(new ApiResponse(404, message: "The requested video was not found"));
        }
        #endregion

        #region Private Methods

        public async Task<IEnumerable<SelectListItem>> GetCategoryDropdownAsync()
        {
            var allCategories = await UnitOfWork.CategoryRepo.GetAllAsync();
            return allCategories.Select(category => new SelectListItem()
            {
                Text = category.CategoryName,
                Value = category.Id.ToString()
            });
        }
        private string[] AcceptableContentTypes(string type)
        {
            if (type.Equals("image"))
            {
                return Configuration.GetSection("FileUpload:ImageContentTypes").Get<string[]>();
            }
            else
            {
                return Configuration.GetSection("FileUpload:VideoContentTypes").Get<string[]>();
            }
        }
        public bool IsAcceptableContentType(string type, string contentType)
        {
            var allowedContentTypes = AcceptableContentTypes(type);
            foreach (var allowedContentType in allowedContentTypes)
            {
                if (allowedContentType.ToLower().Equals(contentType.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<byte[]> GetContentsAsync(IFormFile file)
        {
            byte[] contents;
            using var memoryStream = new System.IO.MemoryStream();
            await file.CopyToAsync(memoryStream);
            contents = memoryStream.ToArray();
            return contents;
        }

        [HttpPut]
        public async Task<IActionResult> SubscribeChannel(Guid channelId)
        {
            var channel = await UnitOfWork.ChannelRepo.GetFirstOrDefaultAsync(x => x.Id == channelId, "Subscribers");
            if (channel != null)
            {
                string userId = User.GetUserId();
                var fetchedSubscribe = channel.Subscribers.Where(x => x.ChannelId == channelId && x.AppUserId == userId).FirstOrDefault();
                if (fetchedSubscribe == null)
                {
                    // Subscribe
                    channel.Subscribers.Add(new Subscribe(userId, channelId));
                    await UnitOfWork.CompleteAsync();
                    return Json(new ApiResponse(200, "Subscribed", "You have successfully subscribed to " + channel.ChannelName));
                }
                else
                {
                    // Unsubscribe
                    channel.Subscribers.Remove(fetchedSubscribe);
                    await UnitOfWork.CompleteAsync();
                    return Json(new ApiResponse(200, "Unsubscribed", "You have successfully unsubscribed from " + channel.ChannelName));
                }
            }
            return Json(new ApiResponse(404, message: "The requested channel was not found"));

        }
        [HttpPut]
        public async Task<IActionResult> LikeDislikeVideo(Guid videoId, string action, bool like)
        {
            var video = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == videoId, "LikeDislikes");
            if (video != null)
            {
                string userId = User.GetUserId();
                var fetchedLikeDislike = video.LikeDislikes.Where(x => x.VideoId == videoId && x.AppUserId == userId).FirstOrDefault();
                string clientCommand = "";
                if (action.Equals("like"))
                {
                    if (fetchedLikeDislike == null)
                    {
                        //Adding LikeDislike with value of Like= true
                        video.LikeDislikes.Add(new LikeDislike(videoId, userId, true));
                        await UnitOfWork.CompleteAsync();
                        clientCommand = "addLike";
                        return Json(new ApiResponse(200, clientCommand));
                    }
                    else
                    {
                        // the user has whether liked or disliked previously and we need to update the value
                        if (fetchedLikeDislike.Liked == false)
                        {
                            // User was previously disliked the video and now decided to like the video so Liked becomes true
                            fetchedLikeDislike.Liked = true;
                            clientCommand = "removeDislike-addLike";
                        }
                        else
                        {
                            // User was previously liked the video and now decided to not to like the video and still not Dislike the video
                            // so remove LikeDislike from the database
                            video.LikeDislikes.Remove(fetchedLikeDislike);
                            clientCommand = "removeLike";
                        }
                        await UnitOfWork.CompleteAsync();
                        return Json(new ApiResponse(200, clientCommand));
                    }
                }
                else if (action.Equals("dislike"))
                {
                    if (fetchedLikeDislike == null)
                    {
                        //Adding LikeDislike with value of Like= false
                        video.LikeDislikes.Add(new LikeDislike(videoId, userId, false));
                        await UnitOfWork.CompleteAsync();
                        clientCommand = "addDislike";
                        return Json(new ApiResponse(200, clientCommand));
                    }
                    else
                    {
                        // the user has whether liked or disliked previously and we need to update the value
                        if (fetchedLikeDislike.Liked == true)
                        {
                            // User was previously liked the video and now decided to dislike the video so Liked becomes false
                            fetchedLikeDislike.Liked = false;
                            clientCommand = "removeLike-addDislike";
                        }
                        else
                        {
                            // User was previously disliked the video and now decided to not to dislike the video and still not Like the video
                            // so remove LikeDislike from the database
                            video.LikeDislikes.Remove(fetchedLikeDislike);
                            clientCommand = "removeDislike";
                        }
                        await UnitOfWork.CompleteAsync();
                        return Json(new ApiResponse(200, clientCommand));
                    }
                }
                else
                {
                    return Json(new ApiResponse(400, message: "Invalid action"));
                }
            }
            return Json(new ApiResponse(404, message: "The requested video was not found"));

        }
        #endregion
    }
}
