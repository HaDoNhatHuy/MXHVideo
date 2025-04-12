using Database_Video.DTOs;
using Database_Video.Entities;
using Database_Video.Pagination;

namespace Database_Video.IRepo
{
    public interface IVideoRepo : IBaseRepo<Video>
    {
        Task<string> GetUserIdByVideoIdAsync(Guid videoId);
        Task<PaginatedList<VideoGridChannelDto>> GetVideosForChannelGridAsync(Guid channelId, BaseParameters parameters);
        Task<PaginatedList<VideoForHomeGridDto>> GetVideosForHomeGridAsync(HomeParameters parameters);
        Task RemoveVideoAsync(Guid videoId);
    }
}
