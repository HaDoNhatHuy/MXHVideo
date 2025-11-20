using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Moderator;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.ModeratorRole}")]
    public class ModeratorController : CoreController
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory; // THÊM
        public ModeratorController(IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
        }
        public async Task<IActionResult> AllVideos()
        {
            var moderatorVideo = await UnitOfWork.VideoRepo.GetAllAsync(includeProperties: "Category,Channel");
            var toReturn = Mapper.Map<IEnumerable<VideoDisplayGridViewModel>>(moderatorVideo).OrderByDescending(moderatorVideo => moderatorVideo.UploadDate);
            return View(toReturn);
        }
        [HttpPost]
        public async Task<IActionResult> DeleteVideo(Guid id)
        {
            var video = await Context.Videos
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.Thumbnail,
                    x.Title
                }).FirstOrDefaultAsync();

            if (video != null)
            {
                PhotoService.DeletePhotoLocally(video.Thumbnail);
                await UnitOfWork.VideoRepo.RemoveVideoAsync(video.Id);
                await UnitOfWork.CompleteAsync();

                TempData["notification"] = $"true;Deleted;Video of {video.Title} has been deleted";
                return RedirectToAction("AllVideos");
            }

            TempData["notification"] = $"false;Not Found;Requested video was not found";
            return RedirectToAction("AllVideos");
        }
        // --- Bắt đầu phần quản lý Report ---

        public async Task<IActionResult> AllReports()
        {
            var reports = await Context.Reports
                .Include(r => r.Video)
                .Include(r => r.AppUser)
                .OrderByDescending(r => r.ReportedDate)
                .Select(r => new ReportDisplayViewModel
                {
                    Id = r.Id,
                    VideoId = r.VideoId,
                    VideoTitle = r.Video.Title ?? "Untitled Video",
                    ThumbnailUrl = r.Video.Thumbnail,
                    ReportedByUserId = r.AppUserId,
                    ReportedByUserName = r.AppUser.FullName ?? r.AppUser.UserName,
                    Reason = r.Reason,
                    Status = r.Status,
                    IsBlurringActivated = r.IsBlurringActivated,
                    ReportedDate = r.ReportedDate,
                    ReportedCelebrityName = r.ReportedCelebrityName
                })
                .ToListAsync();

            return View(reports);
        }
        [Authorize(Roles = $"{SD.AdminRole}")]         
        [HttpPost]
        public async Task<IActionResult> ToggleBlur(Guid videoId, bool activate, string celebrityName)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();

            var video = await context.Videos
                .Include(v => v.VideoFile)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null || video.VideoFile == null)
                return Json(new ApiResponse(404, message: "Video không tồn tại."));

            video.IsBlurActivated = activate;
            await context.SaveChangesAsync();

            if (!activate)
                return Json(new ApiResponse(200, "Thành công", "Đã vô hiệu hóa làm mờ."));

            if (string.IsNullOrWhiteSpace(video.CelebrityFrames))
                return Json(new ApiResponse(400, message: "Chưa có dữ liệu khuôn mặt."));

            var videoContent = video.VideoFile.Contents;
            var videoExtension = video.VideoFile.Extension ?? ".mp4";
            var celebrityFramesJson = video.CelebrityFrames;
            var videoTitle = video.Title;

            // FIRE-AND-FORGET
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunVideoBlurringJobAsync(
                        videoId, videoTitle, videoContent,
                        videoExtension, celebrityFramesJson, celebrityName
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Blur Job] LỖI NGOẠI LỆ: {ex}");
                }
            });

            return Json(new ApiResponse(202, "Đang xử lý",
                $"Đang làm mờ '{celebrityName}' trong video '{videoTitle}'. Vui lòng reload sau 1-2 phút."));
        }

        private async Task RunVideoBlurringJobAsync(
            Guid videoId, string videoTitle, byte[] videoContent,
            string videoExtension, string celebrityFramesJson, string celebrityName)
        {
            var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp_processing");
            Directory.CreateDirectory(tempDir);
            var originalPath = Path.Combine(tempDir, $"{videoId}_original{videoExtension}");

            try
            {
                await System.IO.File.WriteAllBytesAsync(originalPath, videoContent);
                Console.WriteLine($"[C#] Ghi file tạm: {originalPath}");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(15);

                var payload = new
                {
                    video_path = originalPath.Replace("\\", "/"),
                    celebrity_frames_json = celebrityFramesJson,
                    celebrity_to_blur = celebrityName
                };

                var json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("http://localhost:5000/blur_selected_celebrity", content);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[C#] Python lỗi: {err}");
                    return;
                }

                Console.WriteLine($"[C#] Python đã xử lý xong. Đang đọc lại file...");

                if (!System.IO.File.Exists(originalPath))
                {
                    Console.WriteLine($"[C#] File bị mất: {originalPath}");
                    return;
                }

                var blurredBytes = await System.IO.File.ReadAllBytesAsync(originalPath);
                Console.WriteLine($"[C#] Đã đọc {blurredBytes.Length} bytes (đã mờ)");

                // CẬP NHẬT DB VỚI CONTEXT MỚI
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();

                var videoToUpdate = await context.Videos
                    .Include(v => v.VideoFile)
                    .FirstOrDefaultAsync(v => v.Id == videoId);

                if (videoToUpdate?.VideoFile != null)
                {
                    videoToUpdate.VideoFile.Contents = blurredBytes;
                    await context.SaveChangesAsync();
                    Console.WriteLine($"[C#] ĐÃ CẬP NHẬT DB: {videoTitle}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[C#] Lỗi: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                if (System.IO.File.Exists(originalPath))
                {
                    System.IO.File.Delete(originalPath);
                    Console.WriteLine($"[C#] Đã xóa file tạm");
                }
            }
        }

        // Phương thức để Admin đánh dấu Report là đã xem
        [HttpPost]
        public async Task<IActionResult> CloseReport(Guid reportId)
        {
            var report = await Context.Reports.FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy báo cáo."));
            }

            report.Status = "Reviewed/Closed";
            await Context.SaveChangesAsync();

            return Json(new ApiResponse(200, "Đã đóng", "Đã đánh dấu báo cáo là đã xem."));
        }

    }
}
