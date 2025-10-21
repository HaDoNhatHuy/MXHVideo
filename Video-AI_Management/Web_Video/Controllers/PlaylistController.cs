using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Playlist;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.UserRole}")]
    public class PlaylistController : CoreController
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly DataContext _context;

        public PlaylistController(IUnitOfWork unitOfWork, DataContext context)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // Trang chính: Hiển thị tất cả playlist của người dùng
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["CurrentPage"] = "Playlists";
            return View();
        }

        // Trang xem một Playlist cụ thể
        [HttpGet]
        public async Task<IActionResult> WatchPlaylist(Guid id)
        {
            var userId = User.GetUserId();

            var playlist = await Context.Playlists
                .Include(p => p.PlaylistItems)
                .ThenInclude(pi => pi.Video)
                .ThenInclude(v => v.Channel)
                .Where(p => p.Id == id && p.AppUserId == userId) // Chỉ xem playlist của mình (cần thêm logic Public/Private nếu muốn người khác xem)
                .FirstOrDefaultAsync();

            if (playlist == null)
            {
                TempData["notification"] = "false;Not Found;Playlist không tồn tại hoặc bạn không có quyền truy cập";
                return RedirectToAction("Index");
            }

            var viewModel = new PlaylistWatchViewModel
            {
                PlaylistInfo = new PlaylistDisplayViewModel
                {
                    Id = playlist.Id,
                    Name = playlist.Name,
                    VideoCount = playlist.PlaylistItems.Count,
                    CreatedAtTimeAgo = SD.TimeAgo(playlist.CreatedDate),
                    FirstVideoThumbnail = playlist.PlaylistItems.OrderBy(pi => pi.OrderIndex).FirstOrDefault()?.Video.Thumbnail
                },
                Items = playlist.PlaylistItems
                    .OrderBy(pi => pi.OrderIndex)
                    .Select(pi => new PlaylistItemDto
                    {
                        VideoId = pi.VideoId,
                        Title = pi.Video.Title,
                        Thumbnail = pi.Video.Thumbnail,
                        ChannelName = pi.Video.Channel.ChannelName,
                        Duration = pi.Video.Duration ?? "0:00",
                        OrderIndex = pi.OrderIndex
                    }).ToList()
            };

            return View(viewModel);
        }

        #region API Endpoints (Quản lý Playlist)

        // API tạo Playlist mới
        [HttpPost]
        public async Task<IActionResult> CreatePlaylist([FromBody] PlaylistAddEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ApiResponse(400, message: "Dữ liệu không hợp lệ."));
            }

            var userId = User.GetUserId();
            var newPlaylist = new Playlist
            {
                Id = Guid.NewGuid(), // Fix: Set Id để tránh Guid.Empty
                Name = model.Name.Trim(),
                AppUserId = userId,
                Description = "", // Có thể thêm mô tả sau
                CreatedDate = DateTime.UtcNow,
                Privacy = 0 // Default Public
            };

            UnitOfWork.PlaylistRepo.Add(newPlaylist);
            await UnitOfWork.CompleteAsync();

            return Json(new ApiResponse(201, "Created", "Đã tạo danh sách phát thành công.", new { id = newPlaylist.Id, name = newPlaylist.Name }));
        }

        // API lấy danh sách Playlist của người dùng hiện tại
        [HttpGet]
        public async Task<IActionResult> GetUserPlaylists()
        {
            var userId = User.GetUserId();

            var playlists = await Context.Playlists
                .Where(p => p.AppUserId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PlaylistDisplayViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    CreatedAtTimeAgo = SD.TimeAgo(p.CreatedDate),
                    VideoCount = p.PlaylistItems.Count(),
                    // Lấy thumbnail của video đầu tiên
                    FirstVideoThumbnail = p.PlaylistItems
                        .OrderBy(pi => pi.OrderIndex)
                        .Select(pi => pi.Video.Thumbnail)
                        .FirstOrDefault()
                }).ToListAsync();

            return Json(new ApiResponse(200, result: playlists));
        }

        // API thêm hoặc xóa video khỏi Playlist
        [HttpPost]
        [ValidateAntiForgeryToken] // Thêm để bảo vệ API
        public async Task<IActionResult> ToggleVideoInPlaylist([FromBody] AddRemoveVideoToPlaylistViewModel model)
        {
            var userId = User.GetUserId();

            // 1. Kiểm tra Playlist tồn tại và thuộc về User
            var playlist = await UnitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == model.PlaylistId && p.AppUserId == userId,
                includeProperties: "PlaylistItems");

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            // 2. Kiểm tra Video tồn tại
            var videoExists = await UnitOfWork.VideoRepo.AnyAsync(v => v.Id == model.VideoId);
            if (!videoExists)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Video."));
            }

            // 3. Kiểm tra sự tồn tại của PlaylistItem
            var playlistItem = await UnitOfWork.PlaylistItemRepo.GetByKeysAsync(model.PlaylistId, model.VideoId);

            if (playlistItem == null)
            {
                // THÊM MỚI: Video chưa có trong Playlist
                int nextOrderIndex = playlist.PlaylistItems.Any()
                    ? playlist.PlaylistItems.Max(pi => pi.OrderIndex) + 1
                    : 1;

                var newPlaylistItem = new PlaylistItem(model.PlaylistId, model.VideoId, nextOrderIndex);
                UnitOfWork.PlaylistItemRepo.Add(newPlaylistItem);
                await UnitOfWork.CompleteAsync();

                return Json(new ApiResponse(200, "Added", $"Đã thêm video vào Playlist '{playlist.Name}'."));
            }
            else
            {
                // XÓA: Video đã có trong Playlist
                UnitOfWork.PlaylistItemRepo.Remove(playlistItem);
                await UnitOfWork.CompleteAsync();

                // Cập nhật lại OrderIndex cho các video còn lại (tùy chọn)
                // Logic này phức tạp và có thể bỏ qua nếu chấp nhận lỗ hổng về thứ tự.

                return Json(new ApiResponse(200, "Removed", $"Đã xóa video khỏi Playlist '{playlist.Name}'."));
            }
        }

        // API để lấy danh sách các Playlist (để hiển thị trong modal "Add to Playlist")
        [HttpGet]
        public async Task<IActionResult> GetUserPlaylistsSummary(Guid videoId)
        {
            var userId = User.GetUserId();

            var playlists = await Context.Playlists
                .Where(p => p.AppUserId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    // Kiểm tra xem videoId đã có trong playlist này chưa
                    IsChecked = p.PlaylistItems.Any(pi => pi.VideoId == videoId)
                }).ToListAsync();

            return Json(new ApiResponse(200, result: playlists));
        }
        [HttpPost]
        public async Task<IActionResult> RemoveVideoFromPlaylist([FromBody] AddRemoveVideoToPlaylistViewModel model)
        {
            var userId = User.GetUserId();

            // Kiểm tra Playlist tồn tại và thuộc về User
            var playlist = await UnitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == model.PlaylistId && p.AppUserId == userId,
                includeProperties: "PlaylistItems");

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            // Kiểm tra PlaylistItem tồn tại
            var playlistItem = await UnitOfWork.PlaylistItemRepo.GetByKeysAsync(model.PlaylistId, model.VideoId);
            if (playlistItem == null)
            {
                return Json(new ApiResponse(400, message: "Video không tồn tại trong Playlist."));
            }

            // Xóa PlaylistItem
            UnitOfWork.PlaylistItemRepo.Remove(playlistItem);
            await UnitOfWork.CompleteAsync();

            // (Tùy chọn) Cập nhật lại OrderIndex cho các video còn lại
            var remainingItems = await Context.PlaylistItems
                .Where(pi => pi.PlaylistId == model.PlaylistId)
                .OrderBy(pi => pi.OrderIndex)
                .ToListAsync();
            for (int i = 0; i < remainingItems.Count; i++)
            {
                remainingItems[i].OrderIndex = i + 1;
            }
            await UnitOfWork.CompleteAsync();

            return Json(new ApiResponse(200, "Removed", $"Đã xóa video khỏi Playlist '{playlist.Name}'."));
        }
        [HttpPost]
        public async Task<IActionResult> DeletePlaylist(Guid id)
        {
            var userId = User.GetUserId();

            // Kiểm tra Playlist tồn tại và thuộc về User
            var playlist = await UnitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == id && p.AppUserId == userId);

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            // Xóa tất cả PlaylistItems liên quan trước
            var playlistItems = await Context.PlaylistItems
                .Where(pi => pi.PlaylistId == id)
                .ToListAsync();
            Context.PlaylistItems.RemoveRange(playlistItems);

            // Xóa Playlist
            UnitOfWork.PlaylistRepo.Remove(playlist);
            await UnitOfWork.CompleteAsync();

            return Json(new ApiResponse(200, "Deleted", $"Đã xóa Playlist '{playlist.Name}'."));
        }
        #endregion
    }
}