using DataAccess.Data;
using Database_Video.Entities;
using Database_Video.IRepo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Extensions;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Playlist;
using Web_Video.ViewModels.Video;
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
        public async Task<IActionResult> WatchPlaylist(Guid id) // id là PlaylistId
        {
            var userId = User.GetUserId(); // Lấy ID người dùng hiện tại [8]

            // Lấy thông tin Playlist và các VideoItems bên trong
            var playlist = await Context.Playlists
                .Include(p => p.PlaylistItems)
                .ThenInclude(pi => pi.Video)
                .Where(p => p.Id == id && p.AppUserId == userId) // Kiểm tra quyền sở hữu [1, 2]
                .FirstOrDefaultAsync();

            if (playlist == null)
            {
                // Thông báo nếu không tìm thấy hoặc không có quyền truy cập
                TempData["notification"] = "false;Not Found;Playlist không tồn tại hoặc bạn không có quyền truy cập"; // [2]
                return RedirectToAction("Index");
            }

            // 1. Lấy video đầu tiên trong playlist theo OrderIndex
            var firstVideoItem = playlist.PlaylistItems
                                    .OrderBy(pi => pi.OrderIndex) // Sắp xếp theo thứ tự [3]
                                    .FirstOrDefault();

            if (firstVideoItem == null)
            {
                // Thông báo nếu playlist rỗng
                TempData["notification"] = "false;Not Found;Playlist này chưa có video nào.";
                return RedirectToAction("Index");
            }

            // 2. CHUYỂN HƯỚNG SANG VideoController.Watch
            // Truyền ID của video đầu tiên (id) và ID của playlist (playlistId)
            return RedirectToAction(
                "Watch",
                "Video",
                new
                {
                    id = firstVideoItem.VideoId,    // ID của video cần xem
                    playlistId = id                 // ID của playlist (dùng để load Partial View)
                });
        }

        // Trang xem full list video in playlist (grid view)
        [HttpGet]
        public async Task<IActionResult> FullList(Guid id)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                TempData["notification"] = "false;Error;Không xác thực được người dùng";
                return RedirectToAction("Index");
            }

            var playlist = await _context.Playlists
                .Include(p => p.PlaylistItems)
                .ThenInclude(pi => pi.Video)
                .ThenInclude(v => v.Channel)
                .Where(p => p.Id == id && p.AppUserId == userId)
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
                    CreatedAtTimeAgo = SD.TimeAgo(playlist.CreatedDate)
                },
                Items = playlist.PlaylistItems
                    .OrderBy(pi => pi.OrderIndex)
                    .Select(pi => new PlaylistItemDto
                    {
                        VideoId = pi.VideoId,
                        Title = pi.Video.Title,
                        Thumbnail = pi.Video.Thumbnail ?? "/default-thumbnail.jpg",
                        ChannelName = pi.Video.Channel.ChannelName,
                        Duration = pi.Video.Duration ?? "0:00",
                        OrderIndex = pi.OrderIndex,
                        CreatedAt = pi.Video.UploadDate
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
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            var newPlaylist = new Playlist
            {
                Id = Guid.NewGuid(),
                Name = model.Name.Trim(),
                AppUserId = userId,
                Description = "",
                CreatedDate = DateTime.UtcNow,
                Privacy = 0
            };

            _unitOfWork.PlaylistRepo.Add(newPlaylist);
            await _unitOfWork.CompleteAsync();

            return Json(new ApiResponse(201, "Created", "Đã tạo danh sách phát thành công.", new { id = newPlaylist.Id, name = newPlaylist.Name }));
        }

        // API lấy danh sách Playlist của người dùng hiện tại
        [HttpGet]
        public async Task<IActionResult> GetUserPlaylists()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            var playlists = await _context.Playlists
                .Where(p => p.AppUserId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new PlaylistDisplayViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    CreatedAtTimeAgo = SD.TimeAgo(p.CreatedDate),
                    VideoCount = p.PlaylistItems.Count(),
                    FirstVideoThumbnail = p.PlaylistItems
                        .OrderBy(pi => pi.OrderIndex)
                        .Select(pi => pi.Video.Thumbnail)
                        .FirstOrDefault() ?? "/default-thumbnail.jpg"
                }).ToListAsync();

            return Json(new ApiResponse(200, result: playlists));
        }

        // API thêm hoặc xóa video khỏi Playlist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleVideoInPlaylist([FromBody] AddRemoveVideoToPlaylistViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ApiResponse(400, message: "Dữ liệu không hợp lệ."));
            }

            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            var playlist = await _unitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == model.PlaylistId && p.AppUserId == userId,
                includeProperties: "PlaylistItems");

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            var videoExists = await _unitOfWork.VideoRepo.AnyAsync(v => v.Id == model.VideoId);
            if (!videoExists)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Video."));
            }

            var playlistItem = await _unitOfWork.PlaylistItemRepo.GetByKeysAsync(model.PlaylistId, model.VideoId);

            if (playlistItem == null)
            {
                int nextOrderIndex = playlist.PlaylistItems.Any()
                    ? playlist.PlaylistItems.Max(pi => pi.OrderIndex) + 1
                    : 1;

                var newPlaylistItem = new PlaylistItem(model.PlaylistId, model.VideoId, nextOrderIndex);
                _unitOfWork.PlaylistItemRepo.Add(newPlaylistItem);
                await _unitOfWork.CompleteAsync();

                return Json(new ApiResponse(200, "Added", $"Đã thêm video vào Playlist '{playlist.Name}'."));
            }
            else
            {
                _unitOfWork.PlaylistItemRepo.Remove(playlistItem);
                await _unitOfWork.CompleteAsync();

                return Json(new ApiResponse(200, "Removed", $"Đã xóa video khỏi Playlist '{playlist.Name}'."));
            }
        }

        // API lấy danh sách Playlist cho modal
        [HttpGet]
        public async Task<IActionResult> GetUserPlaylistsSummary(Guid videoId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            var playlists = await _context.Playlists
                .Where(p => p.AppUserId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    IsChecked = p.PlaylistItems.Any(pi => pi.VideoId == videoId)
                }).ToListAsync();

            return Json(new ApiResponse(200, result: playlists));
        }

        // API xóa video khỏi playlist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveVideoFromPlaylist([FromBody] AddRemoveVideoToPlaylistViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new ApiResponse(400, message: "Dữ liệu không hợp lệ."));
            }

            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            var playlist = await _unitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == model.PlaylistId && p.AppUserId == userId,
                includeProperties: "PlaylistItems");

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            var playlistItem = await _unitOfWork.PlaylistItemRepo.GetByKeysAsync(model.PlaylistId, model.VideoId);
            if (playlistItem == null)
            {
                return Json(new ApiResponse(400, message: "Video không tồn tại trong Playlist."));
            }

            _unitOfWork.PlaylistItemRepo.Remove(playlistItem);
            await _unitOfWork.CompleteAsync();

            // Update OrderIndex for remaining items
            var remainingItems = await _context.PlaylistItems
                .Where(pi => pi.PlaylistId == model.PlaylistId)
                .OrderBy(pi => pi.OrderIndex)
                .ToListAsync();
            for (int i = 0; i < remainingItems.Count; i++)
            {
                remainingItems[i].OrderIndex = i + 1;
            }
            await _unitOfWork.CompleteAsync();

            return Json(new ApiResponse(200, "Removed", $"Đã xóa video khỏi Playlist '{playlist.Name}'."));
        }

        // API xóa playlist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePlaylist(Guid id)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new ApiResponse(401, message: "Không xác thực được người dùng."));
            }

            Console.WriteLine($"Delete attempt: Id={id}, UserId={userId}");  // Log debug

            var playlist = await _unitOfWork.PlaylistRepo.GetFirstOrDefaultAsync(p =>
                p.Id == id && p.AppUserId == userId);

            if (playlist == null)
            {
                return Json(new ApiResponse(404, message: "Không tìm thấy Playlist."));
            }

            // Xóa PlaylistItems trước
            var playlistItems = await _context.PlaylistItems
                .Where(pi => pi.PlaylistId == id)
                .ToListAsync();
            _context.PlaylistItems.RemoveRange(playlistItems);

            // Xóa Playlist
            _unitOfWork.PlaylistRepo.Remove(playlist);
            await _unitOfWork.CompleteAsync();

            return Json(new ApiResponse(200, "Deleted", $"Đã xóa Playlist '{playlist.Name}'."));
        }
        #endregion
    }
}