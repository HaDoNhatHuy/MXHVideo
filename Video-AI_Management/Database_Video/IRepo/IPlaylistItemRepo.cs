using Database_Video.Entities;

namespace Database_Video.IRepo
{
    public interface IPlaylistItemRepo // Không cần IBaseRepo vì không dùng Guid PK
    {
        void Add(PlaylistItem entity);
        void Remove(PlaylistItem entity);
        Task<PlaylistItem> GetByKeysAsync(Guid playlistId, Guid videoId);
        // ... (có thể thêm các phương thức khác)
    }
}