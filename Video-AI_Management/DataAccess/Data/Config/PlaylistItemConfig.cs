using Database_Video.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class PlaylistItemConfig : IEntityTypeConfiguration<PlaylistItem>
    {
        public void Configure(EntityTypeBuilder<PlaylistItem> builder)
        {
            // Định nghĩa khóa chính kết hợp: PlaylistId và VideoId
            builder.HasKey(x => new { x.PlaylistId, x.VideoId });

            // Cấu hình mối quan hệ
            // Mối quan hệ Playlist - PlaylistItem (1 Playlist có nhiều PlaylistItem)
            builder.HasOne(p => p.Playlist)
                   .WithMany(pi => pi.PlaylistItems)
                   .HasForeignKey(p => p.PlaylistId)
                   .OnDelete(DeleteBehavior.Cascade); // Nếu xóa Playlist thì xóa PlaylistItem

            // Mối quan hệ Video - PlaylistItem (1 Video có thể có trong nhiều PlaylistItem)
            builder.HasOne(v => v.Video)
                   .WithMany() // Vì Video không cần Navigation Collection ngược lại
                   .HasForeignKey(v => v.VideoId)
                   .OnDelete(DeleteBehavior.Restrict); // Không xóa Video nếu nó đang trong Playlist (giống như Comment [5] hoặc LikeDislike [6])
        }
    }
}