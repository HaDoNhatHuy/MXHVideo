using Database_Video.Entities;

namespace Database_Video.IRepo
{
    public interface IVideoViewRepo : IBaseRepo<VideoView>
    {
        Task HandleVideoViewAsync(string userId, Guid videoId, string ipAddress);
    }
}
