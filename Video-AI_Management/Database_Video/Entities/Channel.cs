using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Channel")]
    public class Channel : BaseEntity
    {
        public string? ChannelName { get; set; }
        public string? About { get; set; }
        public DateTime? CreatedDate { get; set; } = DateTime.UtcNow;
        [ForeignKey("AppUserId")]
        public string? AppUserId { get; set; }
        public AppUser? AppUser { get; set; }
        public ICollection<Video> Videos { get; set; } = new HashSet<Video>();
        public ICollection<Subscribe> Subscribers { get; set; } = new HashSet<Subscribe>();
    }
}
