using System;
using System.Threading.Tasks;
using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Web_Video.Extensions;
using Web_Video.ViewModels;
using Web_Video.ViewModels.Home;
using Database_Video.Pagination;
using Database_Video.DTOs;
using WebVideo.Utility;
using System.Linq;
using Web_Video.ViewModels.Channel;

namespace Web_Video.Controllers
{
    public class CategoryController : CoreController
    {
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(DataContext context, ILogger<CategoryController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index(Guid categoryId)
        {
            var viewModel = new HomeViewModel();
            var category = await Context.Categories
                .Where(c => c.Id == categoryId)
                .Select(c => new { c.Id, c.CategoryName })
                .FirstOrDefaultAsync();

            if (category == null)
            {
                _logger.LogWarning("Category not found for ID: {CategoryId}", categoryId);
                return NotFound("Danh mục không tồn tại.");
            }

            viewModel.CategoryId = categoryId;
            viewModel.CategoryName = category.CategoryName;
            return View(viewModel);
        }

        [Authorize(Roles = $"{SD.UserRole}")]
        [HttpGet]
        public async Task<IActionResult> GetVideosByCategory(Guid categoryId, int pageNumber = 1, int pageSize = 12)
        {
            _logger.LogInformation("GetVideosByCategory called for category {CategoryId}, page {PageNumber}", categoryId, pageNumber);

            try
            {
                var query = Context.Videos
                    .Include(v => v.Channel)
                    .Where(v => v.CategoryId == categoryId && v.ChannelId != null)
                    .Select(v => new VideoForHomeGridDto
                    {
                        Id = v.Id,
                        Title = v.Title,
                        Thumbnail = v.Thumbnail != null ? v.Thumbnail.Replace("\\", "/") : "/img/default-thumbnail.jpg",
                        Description = v.Description,
                        ChannelName = v.Channel != null ? v.Channel.ChannelName : "Unknown Channel",
                        ChannelId = v.ChannelId ?? Guid.Empty,
                        CategoryId = v.CategoryId ?? Guid.Empty,
                        Views = v.Viewers != null ? v.Viewers.Sum(vv => vv.NumberOfVisit) : 0, // ✅ Sửa thành Sum(NumberOfVisit)                        CreatedAtTimeAgo = SD.TimeAgo(v.UploadDate),
                        CreatedAt = v.UploadDate,
                        Duration = v.Duration ?? "0:00"
                    });

                var totalItems = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                var items = await query
                    .OrderByDescending(v => v.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                _logger.LogInformation("Videos count for category {CategoryId}: {Count}/{TotalItems}, page {PageNumber}/{TotalPages}",
                    categoryId, items.Count, totalItems, pageNumber, totalPages);

                var paginatedResults = new PaginatedResult<VideoForHomeGridDto>(
                    items, totalItems, pageNumber, pageSize, totalPages);

                return Json(new ApiResponse(200, result: paginatedResults));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting videos for category {CategoryId}", categoryId);
                return Json(new ApiResponse(500, message: "Có lỗi khi tải video."));
            }
        }
    }
}