using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.IRepo;
using Database_Video.Pagination;
using Microsoft.EntityFrameworkCore;
using WebVideo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DataAccess.Repo
{
    public class VideoRepo : BaseRepo<Video>, IVideoRepo
    {
        private readonly DataContext _context;

        public VideoRepo(DataContext context) : base(context)
        {
            _context = context;
        }

        public async Task<string> GetUserIdByVideoIdAsync(Guid videoId)
        {
            return await _context.Videos
                .AsNoTracking()
                .Where(x => x.Id == videoId)
                .Select(x => x.Channel.AppUserId)
                .FirstOrDefaultAsync();
        }

        public async Task<PaginatedList<VideoGridChannelDto>> GetVideosForChannelGridAsync(
            Guid channelId,
            BaseParameters parameters)
        {
            // Tối ưu: Chỉ select các field cần thiết
            var query = _context.Videos
                .AsNoTracking()
                .Where(x => x.ChannelId == channelId)
                .Select(x => new VideoGridChannelDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail != null
                        ? x.Thumbnail.Replace("\\", "/")
                        : "/avatarUser/avt-default.jpg",
                    Duration = x.Duration ?? "0:00",
                    CreatedAt = x.UploadDate,
                    CategoryName = x.Category.CategoryName,
                    Views = x.Viewers.Sum(v => v.NumberOfVisit),
                    Comments = x.Comments.Count(),
                    Likes = x.LikeDislikes.Count(l => l.Liked == true),
                    Dislikes = x.LikeDislikes.Count(l => l.Liked == false),
                });

            // Áp dụng sorting
            query = parameters.SortBy switch
            {
                "title-a" => query.OrderBy(x => x.Title),
                "title-d" => query.OrderByDescending(x => x.Title),
                "date-a" => query.OrderBy(u => u.CreatedAt),
                "date-d" => query.OrderByDescending(u => u.CreatedAt),
                "views-a" => query.OrderBy(u => u.Views),
                "views-d" => query.OrderByDescending(u => u.Views),
                "comments-a" => query.OrderBy(u => u.Comments),
                "comments-d" => query.OrderByDescending(u => u.Comments),
                "likes-a" => query.OrderBy(u => u.Likes),
                "likes-d" => query.OrderByDescending(u => u.Likes),
                "dislikes-a" => query.OrderBy(u => u.Dislikes),
                "dislikes-d" => query.OrderByDescending(u => u.Dislikes),
                "category-a" => query.OrderBy(u => u.CategoryName),
                "category-d" => query.OrderByDescending(u => u.CategoryName),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };

            return await PaginatedList<VideoGridChannelDto>.CreateAsync(
                query,
                parameters.PageNumber,
                parameters.PageSize);
        }

        public async Task<PaginatedList<VideoForHomeGridDto>> GetVideosForHomeGridAsync(
            HomeParameters parameters)
        {
            // Tối ưu: Sử dụng projection để giảm data load
            var query = _context.Videos
                .AsNoTracking()
                .Select(x => new VideoForHomeGridDto
                {
                    Id = x.Id,
                    Thumbnail = x.Thumbnail,
                    Duration = x.Duration ?? "0:00",
                    Title = x.Title,
                    Description = x.Description,
                    CreatedAt = x.UploadDate,
                    ChannelName = x.Channel.ChannelName,
                    ChannelId = x.Channel.Id,
                    CategoryId = x.Category.Id,
                    Views = x.Viewers.Sum(v => v.NumberOfVisit),
                    CreatedAtTimeAgo = SD.TimeAgo(x.UploadDate)
                })
                .AsQueryable();

            // Filter by category
            if (parameters.CategoryId != Guid.Empty)
            {
                query = query.Where(x => x.CategoryId == parameters.CategoryId);
            }

            // Filter by search
            if (!string.IsNullOrEmpty(parameters.SearchBy) &&
                parameters.SearchBy.ToLower() != "all")
            {
                string searchLower = parameters.SearchBy.ToLower();
                query = query.Where(x =>
                    x.Title.ToLower().Contains(searchLower) ||
                    x.Description.ToLower().Contains(searchLower));
            }

            return await PaginatedList<VideoForHomeGridDto>.CreateAsync(
                query,
                parameters.PageNumber,
                parameters.PageSize);
        }

        public async Task RemoveVideoAsync(Guid videoId)
        {
            // Tối ưu: Sử dụng batch delete thay vì load tất cả vào memory
            var video = await _context.Videos
                .FirstOrDefaultAsync(x => x.Id == videoId);

            if (video != null)
            {
                // Xóa related entities bằng raw SQL để tăng performance
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM VideoViews WHERE VideoId = {videoId}");

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM Comments WHERE VideoId = {videoId}");

                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM LikeDislikes WHERE VideoId = {videoId}");

                // Xóa video
                _context.Videos.Remove(video);
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Lấy video phổ biến nhất (cho trending)
        /// </summary>
        public async Task<List<Video>> GetTrendingVideosAsync(int count = 20)
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            return await _context.Videos
                .AsNoTracking()
                .Include(v => v.Channel)
                .Include(v => v.Category)
                .Where(v => v.UploadDate >= sevenDaysAgo)
                .OrderByDescending(v => v.Viewers.Sum(vv => vv.NumberOfVisit))
                .ThenByDescending(v => v.LikeDislikes.Count(l => l.Liked == true))
                .Take(count)
                .ToListAsync();
        }

        /// <summary>
        /// Tìm video liên quan (cho recommendation)
        /// </summary>
        public async Task<List<Video>> GetRelatedVideosAsync(Guid videoId, int count = 12)
        {
            var video = await _context.Videos
                .AsNoTracking()
                .Include(v => v.Category)
                .FirstOrDefaultAsync(v => v.Id == videoId);

            if (video == null)
                return new List<Video>();

            return await _context.Videos
                .AsNoTracking()
                .Include(v => v.Channel)
                .Include(v => v.Category)
                .Where(v => v.Id != videoId &&
                           v.Category.Id == video.Category.Id)
                .OrderByDescending(v => v.Viewers.Sum(vv => vv.NumberOfVisit))
                .Take(count)
                .ToListAsync();
        }
    }
}