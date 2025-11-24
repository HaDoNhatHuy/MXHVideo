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
using System.Net.Http.Json;
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
        [Authorize(Roles = $"{SD.ModeratorRole}")]
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

            // Cập nhật trạng thái DB
            video.IsBlurActivated = activate;

            if (!activate)
            {
                // Nếu tắt blur: (Tùy chọn) Bạn có thể khôi phục file gốc nếu trước đó bạn đã backup
                // Hiện tại chỉ update trạng thái
                await context.SaveChangesAsync();
                return Json(new ApiResponse(200, "Thành công", "Đã vô hiệu hóa làm mờ."));
            }

            if (string.IsNullOrWhiteSpace(video.CelebrityFrames) || video.CelebrityFrames == "{}")
                return Json(new ApiResponse(400, message: "Chưa có dữ liệu khuôn mặt để làm mờ."));

            // Lấy đường dẫn vật lý
            string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
            string physicalPath = webRootPath + video.VideoFile.FilePath.Replace("/", "\\");

            if (!System.IO.File.Exists(physicalPath))
                return Json(new ApiResponse(404, message: "File video gốc không tìm thấy trên server."));

            var celebrityFramesJson = video.CelebrityFrames;
            var videoTitle = video.Title;

            // FIRE-AND-FORGET: Chạy ngầm để không treo UI
            _ = Task.Run(async () =>
            {
                try
                {
                    await RunVideoBlurringJobAsync(videoId, videoTitle, physicalPath, celebrityFramesJson, celebrityName);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Blur Job] LỖI NGOẠI LỆ: {ex}");
                }
            });

            return Json(new ApiResponse(202, "Đang xử lý",
                $"Hệ thống đang xử lý làm mờ '{celebrityName}' trên file gốc. Vui lòng đợi vài phút."));
        }

        private async Task RunVideoBlurringJobAsync(
    Guid videoId, string videoTitle, string originalPhysicalPath,
    string celebrityFramesJson, string celebrityName)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(30); // Tăng timeout vì xử lý video lâu

                // Gửi đường dẫn file thật cho Python
                var payload = new
                {
                    video_path = originalPhysicalPath, // Python sẽ đọc trực tiếp file này
                    celebrity_frames_json = celebrityFramesJson,
                    celebrity_to_blur = celebrityName
                };

                // Gọi Python API (Lưu ý: Python code cần update để trả về output_path)
                var response = await client.PostAsJsonAsync("http://localhost:5000/blur_selected_celebrity", payload);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Blur] Lỗi Python: {response.StatusCode}");
                    return;
                }

                var resultDict = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

                if (resultDict != null && resultDict.ContainsKey("output_path"))
                {
                    string blurredPath = resultDict["output_path"]; // File _final.mp4 do Python tạo

                    // CẬP NHẬT DB: Trỏ FilePath sang file mới đã làm mờ
                    // Hoặc: Python đã ghi đè file gốc thì không cần làm gì cả (tùy logic Python của bạn)

                    // Ở đây giả sử Python tạo file mới tên là _final.mp4 nằm cùng thư mục
                    // Chúng ta cập nhật DB để trỏ vào file mới này

                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                    var videoToUpdate = await context.Videos.Include(v => v.VideoFile).FirstOrDefaultAsync(v => v.Id == videoId);

                    if (videoToUpdate?.VideoFile != null)
                    {
                        // Chuyển đường dẫn tuyệt đối thành tương đối để lưu Web
                        string webRootPath = Directory.GetCurrentDirectory() + "\\wwwroot";
                        string relativePath = blurredPath.Replace(webRootPath, "").Replace("\\", "/");

                        videoToUpdate.VideoFile.FilePath = relativePath;
                        await context.SaveChangesAsync();

                        Console.WriteLine($"[Blur] Đã cập nhật video {videoTitle} sang file đã làm mờ.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Blur] Exception: {ex.Message}");
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
