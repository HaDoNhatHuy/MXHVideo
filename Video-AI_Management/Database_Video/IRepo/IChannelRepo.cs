using Database_Video.Entities;

namespace Database_Video.IRepo
{
    public interface IChannelRepo : IBaseRepo<Channel>
    {
        Task<Guid> GetChannelIdByUserId(string userId);
    }
}
