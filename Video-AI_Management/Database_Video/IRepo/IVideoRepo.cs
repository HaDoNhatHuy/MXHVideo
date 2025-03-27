using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.Pagination;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.IRepo
{
    public interface IVideoRepo : IBaseRepo<Video>
    {
        Task<string> GetUserIdByVideoId(Guid videoId);
        Task<PaginatedList<VideoGridChannelDto>> GetVideosForChannelGrid(Guid channelId, BaseParameters parameters);
    }
}
