using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Video;
using WebVideo.Utility;
using Xabe.FFmpeg;
using static Web_Video.ViewModels.Video.VideoWatchViewModel;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]
    public class VideoController : CoreController
    {
        public async Task<IActionResult> Watch(Guid id)
        {
            // inefficient way of fetching the videos with lots of include properties with unecessary columns
            //var toReturn = await GetVideoWatch_VMWithIncludeProperties(id);

            // efficient way of fetching the video from the database and only takes the column that we are interested in the query
            var toReturn = await GetVideoWatch_VMWithProjections(id);

            if (toReturn != null)
            {
                // Lấy danh sách video đề xuất
                toReturn.RecommendedVideos = await GetRecommendedVideos(id);

                var userIpAddress = Request.HttpContext.Connection.RemoteIpAddress.ToString();
                await UnitOfWork.VideoViewRepo.HandleVideoViewAsync(User.GetUserId(), id, userIpAddress);
                await UnitOfWork.CompleteAsync();

                return View(toReturn);
            }
            TempData["notification"] = "false;Not Found;Requested video was not found";
            return RedirectToAction("Index", "Home");
        }

        //[HttpPost]
        //public async Task<IActionResult> CreateComment(CommentViewModel model)
        //{
        //    var video = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == model.PostComment.VideoId, "Comments");
        //    if (video != null)
        //    {
        //        video.Comments.Add(new Comment(model.PostComment.VideoId, User.GetUserId(), model.PostComment.Content.Trim()));
        //        await UnitOfWork.CompleteAsync();
        //        return RedirectToAction("Watch", new { id = model.PostComment.VideoId });
        //    }
        //    TempData["notification"] = "false;Not Found;Requested video was not found";
        //    return RedirectToAction("Index", "Home");
        //}
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> EditComment(Guid commentId, Guid videoId, string content)
        //{
        //    if (commentId == Guid.Empty || videoId == Guid.Empty)
        //    {
        //        TempData["notification"] = "false;Invalid;Invalid comment or video ID";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }

        //    if (string.IsNullOrWhiteSpace(content))
        //    {
        //        TempData["notification"] = "false;Invalid;Comment content cannot be empty";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }
        //    var comment = await UnitOfWork.CommentRepo.GetFirstOrDefaultAsync(c => c.Id == commentId);
        //    if (comment == null)
        //    {
        //        TempData["notification"] = "false;Not Found;Comment not found";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }

        //    // Kiểm tra quyền: Chỉ người tạo comment mới được sửa
        //    if (comment.AppUserId != User.GetUserId())
        //    {
        //        TempData["notification"] = "false;Unauthorized;You are not authorized to edit this comment";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }

        //    // Cập nhật nội dung comment
        //    comment.Content = content.Trim();
        //    comment.ModifiedDate = DateTime.Now;
        //    comment.ModifiedBy = User.GetUserId();

        //    await UnitOfWork.CompleteAsync();
        //    TempData["notification"] = "true;Success;Comment updated successfully";
        //    return RedirectToAction("Watch", new { id = videoId });
        //}

        //[HttpPost]
        //public async Task<IActionResult> DeleteComment(Guid commentId, Guid videoId)
        //{
        //    var comment = await UnitOfWork.CommentRepo.GetFirstOrDefaultAsync(c => c.Id == commentId);
        //    if (comment == null)
        //    {
        //        TempData["notification"] = "false;Not Found;Comment not found";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }

        //    // Kiểm tra quyền: Chỉ người tạo comment mới được xóa
        //    if (comment.AppUserId != User.GetUserId())
        //    {
        //        TempData["notification"] = "false;Unauthorized;You are not authorized to delete this comment";
        //        return RedirectToAction("Watch", new { id = videoId });
        //    }

        //    UnitOfWork.CommentRepo.Remove(comment);
        //    await UnitOfWork.CompleteAsync();
        //    TempData["notification"] = "true;Success;Comment deleted successfully";
        //    return RedirectToAction("Watch", new { id = videoId });
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateComment(CommentViewModel model)
        {
            if (model.PostComment == null || model.PostComment.VideoId == Guid.Empty)
            {
                return Json(new { isSuccess = false, title = "Invalid", message = "Invalid video ID" });
            }

            if (string.IsNullOrWhiteSpace(model.PostComment.Content))
            {
                return Json(new { isSuccess = false, title = "Invalid", message = "Comment content cannot be empty" });
            }

            var video = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == model.PostComment.VideoId, "Comments");
            if (video == null)
            {
                return Json(new { isSuccess = false, title = "Not Found", message = "Requested video was not found" });
            }

            var newComment = new Comment(model.PostComment.VideoId, User.GetUserId(), model.PostComment.Content.Trim());
            video.Comments.Add(newComment);
            await UnitOfWork.CompleteAsync();

            return Json(new
            {
                isSuccess = true,
                title = "Success",
                message = "Comment added successfully",
                comment = new
                {
                    id = newComment.Id,
                    content = newComment.Content,
                    postedAt = newComment.CreatedDate,
                    //fromName = User.Identity.Name, // Tên người dùng (có thể cần điều chỉnh)
                    fromName = User.GetFullName(),
                    //fromChannelId = Guid.Empty, // Điều chỉnh nếu bạn có logic lấy channel ID
                    fromChannelId = User.GetUserChannelId(),
                    appUserId = newComment.AppUserId
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(Guid commentId, Guid videoId, string content)
        {
            if (commentId == Guid.Empty || videoId == Guid.Empty)
            {
                return Json(new { isSuccess = false, title = "Invalid", message = "Invalid comment or video ID" });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { isSuccess = false, title = "Invalid", message = "Comment content cannot be empty" });
            }

            var comment = await UnitOfWork.CommentRepo.GetFirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
            {
                return Json(new { isSuccess = false, title = "Not Found", message = "Comment not found" });
            }

            if (comment.AppUserId != User.GetUserId())
            {
                return Json(new { isSuccess = false, title = "Unauthorized", message = "You are not authorized to edit this comment" });
            }

            comment.Content = content.Trim();
            comment.ModifiedDate = DateTime.Now;
            comment.ModifiedBy = User.GetUserId();

            await UnitOfWork.CompleteAsync();
            return Json(new { isSuccess = true, title = "Success", message = "Comment updated successfully" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(Guid commentId, Guid videoId)
        {
            var comment = await UnitOfWork.CommentRepo.GetFirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
            {
                return Json(new { isSuccess = false, title = "Not Found", message = "Comment not found" });
            }

            if (comment.AppUserId != User.GetUserId())
            {
                return Json(new { isSuccess = false, title = "Unauthorized", message = "You are not authorized to delete this comment" });
            }

            UnitOfWork.CommentRepo.Remove(comment);
            await UnitOfWork.CompleteAsync();
            return Json(new { isSuccess = true, title = "Success", message = "Comment deleted successfully" });
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
        //[HttpPost]
        //public async Task<IActionResult> CreateEditVideo(VideoAddEditViewModel model)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        bool proceed = true;
        //        if (model.Id == Guid.Empty)
        //        {
        //            if (model.ImageUpload == null)
        //            {
        //                ModelState.AddModelError("ImageUpload", "Please upload an image for your video");
        //                proceed = false;
        //            }
        //            if (proceed && model.VideoUpload == null)
        //            {
        //                ModelState.AddModelError("VideoUpload", "Please upload a video for your video");
        //                proceed = false;
        //            }
        //        }
        //        if (model.ImageUpload != null)
        //        {
        //            if (proceed && !IsAcceptableContentType("image", model.ImageUpload.ContentType))
        //            {
        //                ModelState.AddModelError("ImageUpload", string.Format("Invalid content type. It must be one of the following: {0}",
        //                    string.Join(", ", AcceptableContentTypes("image"))));
        //                proceed = false;
        //            }
        //            if (proceed && model.ImageUpload.Length > int.Parse(Configuration["FileUpload:ImageMaxSizeInMB"]) * SD.MB)
        //            {
        //                ModelState.AddModelError("ImageUpload", string.Format("The uploaded file should not exceed {0} MB",
        //                    int.Parse(Configuration["FileUpload:ImageMaxSizeInMB"])));
        //                proceed = false;
        //            }
        //        }

        //        if (model.VideoUpload != null)
        //        {
        //            if (proceed && !IsAcceptableContentType("video", model.VideoUpload.ContentType))
        //            {
        //                ModelState.AddModelError("VideoUpload", string.Format("Invalid content type. It must be one of the following: {0}",
        //                    string.Join(", ", AcceptableContentTypes("video"))));
        //                proceed = false;
        //            }
        //            if (proceed && model.VideoUpload.Length > int.Parse(Configuration["FileUpload:VideoMaxSizeInMB"]) * SD.MB)
        //            {
        //                ModelState.AddModelError("VideoUpload", string.Format("The uploaded file should not exceed {0} MB",
        //                    int.Parse(Configuration["FileUpload:VideoMaxSizeInMB"])));
        //                proceed = false;
        //            }
        //        }
        //        if (proceed)
        //        {
        //            string title = "";
        //            string message = "";
        //            Video videoToAdd = null;
        //            if (model.Id == Guid.Empty)
        //            {
        //                // Lưu video tạm thời để xử lý nhận diện khuôn mặt
        //                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
        //                Directory.CreateDirectory(uploadPath);
        //                var videoPath = Path.Combine(uploadPath, model.VideoUpload.FileName);
        //                using (var stream = new FileStream(videoPath, FileMode.Create))
        //                {
        //                    await model.VideoUpload.CopyToAsync(stream);
        //                }

        //                // Nhận diện khuôn mặt
        //                string recognitionResult = await ProcessVideo(videoPath);

        //                // Tạo video mới
        //                videoToAdd = new Video()
        //                {
        //                    Id = Guid.NewGuid(),
        //                    Title = model.Title,
        //                    VideoFile = new VideoFile
        //                    {
        //                        ContentType = model.VideoUpload.ContentType,
        //                        Contents = GetContentsAsync(model.VideoUpload).GetAwaiter().GetResult(),
        //                        Extension = SD.GetFileExtension(model.VideoUpload.ContentType)
        //                    },
        //                    Description = model.Description,
        //                    CategoryId = model.CategoryId,
        //                    ChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(User.GetUserId()).GetAwaiter().GetResult(),
        //                    Thumbnail = PhotoService.UploadPhotoLocally(model.ImageUpload),
        //                    RecognizedCelebrities = recognitionResult
        //                };
        //                UnitOfWork.VideoRepo.Add(videoToAdd);
        //                title = "Created";
        //                message = "New video has been created";
        //            }
        //            else
        //            {
        //                var fetchedVideo = await UnitOfWork.VideoRepo.GetByIdAsync(model.Id);
        //                if (fetchedVideo == null)
        //                {
        //                    TempData["notification"] = "false;Not Found;Requested video was not found";
        //                    return RedirectToAction("Index", "Channel");
        //                }
        //                fetchedVideo.Title = model.Title;
        //                fetchedVideo.Description = model.Description;
        //                fetchedVideo.CategoryId = model.CategoryId;
        //                if (model.ImageUpload != null)
        //                {
        //                    fetchedVideo.Thumbnail = PhotoService.UploadPhotoLocally(model.ImageUpload, fetchedVideo.Thumbnail);
        //                }
        //                title = "Updated";
        //                message = "Video has been updated";
        //            }
        //            await UnitOfWork.CompleteAsync();

        //            TempData["notification"] = $"true;{title};{message}";
        //            return RedirectToAction("Index", "Channel");
        //        }
        //    }

        //    // Nếu không hợp lệ, trả về view với lỗi
        //    model.CategoryDropdown = await GetCategoryDropdownAsync();
        //    return View(model);
        //}
        //// Thêm các phương thức xử lý nhận diện khuôn mặt từ HomeController.cs
        //private async Task<string> ProcessVideo(string videoPath)
        //{
        //    var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/frames");
        //    Directory.CreateDirectory(outputDir);

        //    var oldFrames = Directory.GetFiles(outputDir, "frame-*.jpg");
        //    foreach (var oldFrame in oldFrames)
        //    {
        //        System.IO.File.Delete(oldFrame);
        //    }

        //    var outputImage = Path.Combine(outputDir, "frame-%03d.jpg");
        //    Xabe.FFmpeg.FFmpeg.SetExecutablesPath(@"C:\FFmpeg\ffmpeg\bin");

        //    var conversion = FFmpeg.Conversions.New()
        //        .AddParameter($"-i \"{videoPath}\" -vf fps=1,scale=640:-1 \"{outputImage}\"")
        //        .SetOverwriteOutput(true);
        //    await conversion.Start();

        //    var frames = Directory.GetFiles(outputDir, "frame-*.jpg");
        //    if (frames.Length == 0)
        //    {
        //        return "Không thể trích xuất frame từ video.";
        //    }

        //    string recognitionResult = await RecognizeCelebrity(frames);
        //    return recognitionResult;
        //}

        //private async Task<string> RecognizeCelebrity(string[] frames)
        //{
        //    var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/"), Timeout = TimeSpan.FromMinutes(5) };
        //    HashSet<string> allCelebrities = new HashSet<string>();

        //    foreach (var frame in frames)
        //    {
        //        var requestBody = new { frame_path = frame };
        //        var response = await client.PostAsJsonAsync("recognize", requestBody);
        //        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();

        //        var celebrities = result["celebrities"];
        //        if (celebrities != null && celebrities.Length > 0 && celebrities[0] != "Unknown")
        //        {
        //            foreach (var celeb in celebrities)
        //            {
        //                allCelebrities.Add(celeb);
        //            }
        //        }
        //    }

        //    return allCelebrities.Count > 0
        //        ? $"Đã nhận diện: {string.Join(", ", allCelebrities)}"
        //        : "Không nhận diện được nhân vật nổi tiếng.";
        //}
        [HttpPost]
        public async Task<IActionResult> CreateEditVideo(VideoAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool proceed = true;
                if (model.Id == Guid.Empty)
                {
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
                    Video videoToAdd = null;
                    if (model.Id == Guid.Empty)
                    {
                        // Lưu video tạm thời để xử lý nhận diện khuôn mặt
                        var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                        Directory.CreateDirectory(uploadPath);
                        var videoPath = Path.Combine(uploadPath, model.VideoUpload.FileName);
                        using (var stream = new FileStream(videoPath, FileMode.Create))
                        {
                            await model.VideoUpload.CopyToAsync(stream);
                        }

                        // Nhận diện khuôn mặt
                        string recognitionResult = await ProcessVideo(videoPath);

                        // Tạo video mới
                        videoToAdd = new Video()
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
                            ChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(User.GetUserId()).GetAwaiter().GetResult(),
                            Thumbnail = PhotoService.UploadPhotoLocally(model.ImageUpload),
                            RecognizedCelebrities = recognitionResult

                        };

                        // Lưu người nổi tiếng vào bảng Celebrity và liên kết với video
                        await SaveRecognizedCelebrities(videoToAdd, recognitionResult);

                        UnitOfWork.VideoRepo.Add(videoToAdd);
                        title = "Created";
                        message = "New video has been created";
                    }
                    else
                    {
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
                            fetchedVideo.Thumbnail = PhotoService.UploadPhotoLocally(model.ImageUpload, fetchedVideo.Thumbnail);
                        }
                        title = "Updated";
                        message = "Video has been updated";
                    }
                    await UnitOfWork.CompleteAsync();

                    TempData["notification"] = $"true;{title};{message}";
                    return RedirectToAction("Index", "Channel");
                }
            }

            // Nếu không hợp lệ, trả về view với lỗi
            model.CategoryDropdown = await GetCategoryDropdownAsync();
            return View(model);
        }
        // Phương thức lưu người nổi tiếng vào bảng Celebrity và liên kết với video
        private async Task SaveRecognizedCelebrities(Video video, string recognitionResult)
        {
            if (string.IsNullOrEmpty(recognitionResult) || recognitionResult.Contains("Không nhận diện được"))
            {
                return;
            }

            // Tách danh sách người nổi tiếng từ chuỗi recognitionResult
            var celebrityNames = recognitionResult
                .Replace("Đã nhận diện: ", "")
                .Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToList();

            foreach (var name in celebrityNames)
            {
                // Kiểm tra xem người nổi tiếng đã tồn tại trong bảng Celebrity chưa
                var celebrity = await UnitOfWork.CelebrityRepo
                    .GetFirstOrDefaultAsync(c => c.Name.ToLower() == name.ToLower());

                if (celebrity == null)
                {
                    // Nếu chưa tồn tại, tạo mới
                    celebrity = new Celebrity
                    {
                        Name = name,
                        // Các thông tin khác như Age, Gender, Job có thể được thêm sau nếu có dữ liệu từ API
                    };
                    UnitOfWork.CelebrityRepo.Add(celebrity);
                    await UnitOfWork.CompleteAsync(); // Lưu để có Id cho celebrity
                }

                // Liên kết người nổi tiếng với video qua bảng RecognizeCelebrities
                var recognizeCelebrity = new RecognizeCelebrities
                {
                    VideoId = video.Id,
                    CelebrityId = celebrity.Id
                };
                video.RecognizeCelebrities.Add(recognizeCelebrity);
            }
        }

        // Giữ nguyên các phương thức ProcessVideo và RecognizeCelebrity
        private async Task<string> ProcessVideo(string videoPath)
        {
            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/frames");
            Directory.CreateDirectory(outputDir);

            var oldFrames = Directory.GetFiles(outputDir, "frame-*.jpg");
            foreach (var oldFrame in oldFrames)
            {
                System.IO.File.Delete(oldFrame);
            }

            var outputImage = Path.Combine(outputDir, "frame-%03d.jpg");
            Xabe.FFmpeg.FFmpeg.SetExecutablesPath(@"C:\FFmpeg\ffmpeg\bin");

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-i \"{videoPath}\" -vf fps=1,scale=640:-1 \"{outputImage}\"")
                .SetOverwriteOutput(true);
            await conversion.Start();

            var frames = Directory.GetFiles(outputDir, "frame-*.jpg");
            if (frames.Length == 0)
            {
                return "Không thể trích xuất frame từ video.";
            }

            string recognitionResult = await RecognizeCelebrity(frames);
            return recognitionResult;
        }

        private async Task<string> RecognizeCelebrity(string[] frames)
        {
            var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/"), Timeout = TimeSpan.FromMinutes(5) };
            HashSet<string> allCelebrities = new HashSet<string>();

            foreach (var frame in frames)
            {
                var requestBody = new { frame_path = frame };
                var response = await client.PostAsJsonAsync("recognize", requestBody);
                var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();

                var celebrities = result["celebrities"];
                if (celebrities != null && celebrities.Length > 0 && celebrities[0] != "Unknown")
                {
                    foreach (var celeb in celebrities)
                    {
                        allCelebrities.Add(celeb);
                    }
                }
            }

            return allCelebrities.Count > 0
                ? $"Đã nhận diện: {string.Join(", ", allCelebrities)}"
                : "Không nhận diện được nhân vật nổi tiếng.";
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
            var video = await Context.Videos
                .Where(x => x.Id == id && x.Channel.AppUserId == User.GetUserId())
                .Select(x => new
                {
                    x.Id,
                    x.Thumbnail,
                    x.Title
                }).FirstOrDefaultAsync();
            //var video = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == id && x.Channel.AppUserId == User.GetUserId());
            if (video != null)
            {
                PhotoService.DeletePhotoLocally(video.Thumbnail);
                await UnitOfWork.VideoRepo.RemoveVideoAsync(video.Id);
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
        private async Task<VideoWatchViewModel> GetVideoWatch_VMWithIncludeProperties(Guid id)
        {
            // with having DOT(.) we can have then include eg:"Channel.Subscribers"
            var fetchedVideo = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == id, "Channel.Subscribers,LikeDislikes,Comments.AppUser,Viewers");
            if (fetchedVideo != null)
            {
                string userId = User.GetUserId();
                var toReturn = new VideoWatchViewModel();
                toReturn.Id = fetchedVideo.Id;
                toReturn.Title = fetchedVideo.Title;
                toReturn.Description = fetchedVideo.Description;
                toReturn.CreatedAt = fetchedVideo.UploadDate;
                toReturn.ChannelId = fetchedVideo.ChannelId ?? Guid.Empty;
                toReturn.ChannelName = fetchedVideo.Channel.ChannelName;

                toReturn.IsSubscribed = fetchedVideo.Channel.Subscribers.Any(x => x.AppUserId == userId);
                toReturn.IsLiked = fetchedVideo.LikeDislikes.Any(x => x.AppUserId == userId && x.Liked == true);
                toReturn.IsDisiked = fetchedVideo.LikeDislikes.Any(x => x.AppUserId == userId && x.Liked == false);

                toReturn.SubscribersCount = fetchedVideo.Channel.Subscribers.Count();
                toReturn.ViewersCount = fetchedVideo.Viewers.Select(x => x.NumberOfVisit).Sum();
                toReturn.LikesCount = fetchedVideo.LikeDislikes.Where(x => x.Liked == true).Count();
                toReturn.DislikesCount = fetchedVideo.LikeDislikes.Where(x => x.Liked == false).Count();

                toReturn.CommentVM.PostComment.VideoId = id;
                toReturn.CommentVM.AvailableComments = fetchedVideo.Comments.Select(x => new AvailableCommentViewModel
                {
                    FromName = x.AppUser.FullName,
                    FromChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(x.AppUserId).GetAwaiter().GetResult(),
                    PostedAt = x.CreatedDate ?? DateTime.UtcNow,
                    Content = x.Content
                });
                return toReturn;
            }
            return null;
        }
        //private async Task<VideoWatchViewModel> GetVideoWatch_VMWithProjections(Guid id)
        //{
        //    string userId = User.GetUserId();
        //    var toReturn = await Context.Videos
        //        .Where(x => x.Id == id)
        //        .Select(x => new VideoWatchViewModel
        //        {
        //            Id = x.Id,
        //            Title = x.Title,
        //            Description = x.Description,
        //            CreatedAt = x.UploadDate,
        //            ChannelId = x.ChannelId ?? Guid.Empty,
        //            ChannelName = x.Channel.ChannelName,
        //            IsSubscribed = x.Channel.Subscribers.Any(s => s.AppUserId == userId),
        //            IsLiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == true),
        //            IsDisiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == false),
        //            SubscribersCount = x.Channel.Subscribers.Count(),
        //            ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
        //            LikesCount = x.LikeDislikes.Where(l => l.Liked == true).Count(),
        //            DislikesCount = x.LikeDislikes.Where(l => l.Liked == false).Count(),
        //            VideoContentType = x.VideoFile.ContentType, // Thêm ContentType
        //            RecognizedCelebrities = x.RecognizedCelebrities, // Thêm RecognizedCelebrities vào ViewModel
        //            CommentVM = new CommentViewModel
        //            {
        //                PostComment = new CommentPostViewModel
        //                {
        //                    VideoId = x.Id,
        //                },
        //                AvailableComments = x.Comments.Select(c => new AvailableCommentViewModel
        //                {
        //                    Id = c.Id,
        //                    AppUserId = c.AppUserId,
        //                    FromName = c.AppUser.FullName,
        //                    FromChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(c.AppUserId).GetAwaiter().GetResult(),
        //                    PostedAt = c.CreatedDate ?? DateTime.UtcNow,
        //                    ModifiedDate = c.ModifiedDate,
        //                    Content = c.Content
        //                })
        //            }
        //        }).FirstOrDefaultAsync();
        //    return toReturn;
        //}

        private async Task<VideoWatchViewModel> GetVideoWatch_VMWithProjections(Guid id)
        {
            string userId = User.GetUserId();
            var toReturn = await Context.Videos
                .Where(x => x.Id == id)
                .Select(x => new VideoWatchViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Description = x.Description,
                    CreatedAt = x.UploadDate,
                    ChannelId = x.ChannelId ?? Guid.Empty,
                    ChannelName = x.Channel.ChannelName,
                    ChannelAvatar = x.Channel.ChannelPicture ?? "/avatarUser/avt-default.jpg", // Giả sử cột Avatar, mặc định nếu null
                    IsSubscribed = x.Channel.Subscribers.Any(s => s.AppUserId == userId),
                    IsLiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == true),
                    IsDisiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == false),
                    SubscribersCount = x.Channel.Subscribers.Count(),
                    ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    LikesCount = x.LikeDislikes.Where(l => l.Liked == true).Count(),
                    DislikesCount = x.LikeDislikes.Where(l => l.Liked == false).Count(),
                    VideoContentType = x.VideoFile.ContentType,
                    RecognizedCelebrities = x.RecognizedCelebrities,
                    CommentVM = new CommentViewModel
                    {
                        PostComment = new CommentPostViewModel
                        {
                            VideoId = x.Id,
                        },
                        AvailableComments = x.Comments
                            .OrderByDescending(c => c.CreatedDate)
                            .Take(5) // Giới hạn 5 bình luận ban đầu
                            .Select(c => new AvailableCommentViewModel
                            {
                                Id = c.Id,
                                AppUserId = c.AppUserId,
                                FromName = c.AppUser.FullName,
                                FromChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(c.AppUserId).GetAwaiter().GetResult(),
                                PostedAt = c.CreatedDate ?? DateTime.UtcNow,
                                ModifiedDate = c.ModifiedDate,
                                Content = c.Content
                            })
                    }
                }).FirstOrDefaultAsync();
            return toReturn;
        }

        // Phương thức để lấy danh sách video đề xuất
        //private async Task<List<RecommendedVideoViewModel>> GetRecommendedVideos(Guid currentVideoId)
        //{
        //    return await Context.Videos
        //        .Where(x => x.Id != currentVideoId) // Không lấy video hiện tại
        //        .OrderBy(x => Guid.NewGuid()) // Sắp xếp ngẫu nhiên
        //        .Take(10) // Lấy 5 video
        //        .Select(x => new RecommendedVideoViewModel
        //        {
        //            Id = x.Id,
        //            Title = x.Title,
        //            Thumbnail = x.Thumbnail,
        //            ChannelName = x.Channel.ChannelName,
        //            ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
        //            CreatedAt = x.UploadDate
        //        })
        //        .ToListAsync();
        //}
        // Phương thức để lấy danh sách video đề xuất
        private async Task<List<RecommendedVideoViewModel>> GetRecommendedVideos(Guid currentVideoId)
        {
            var recommendedVideos = new List<RecommendedVideoViewModel>();

            // 1. 20% liên quan đến người nổi tiếng (2 video)
            // Lấy danh sách người nổi tiếng trong video hiện tại
            var currentVideoCelebrities = await Context.RecognizeCelebrities
                .Where(rc => rc.VideoId == currentVideoId)
                .Select(rc => rc.CelebrityId)
                .ToListAsync();

            var celebrityVideos = new List<RecommendedVideoViewModel>();
            if (currentVideoCelebrities.Any()) // Chỉ xử lý nếu video hiện tại có người nổi tiếng
            {
                celebrityVideos = await Context.Videos
                    .Where(x => x.Id != currentVideoId && x.RecognizeCelebrities.Any(rc => currentVideoCelebrities.Contains(rc.CelebrityId)))
                    .OrderByDescending(x => x.Viewers.Select(v => v.NumberOfVisit).Sum()) // Sắp xếp theo số lượt xem
                    .Take(2) // Lấy 2 video
                    .Select(x => new RecommendedVideoViewModel
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Thumbnail = x.Thumbnail,
                        ChannelName = x.Channel.ChannelName,
                        ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                        CreatedAt = x.UploadDate
                    })
                    .ToListAsync();
            }
            recommendedVideos.AddRange(celebrityVideos);

            // 2. 20% theo video có nhiều view nhất (2 video)
            var mostViewedVideos = await Context.Videos
                .Where(x => x.Id != currentVideoId && !recommendedVideos.Select(v => v.Id).Contains(x.Id)) // Tránh trùng lặp
                .OrderByDescending(x => x.Viewers.Select(v => v.NumberOfVisit).Sum()) // Sắp xếp theo số lượt xem
                .Take(2) // Lấy 2 video
                .Select(x => new RecommendedVideoViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail,
                    ChannelName = x.Channel.ChannelName,
                    ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    CreatedAt = x.UploadDate
                })
                .ToListAsync();
            recommendedVideos.AddRange(mostViewedVideos);

            // 3. 20% theo video mới nhất (2 video)
            var latestVideos = await Context.Videos
                .Where(x => x.Id != currentVideoId && !recommendedVideos.Select(v => v.Id).Contains(x.Id)) // Tránh trùng lặp
                .OrderByDescending(x => x.UploadDate) // Sắp xếp theo ngày đăng mới nhất
                .Take(2) // Lấy 2 video
                .Select(x => new RecommendedVideoViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail,
                    ChannelName = x.Channel.ChannelName,
                    ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    CreatedAt = x.UploadDate
                })
                .ToListAsync();
            recommendedVideos.AddRange(latestVideos);

            // 4. 40% ngẫu nhiên (4 video)
            var remainingCount = 10 - recommendedVideos.Count; // Số video còn lại cần lấy (tối đa 4)
            if (remainingCount > 0)
            {
                var randomVideos = await Context.Videos
                    .Where(x => x.Id != currentVideoId && !recommendedVideos.Select(v => v.Id).Contains(x.Id)) // Tránh trùng lặp
                    .OrderBy(x => Guid.NewGuid()) // Sắp xếp ngẫu nhiên
                    .Take(remainingCount) // Lấy số video còn lại
                    .Select(x => new RecommendedVideoViewModel
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Thumbnail = x.Thumbnail,
                        ChannelName = x.Channel.ChannelName,
                        ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                        CreatedAt = x.UploadDate
                    })
                    .ToListAsync();
                recommendedVideos.AddRange(randomVideos);
            }

            // Đảm bảo danh sách có đúng 10 video (nếu thiếu, bổ sung ngẫu nhiên)
            if (recommendedVideos.Count < 10)
            {
                var additionalCount = 10 - recommendedVideos.Count;
                var additionalVideos = await Context.Videos
                    .Where(x => x.Id != currentVideoId && !recommendedVideos.Select(v => v.Id).Contains(x.Id))
                    .OrderBy(x => Guid.NewGuid())
                    .Take(additionalCount)
                    .Select(x => new RecommendedVideoViewModel
                    {
                        Id = x.Id,
                        Title = x.Title,
                        Thumbnail = x.Thumbnail,
                        ChannelName = x.Channel.ChannelName,
                        ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                        CreatedAt = x.UploadDate
                    })
                    .ToListAsync();
                recommendedVideos.AddRange(additionalVideos);
            }

            return recommendedVideos.Take(10).ToList(); // Đảm bảo chỉ trả về 10 video
        }
        [HttpGet]
        public async Task<IActionResult> GetCommentsByPage(Guid videoId, int page, int pageSize)
        {
            if (videoId == Guid.Empty || page < 1 || pageSize < 1)
            {
                return Json(new { isSuccess = false, message = "Invalid parameters" });
            }

            var comments = await Context.Comments
                .Where(c => c.VideoId == videoId)
                .OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    content = c.Content,
                    postedAt = c.CreatedDate,
                    fromName = c.AppUser.FullName,
                    fromChannelId = UnitOfWork.ChannelRepo.GetChannelIdByUserId(c.AppUserId).GetAwaiter().GetResult(),
                    appUserId = c.AppUserId
                })
                .ToListAsync();

            return Json(new { isSuccess = true, comments });
        }
        #endregion
    }
}
