using Database_Video.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccess.Data.Config
{
    public class UserBlockConfig : IEntityTypeConfiguration<UserBlock>
    {
        public void Configure(EntityTypeBuilder<UserBlock> builder)
        {
            // Cấu hình mối quan hệ giữa UserBlock và Video.
            // Vì UserBlock.TargetId là một Foreign Key đa năng (polymorphic) 
            // có thể trỏ đến cả Video.Id hoặc Channel.Id [2],
            // chúng ta định nghĩa một mối quan hệ Ngầm (Shadow relationship) 
            // để chỉ rõ TargetId đang tham chiếu đến Video.Id.

            builder.HasOne<Video>()
                   .WithMany()
                   // Sử dụng TargetId làm khóa ngoại trỏ tới Id của Video
                   .HasForeignKey(u => u.TargetId)
                   // QUAN TRỌNG: Thiết lập hành vi CASCADE DELETE.
                   // Khi Video (bản ghi cha) bị xóa, các bản ghi UserBlock liên quan 
                   // (bản ghi con có TargetId = VideoId) sẽ tự động bị xóa.
                   .OnDelete(DeleteBehavior.Cascade);

            // Lưu ý: Nếu sau này bạn muốn Channel bị xóa cũng tự động xóa UserBlock
            // liên quan, bạn cần định nghĩa thêm một cấu hình tương tự cho Channel.
        }
    }
}
