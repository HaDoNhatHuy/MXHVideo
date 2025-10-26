using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Web_Video.Services;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Moderator;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.ModeratorRole}")]
    public class ModeratorController : CoreController
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public ModeratorController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
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
        [Authorize(Roles = $"{SD.AdminRole}")] // Đảm bảo chỉ Admin/Mod được phép [4]
        [HttpPost]
        public async Task<IActionResult> ToggleBlur(Guid videoId, bool activate, string celebrityName)
        {
            var video = await Context.Videos
                .Include(v => v.VideoFile)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null || video.VideoFile == null)
            {
                return Json(new ApiResponse(404, message: "Video hoặc file video không tồn tại."));
            }

            // 1. Cập nhật trạng thái trong DB
            video.IsBlurActivated = activate;
            await Context.SaveChangesAsync();

            // 2. KÍCH HOẠT QUÁ TRÌNH XỬ LÝ VIDEO PYTHON
            if (activate)
            {
                if (string.IsNullOrEmpty(video.CelebrityFrames))
                {
                    return Json(new ApiResponse(400, message: "Không có dữ liệu khuôn mặt để làm mờ. Chỉ bật cờ DB."));
                }

                // Bước 2a: Lưu video gốc ra file tạm thời để Python truy cập
                var tempDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/temp_processing");
                Directory.CreateDirectory(tempDirectory);
                var originalVideoPath = Path.Combine(tempDirectory, $"{videoId}_original{video.VideoFile.Extension}");
                await System.IO.File.WriteAllBytesAsync(originalVideoPath, video.VideoFile.Contents);

                var blurredVideoPath = Path.Combine(tempDirectory, $"{videoId}_blurred{video.VideoFile.Extension}");

                var client = _httpClientFactory.CreateClient(); // Giả định đã inject IHttpClientFactory [5]

                var requestBody = new
                {
                    video_path = originalVideoPath,
                    output_path = blurredVideoPath,
                    celebrity_frames_json = video.CelebrityFrames,
                    celebrity_to_blur = celebrityName // Chỉ làm mờ người này
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                try
                {
                    // Gọi API Python để bắt đầu xử lý làm mờ (PHẦN B.1)
                    var response = await client.PostAsync("http://localhost:5000/blur_selected_celebrity", content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Bước 2b: Đọc file video đã làm mờ và lưu trở lại DB
                        if (System.IO.File.Exists(blurredVideoPath))
                        {
                            byte[] blurredContents = await System.IO.File.ReadAllBytesAsync(blurredVideoPath);

                            // Cập nhật Entity VideoFile (Đây là điểm phức tạp. Nếu VideoFile chỉ có 1 Contents [3], ta phải ghi đè)
                            // **GIẢI PHÁP TỐT HƠN**: Tạo Entity VideoFileBlurred riêng hoặc thêm trường ContentsBlurred vào VideoFile.
                            // TẠM THỜI: Ghi đè file gốc (trong ngữ cảnh này, file đã làm mờ trở thành file chính thức nếu cờ bật)
                            video.VideoFile.Contents = blurredContents;

                            // Xóa file tạm thời
                            System.IO.File.Delete(originalVideoPath);
                            System.IO.File.Delete(blurredVideoPath);

                            await Context.SaveChangesAsync(); // Lưu nội dung file mới
                            return Json(new ApiResponse(200, "Thành công", $"Đã KÍCH HOẠT và xử lý làm mờ ({celebrityName}) cho video '{video.Title}'"));
                        }
                        else
                        {
                            return Json(new ApiResponse(202, "Cảnh báo", $"Kích hoạt làm mờ thành công, nhưng không tìm thấy file làm mờ trả về từ Python."));
                        }
                    }
                    else
                    {
                        // Nếu API Python báo lỗi
                        var errorContent = await response.Content.ReadAsStringAsync();
                        video.IsBlurActivated = false; // Rollback
                        await Context.SaveChangesAsync();
                        return Json(new ApiResponse(500, "Lỗi API Python", $"Lỗi khi xử lý làm mờ: {errorContent}"));
                    }
                }
                catch (Exception ex)
                {
                    // Xóa file tạm nếu xảy ra lỗi C#
                    if (System.IO.File.Exists(originalVideoPath)) System.IO.File.Delete(originalVideoPath);
                    if (System.IO.File.Exists(blurredVideoPath)) System.IO.File.Delete(blurredVideoPath);
                    video.IsBlurActivated = false; // Rollback
                    await Context.SaveChangesAsync();
                    return Json(new ApiResponse(500, "Lỗi", $"Lỗi kết nối hoặc xử lý file: {ex.Message}"));
                }
            }
            else // Vô hiệu hóa
            {
                // Khi vô hiệu hóa, nếu bạn ghi đè file gốc, bạn sẽ cần phải có một bản sao lưu (backup) video gốc.
                // **LƯU Ý**: Vì đây là logic A-Z, ta giả định khi Admin tắt blur, họ sẽ tải lại video gốc thủ công, hoặc hệ thống phải có cơ chế backup.

                return Json(new ApiResponse(200, "Thành công", $"Đã VÔ HIỆU HÓA tính năng làm mờ cho video '{video.Title}'"));
            }
        }
        //[HttpPost]
        //public async Task<IActionResult> ToggleBlur(Guid reportId, bool activate)
        //{
        //    var report = await Context.Reports.Include(r => r.Video).FirstOrDefaultAsync(r => r.Id == reportId);

        //    if (report == null)
        //    {
        //        return Json(new ApiResponse(404, message: "Không tìm thấy báo cáo."));
        //    }

        //    var video = report.Video;
        //    if (video == null)
        //    {
        //        return Json(new ApiResponse(404, message: "Video liên quan không tồn tại."));
        //    }

        //    // Cập nhật cờ trên Entity Video
        //    video.IsBlurActivated = activate;

        //    // Cập nhật trạng thái Report và cờ trên Report
        //    report.IsBlurringActivated = activate;
        //    report.Status = activate ? "Activated Blur" : "Deactivated Blur";

        //    await Context.SaveChangesAsync(); // Lưu cả Video và Report

        //    return Json(new ApiResponse(200, "Thành công",
        //        $"Đã {(activate ? "KÍCH HOẠT" : "VÔ HIỆU HÓA")} tính năng làm mờ cho video '{video.Title}'"));
        //}

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
