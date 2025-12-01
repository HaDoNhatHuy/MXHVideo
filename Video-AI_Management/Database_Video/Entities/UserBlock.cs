using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.Entities
{
    [Table("UserBlock")]
    public class UserBlock : BaseEntity
    {
        public string AppUserId { get; set; }
        public string Type { get; set; } // "Video" hoặc "Channel"
        public Guid TargetId { get; set; } // VideoId hoặc ChannelId bị chặn
        public DateTime BlockedDate { get; set; } = DateTime.UtcNow;
    }
}
