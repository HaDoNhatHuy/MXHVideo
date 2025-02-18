using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Subscribe")]
    public class Subscribe
    {
        //PK (AppUserId, ChannelId)
        //FK = AppUserId and FK = ChannelId
        public string AppUserId { get; set; }
        public Guid ChannelId { get; set; }
        //Navigations
        public AppUser AppUser { get; set; }
        public Channel Channel { get; set; }
    }
}
