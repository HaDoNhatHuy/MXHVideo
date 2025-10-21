using Database_Video.Entities;
using System.Threading.Tasks;

namespace Database_Video.IRepo
{
    public interface IPlaylistRepo : IBaseRepo<Playlist>
    {
        // Có thể thêm các phương thức tìm kiếm đặc biệt tại đây nếu cần
    }
}