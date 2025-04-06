using DataAccess.Data;
using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.IRepo;
using Database_Video.Pagination;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebVideo.Utility;

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
                .Where(x => x.Id == videoId)
                .Select(x => x.Channel.AppUserId)
                .FirstOrDefaultAsync();
        }

        public async Task<PaginatedList<VideoGridChannelDto>> GetVideosForChannelGridAsync(Guid channelId, BaseParameters parameters)
        {
            var query = _context.Videos
                .Include(x => x.Category)
                .Where(x => x.ChannelId == channelId)
                .Select(x => new VideoGridChannelDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    Thumbnail = x.Thumbnail,
                    CreatedAt = x.UploadDate,
                    CategoryName = x.Category.CategoryName,
                    Views = SD.GetRandomNumber(1000, 50000, x.Id.GetHashCode()),// chuyển Guid thành int
                    Comments = SD.GetRandomNumber(1, 100, x.Id.GetHashCode()),// chuyển Guid thành int
                    Likes = SD.GetRandomNumber(10, 100, x.Id.GetHashCode()),// chuyển Guid thành int
                    Dislikes = SD.GetRandomNumber(5, 50, x.Id.GetHashCode()),// chuyển Guid thành int

                })
                .AsQueryable();
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
            return await PaginatedList<VideoGridChannelDto>.CreateAsync(query.AsNoTracking(), parameters.PageNumber, parameters.PageSize);
        }

        public async Task<PaginatedList<VideoForHomeGridDto>> GetVideosForHomeGridAsync(HomeParameters parameters)
        {
            var query = _context.Videos
                .Select(x => new VideoForHomeGridDto
                {
                    Id = x.Id,
                    Thumbnail = x.Thumbnail,
                    Title = x.Title,
                    Description = x.Description,
                    CreatedAt = x.UploadDate,
                    ChannelName = x.Channel.ChannelName,
                    ChannelId = x.Channel.Id,
                    CategoryId = x.Category.Id,
                    Views = SD.GetRandomNumber(1000, 50000, x.Id.GetHashCode()),// chuyển Guid thành int
                })
                .AsQueryable();
            if (parameters.CategoryId != Guid.Empty)
            {
                query = query.Where(x => x.CategoryId == parameters.CategoryId);
            }
            if (!string.IsNullOrEmpty(parameters.SearchBy))
            {
                query = query.Where(x => x.Title.ToLower().Contains(parameters.SearchBy) || x.Description.ToLower().Contains(parameters.SearchBy));
            }
            return await PaginatedList<VideoForHomeGridDto>.CreateAsync(query.AsNoTracking(), parameters.PageNumber, parameters.PageSize);
        }

        public async Task RemoveVideoAsync(Guid videoId)
        {
            var video = await GetFirstOrDefaultAsync(x => x.Id == videoId, "Comments,LikeDislikes,Viewers");
            if (video != null)
            {
                if (video.Viewers != null)
                {
                    _context.VideoViews.RemoveRange(video.Viewers);
                }
                if (video.Comments != null)
                {
                    _context.Comments.RemoveRange(video.Comments);
                }
                if (video.LikeDislikes != null)
                {
                    _context.LikeDislikes.RemoveRange(video.LikeDislikes);
                }
                Remove(video);
            }
        }
    }
}
