using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("PlaylistItem")]
    public class PlaylistItem
    {
        // PK kết hợp (sẽ được cấu hình trong DBContext)
        [ForeignKey("PlaylistId")]
        public Guid PlaylistId { get; set; }
        public Playlist Playlist { get; set; }

        [ForeignKey("VideoId")]
        public Guid VideoId { get; set; }
        public Video Video { get; set; }

        public int OrderIndex { get; set; } // Thứ tự video trong playlist
        public DateTime AddedDate { get; set; } = DateTime.UtcNow;

        public PlaylistItem() { }

        public PlaylistItem(Guid playlistId, Guid videoId, int orderIndex)
        {
            PlaylistId = playlistId;
            VideoId = videoId;
            OrderIndex = orderIndex;
        }
    }
}