using Database_Video.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.Services.IServices;
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

                var userId = await UnitOfWork.VideoRepo.GetUserIdByVideoId(id);
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
                            ContentType = model.VideoUpload.ContentType,
                            Contents = GetContentsAsync(model.VideoUpload).GetAwaiter().GetResult(),
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
        #endregion
    }
}
