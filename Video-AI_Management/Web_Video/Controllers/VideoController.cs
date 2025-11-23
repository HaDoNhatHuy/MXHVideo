using Database_Video.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Video;
using WebVideo.Utility;
using Xabe.FFmpeg;
using Microsoft.Net.Http.Headers; // Thêm namespace này
using static Web_Video.ViewModels.Video.VideoWatchViewModel;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]
    public class VideoController : CoreController
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public VideoController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        [HttpGet]
        public async Task<IActionResult> Watch(Guid id, Guid? playlistId = null)
        {
            // Sử dụng phương thức hiệu quả với projections
            var toReturn = await GetVideoWatch_VMWithProjections(id, playlistId); // <=== TRUYỀN playlistId

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

        [HttpPost]
        [HttpGet] // Thêm để hỗ trợ sendBeacon
        public async Task<IActionResult> UpdateProgress(Guid videoId, float progressSeconds)
        {
            try
            {
                var userId = User.GetUserId();

                // Lấy VideoView mới nhất (chỉ 1 entry duy nhất)
                var videoView = await Context.VideoViews
                    .Where(x => x.AppUserId == userId && x.VideoId == videoId)
                    .OrderByDescending(x => x.LastVisit)
                    .FirstOrDefaultAsync();

                if (videoView == null)
                {
                    // Nếu chưa có → tạo mới
                    var ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
                    await UnitOfWork.VideoViewRepo.HandleVideoViewAsync(userId, videoId, ip);
                    await UnitOfWork.CompleteAsync();

                    // Lấy lại sau khi tạo
                    videoView = await Context.VideoViews
                        .Where(x => x.AppUserId == userId && x.VideoId == videoId)
                        .FirstOrDefaultAsync();
                }

                if (videoView != null && progressSeconds > 0)
                {
                    videoView.ProgressSeconds = progressSeconds;
                    videoView.LastVisit = DateTime.UtcNow; // ✅ Cập nhật LastVisit
                    await Context.SaveChangesAsync();

                    return Json(new ApiResponse(200, message: "Progress updated"));
                }

                return Json(new ApiResponse(404, message: "Video view not found"));
            }
            catch (Exception ex)
            {
                return Json(new ApiResponse(500, message: $"Error: {ex.Message}"));
            }
        }
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
        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpPost]
        public async Task<IActionResult> ReportVideo(ReportViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ApiResponse(400, message: "Dữ liệu báo cáo không hợp lệ."));
            }

            var userId = User.GetUserId(); // Lấy ID người dùng hiện tại
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Người dùng chưa đăng nhập."));
            }

            var video = await Context.Videos.AnyAsync(v => v.Id == model.VideoId);
            if (!video)
            {
                return Json(new ApiResponse(404, message: "Video không tồn tại."));
            }

            // Kiểm tra xem người dùng đã báo cáo video này chưa (tùy chọn)
            var existingReport = await Context.Reports
                .AnyAsync(r => r.AppUserId == userId && r.VideoId == model.VideoId && r.Status == "New");

            if (existingReport)
            {
                return Json(new ApiResponse(400, message: "Bạn đã gửi báo cáo cho video này."));
            }

            var newReport = new Report
            {
                VideoId = model.VideoId,
                AppUserId = userId,
                Reason = model.Reason + (model.OtherReason != null ? $" ({model.OtherReason})" : ""),
                Status = "New",
                IsBlurringActivated = false,
                ReportedCelebrityName = model.ReportedCelebrityName,  // Thêm nếu có
                ReportedDate = DateTime.UtcNow
            };

            Context.Reports.Add(newReport); // Thêm Report vào DbSet
            await Context.SaveChangesAsync(); // Lưu thay đổi

            return Json(new ApiResponse(201, "Thành công", "Báo cáo của bạn đã được gửi và sẽ được xem xét."));
        }
        [HttpGet]
        public async Task<IActionResult> GetVideoFile(Guid videoId)
        {
            var video = await Context.Videos.Include(v => v.VideoFile).FirstOrDefaultAsync(v => v.Id == videoId);
            if (video == null || video.VideoFile == null) return NotFound();

            // Tạo đường dẫn vật lý từ đường dẫn web trong DB
            // DB lưu: /uploads/videos/abc.mp4
            //string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
            //string physicalPath = webRootPath + video.VideoFile.FilePath.Replace("/", "\\");
            string physicalPath = video.VideoFile.FilePath;

            // BƯỚC SỬA: Kiểm tra nếu đường dẫn không phải là tuyệt đối hoặc không tồn tại, thì giả định nó là tương đối trong wwwroot
            if (!System.IO.File.Exists(physicalPath))
            {
                // Giả sử đây là đường dẫn tương đối (từ upload thủ công)
                string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                physicalPath = webRootPath + video.VideoFile.FilePath.Replace("/", "\\");

                if (!System.IO.File.Exists(physicalPath)) return NotFound("File not found on server");
            }

            if (!System.IO.File.Exists(physicalPath)) return NotFound("File not found on server");

            // Streaming video (Cho phép tua)
            return PhysicalFile(physicalPath, video.VideoFile.ContentType, enableRangeProcessing: true);
        }
        [HttpPost]
        public async Task<IActionResult> DownloadVideoFile(Guid videoId)
        {
            var fetchedVideo = await UnitOfWork.VideoRepo.GetFirstOrDefaultAsync(x => x.Id == videoId, "VideoFile");
            if (fetchedVideo != null)
            {
                //string fileDownloadName = fetchedVideo.Title + fetchedVideo.VideoFile.Extension;
                //string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                //string physicalPath = webRootPath + fetchedVideo.VideoFile.FilePath.Replace("/", "\\");
                string physicalPath = fetchedVideo.VideoFile.FilePath;
                string fileDownloadName = fetchedVideo.Title + fetchedVideo.VideoFile.Extension;

                // BƯỚC SỬA: Kiểm tra nếu đường dẫn không phải là tuyệt đối hoặc không tồn tại, thì giả định nó là tương đối trong wwwroot
                if (!System.IO.File.Exists(physicalPath))
                {
                    string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                    physicalPath = webRootPath + fetchedVideo.VideoFile.FilePath.Replace("/", "\\");
                }
                if (!System.IO.File.Exists(physicalPath)) return NotFound("File not found on server");

                // Trả về file từ ổ cứng
                return PhysicalFile(physicalPath, fetchedVideo.VideoFile.ContentType, fileDownloadName);
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
                toReturn.ImageUrl = fetchedVideo.Thumbnail.Replace("\\", "/");
            }

            toReturn.CategoryDropdown = await GetCategoryDropdownAsync();
            return View(toReturn);
        }
        [HttpPost]
        public async Task<IActionResult> CreateEditVideo(VideoAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                // 1. Cấu hình đường dẫn lưu file (Lưu vào wwwroot/uploads/videos)
                string webRootPath = _httpClientFactory.GetType() == typeof(string) ? "" : Directory.GetCurrentDirectory() + "\\wwwroot";
                // Lưu ý: Dòng trên để lấy path, nếu bạn đã có _webHostEnvironment thì dùng nó tốt hơn.
                // Giả sử bạn dùng Directory.GetCurrentDirectory() cho đơn giản:
                string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "videos");

                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                Video videoToAdd = null;

                // === TRƯỜNG HỢP TẠO MỚI ===
                if (model.Id == Guid.Empty)
                {
                    if (model.VideoUpload == null) return Json(new { isSuccess = false, message = "Thiếu video" });

                    // A. Lưu file video vào ổ cứng
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.VideoUpload.FileName);
                    string physicalPath = Path.Combine(uploadDir, fileName); // Đường dẫn vật lý D:\...

                    using (var fileStream = new FileStream(physicalPath, FileMode.Create))
                    {
                        await model.VideoUpload.CopyToAsync(fileStream);
                    }

                    // B. Lấy thời lượng video (dùng đường dẫn vật lý)
                    string duration = await GetVideoDuration(physicalPath);

                    // --- XỬ LÝ THUMBNAIL TỰ ĐỘNG ---
                    string thumbnailPath = "";
                    if (model.ImageUpload != null)
                    {
                        // Nếu người dùng chọn ảnh -> Upload bình thường
                        thumbnailPath = PhotoService.UploadPhotoLocally(model.ImageUpload);
                    }
                    else
                    {
                        // Nếu KHÔNG chọn ảnh -> Dùng FFmpeg cắt frame ngẫu nhiên
                        thumbnailPath = await GenerateThumbnailFromVideo(physicalPath);
                    }

                    // C. Xử lý AI (Tùy chọn dựa vào Checkbox)
                    string recognitionResult = "";
                    string celebrityFramesJson = "{}";

                    if (model.HasCelebrity)
                    {
                        // CÓ người nổi tiếng -> Gọi AI
                        recognitionResult = await ProcessVideo(physicalPath); // Hàm này cần nhận đường dẫn vật lý

                        // Gọi Python để lấy JSON khung hình (nếu có logic đó)
                        var httpClient = _httpClientFactory.CreateClient();
                        var requestBody = new { video_path = physicalPath }; // Gửi path thật cho Python
                        var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
                        try
                        {
                            var response = await httpClient.PostAsync("http://localhost:5000/process_video", content);
                            if (response.IsSuccessStatusCode)
                            {
                                var resultJson = await response.Content.ReadAsStringAsync();
                                var framesData = JsonConvert.DeserializeObject<Dictionary<string, object>>(resultJson)["frames"];
                                celebrityFramesJson = JsonConvert.SerializeObject(framesData);
                            }
                        }
                        catch { /* Bỏ qua lỗi AI nếu có */ }
                    }
                    else
                    {
                        // KHÔNG CÓ người nổi tiếng -> Bỏ qua AI
                        recognitionResult = "Không yêu cầu nhận diện";
                    }

                    // D. Lưu vào Database
                    videoToAdd = new Video()
                    {
                        Id = Guid.NewGuid(),
                        Title = model.Title,
                        Description = model.Description,
                        CategoryId = model.CategoryId,
                        ChannelId = await UnitOfWork.ChannelRepo.GetChannelIdByUserId(User.GetUserId()),
                        Thumbnail = thumbnailPath,
                        Duration = duration,
                        UploadDate = DateTime.UtcNow,
                        RecognizedCelebrities = recognitionResult,
                        CelebrityFrames = celebrityFramesJson,
                        VideoFile = new VideoFile
                        {
                            Id = Guid.NewGuid(),
                            ContentType = model.VideoUpload.ContentType,
                            Extension = Path.GetExtension(model.VideoUpload.FileName),
                            FilePath = $"/uploads/videos/{fileName}" // Chỉ lưu đường dẫn web tương đối
                        }
                    };

                    // Nếu có nhận diện ra người nổi tiếng thì mới lưu vào bảng phụ
                    if (model.HasCelebrity)
                    {
                        await SaveRecognizedCelebrities(videoToAdd, recognitionResult);
                    }

                    UnitOfWork.VideoRepo.Add(videoToAdd);
                    await UnitOfWork.CompleteAsync();

                    TempData["notification"] = "true;Success;Video uploaded successfully";
                    return Json(new { redirectUrl = "/Channel/Index" });
                }
            }
            return Json(new { isSuccess = false, message = "Invalid Data" });
        }
        // THÊM HÀM MỚI TRONG VideoController.cs ĐỂ CẮT THUMBNAIL
        private async Task<string> GenerateThumbnailFromVideo(string videoPhysicalPath)
        {
            try
            {
                string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                string thumbFolder = Path.Combine(webRootPath, "images", "thumbnails");
                if (!Directory.Exists(thumbFolder)) Directory.CreateDirectory(thumbFolder);

                string thumbFileName = Guid.NewGuid().ToString() + ".jpg";
                string thumbPhysicalPath = Path.Combine(thumbFolder, thumbFileName);

                // Cấu hình đường dẫn FFmpeg (Đảm bảo đường dẫn đúng với máy bạn)
                Xabe.FFmpeg.FFmpeg.SetExecutablesPath(@"C:\FFmpeg\ffmpeg\bin");

                // Lấy thông tin video để biết độ dài
                var mediaInfo = await FFmpeg.GetMediaInfo(videoPhysicalPath);

                // Chọn thời điểm cắt: 20% đầu video hoặc 5s (để tránh màn hình đen ở giây 0)
                double videoDuration = mediaInfo.Duration.TotalSeconds;
                double captureTime = videoDuration > 10 ? 5 : 1;

                // Thực hiện cắt ảnh
                IConversion conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
                    videoPhysicalPath, thumbPhysicalPath, TimeSpan.FromSeconds(captureTime)
                );
                await conversion.Start();

                return $"/images/thumbnails/{thumbFileName}"; // Trả về đường dẫn web
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi tạo thumbnail tự động: {ex.Message}");
                return "/avatarUser/avt-default.jpg"; // Fallback nếu lỗi
            }
        }

        // Phương thức tính thời lượng video
        private async Task<string> GetVideoDuration(string videoPath)
        {
            try
            {
                Xabe.FFmpeg.FFmpeg.SetExecutablesPath(@"C:\FFmpeg\ffmpeg\bin");
                var mediaInfo = await FFmpeg.GetMediaInfo(videoPath);
                var duration = mediaInfo.Duration;
                return duration.ToString(@"mm\:ss");
            }
            catch
            {
                return "0:00"; // Fallback nếu lỗi
            }
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
        [HttpDelete]
        public async Task<IActionResult> DeleteVideo(Guid id)
        {
            try
            {
                var video = await Context.Videos
                    .Include(x => x.Comments)
                    .Include(x => x.LikeDislikes)
                    .Include(x => x.Viewers)
                    .Include(x => x.RecognizeCelebrities)
                    .Where(x => x.Id == id && x.Channel.AppUserId == User.GetUserId())
                    .Select(x => new
                    {
                        x.Id,
                        x.Thumbnail,
                        x.Title,
                        x.Comments,
                        x.LikeDislikes,
                        x.Viewers,
                        x.RecognizeCelebrities
                    }).FirstOrDefaultAsync();

                if (video == null)
                {
                    return Json(new ApiResponse(404, message: "The requested video was not found"));
                }
                // Tìm VideoFile
                var videoFile = await Context.Set<VideoFile>().FirstOrDefaultAsync(x => x.VideoId == id);

                if (videoFile != null && !string.IsNullOrEmpty(videoFile.FilePath))
                {
                    string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                    string physicalPath = webRootPath + videoFile.FilePath.Replace("/", "\\");

                    // Xóa file vật lý nếu tồn tại
                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }

                    // Xóa record trong bảng VideoFile (nếu Cascade Delete chưa cấu hình)
                    Context.Set<VideoFile>().Remove(videoFile);
                }

                // Xóa các bản ghi liên quan
                Context.Comments.RemoveRange(video.Comments);
                Context.LikeDislikes.RemoveRange(video.LikeDislikes);
                Context.VideoViews.RemoveRange(video.Viewers);
                Context.RecognizeCelebrities.RemoveRange(video.RecognizeCelebrities);

                // Xóa thumbnail
                PhotoService.DeletePhotoLocally(video.Thumbnail);

                // Xóa video
                await UnitOfWork.VideoRepo.RemoveVideoAsync(video.Id);
                await UnitOfWork.CompleteAsync();

                return Json(new ApiResponse(200, "Deleted", "Your video '" + video.Title + "' has been deleted"));
            }
            catch (Exception ex)
            {
                // Log lỗi để debug
                Console.WriteLine($"Error deleting video: {ex.Message}\n{ex.StackTrace}");
                return Json(new ApiResponse(500, message: $"Error deleting video: {ex.Message}"));
            }
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
        private async Task<VideoWatchViewModel> GetVideoWatch_VMWithProjections(Guid id, Guid? playlistId) // THAY ĐỔI: Thêm Guid? playlistId
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
                    CategoryName = x.Category.CategoryName,
                    ChannelAvatar = x.Channel.ChannelPicture ?? "/avatarUser/avt-default.jpg",
                    IsSubscribed = x.Channel.Subscribers.Any(s => s.AppUserId == userId),
                    IsLiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == true),
                    IsDisiked = x.LikeDislikes.Any(l => l.AppUserId == userId && l.Liked == false),
                    SubscribersCount = x.Channel.Subscribers.Count(),
                    ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                    LikesCount = x.LikeDislikes.Where(l => l.Liked == true).Count(),
                    DislikesCount = x.LikeDislikes.Where(l => l.Liked == false).Count(),
                    VideoContentType = x.VideoFile.ContentType,
                    RecognizedCelebrities = x.RecognizedCelebrities,
                    CelebrityFramesJson = x.CelebrityFrames ?? "{}",
                    ProgressSeconds = x.Viewers
                        .Where(v => v.AppUserId == userId)
                        .OrderByDescending(v => v.LastVisit)
                        .Select(v => v.ProgressSeconds)
                        .FirstOrDefault(), // Lấy ProgressSeconds từ VideoView mới nhất
                    CommentVM = new CommentViewModel
                    {
                        PostComment = new CommentPostViewModel
                        {
                            VideoId = x.Id,
                        },
                        AvailableComments = x.Comments
                            .OrderByDescending(c => c.CreatedDate)
                            .Take(5)
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

            if (toReturn == null) return null;

            // PHẦN MỚI: Xử lý Playlist
            if (playlistId.HasValue && playlistId.Value != Guid.Empty)
            {
                var playlist = await Context.Playlists
                    .Include(p => p.PlaylistItems)
                    .ThenInclude(pi => pi.Video)
                    .ThenInclude(v => v.Channel)
                    .Include(p => p.PlaylistItems)
                    .ThenInclude(pi => pi.Video)
                    .ThenInclude(v => v.Category) // Cần Category để lấy CategoryName trong DTO
                    .Where(p => p.Id == playlistId.Value)
                    .FirstOrDefaultAsync(p => p.AppUserId == userId || p.Privacy == 0); // Giả sử chỉ lấy Public hoặc của mình

                if (playlist != null)
                {
                    // Gán thông tin Playlist vào ViewModel
                    toReturn.CurrentPlaylistId = playlist.Id;
                    toReturn.CurrentPlaylistName = playlist.Name;

                    // Lấy danh sách video trong playlist và ánh xạ sang PlaylistItemDto
                    toReturn.CurrentPlaylistItems = playlist.PlaylistItems
                        .OrderBy(pi => pi.OrderIndex)
                        .Select(pi => new Web_Video.ViewModels.Playlist.PlaylistItemDto
                        {
                            VideoId = pi.VideoId,
                            Title = pi.Video.Title,
                            Thumbnail = pi.Video.Thumbnail,
                            ChannelId = pi.Video.ChannelId ?? Guid.Empty,
                            ChannelName = pi.Video.Channel.ChannelName,
                            Duration = pi.Video.Duration ?? "0:00",
                            OrderIndex = pi.OrderIndex,
                            Description = pi.Video.Description,
                            CategoryName = pi.Video.Category.CategoryName,
                            RecognizedCelebrities = pi.Video.RecognizedCelebrities,
                            CelebrityFramesJson = pi.Video.CelebrityFrames ?? "{}",
                            CreatedAt = pi.Video.UploadDate
                        })
                        .ToList();
                }
            }

            if (toReturn != null && !string.IsNullOrEmpty(toReturn.CelebrityFramesJson) && toReturn.CelebrityFramesJson != "{}")
            {
                try
                {
                    var framesData = JsonConvert.DeserializeObject<Dictionary<string, List<Dictionary<string, object>>>>(toReturn.CelebrityFramesJson);
                    toReturn.CelebrityFrames = new Dictionary<string, List<CelebrityFrame>>();
                    foreach (var celeb in framesData)
                    {
                        var frames = new List<CelebrityFrame>();
                        foreach (var frameData in celeb.Value)
                        {
                            frames.Add(new CelebrityFrame
                            {
                                Time = Convert.ToSingle(frameData["time"]),
                                FrameImage = frameData["frame"].ToString()
                            });
                        }
                        toReturn.CelebrityFrames[celeb.Key] = frames;
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON Parse Error: {ex.Message}");
                    toReturn.CelebrityFrames = new Dictionary<string, List<CelebrityFrame>>();
                }
            }
            else
            {
                toReturn.CelebrityFrames = new Dictionary<string, List<CelebrityFrame>>();
            }

            return toReturn;
        }
        private async Task<List<RecommendedVideoViewModel>> GetRecommendedVideos(Guid currentVideoId)
        {
            string userId = User.GetUserId();
            var recommendedVideos = new List<RecommendedVideoViewModel>();
            try
            {
                // 1. Gọi Python API
                var httpClient = _httpClientFactory.CreateClient();
                var payload = new { userId = userId, currentVideoId = currentVideoId };
                // Gọi sang Port 5001 (Service mới)
                var response = await httpClient.PostAsJsonAsync("http://localhost:5001/api/recommend", payload);

                if (response.IsSuccessStatusCode)
                {
                    var apiResult = await response.Content.ReadFromJsonAsync<PythonRecommendResponse>(); // Tạo class DTO bên dưới
                    var videoIds = apiResult.recommendations;

                    // 2. Query DB lấy thông tin chi tiết của các ID đó
                    if (videoIds != null && videoIds.Count() > 0)
                    {
                        recommendedVideos = await Context.Videos
                            .Where(x => videoIds.Contains(x.Id))
                            .Include(x => x.Channel)
                            .Include(x => x.Viewers)
                            .Select(x => new RecommendedVideoViewModel
                            {
                                Id = x.Id,
                                Title = x.Title,
                                Thumbnail = x.Thumbnail,
                                ChannelName = x.Channel.ChannelName,
                                Duration = x.Duration,
                                ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                                CreatedAt = x.UploadDate
                            })
                            .ToListAsync();

                        // Sắp xếp lại theo thứ tự Python trả về (vì SQL 'IN' không giữ thứ tự)
                        recommendedVideos = recommendedVideos
                            .OrderBy(v => videoIds.IndexOf(v.Id))
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
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
                            Duration = x.Duration,
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
                        Duration = x.Duration,
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
                        Duration = x.Duration,
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
                            Duration = x.Duration,
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
                            Duration = x.Duration,
                            ViewersCount = x.Viewers.Select(v => v.NumberOfVisit).Sum(),
                            CreatedAt = x.UploadDate
                        })
                        .ToListAsync();
                    recommendedVideos.AddRange(additionalVideos);
                }

                return recommendedVideos.Take(10).ToList(); // Đảm bảo chỉ trả về 10 video
            }
            // Đảm bảo KHÔNG BAO GIỜ NULL (Fallback cuối cùng)
            if (recommendedVideos.Count == 0)
            {
                recommendedVideos = await Context.Videos
                    .OrderByDescending(v => v.UploadDate)
                    .Take(10)
                    .Select(x => new RecommendedVideoViewModel { /* Map properties */ })
                    .ToListAsync();
            }

            return recommendedVideos;
        }
        // Class DTO để hứng kết quả JSON
        public class PythonRecommendResponse
        {
            public string user_id { get; set; }
            public List<Guid> recommendations { get; set; }
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
