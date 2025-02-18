using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("LikeDislike")]
    public class LikeDislike
    {
        //PK(AppUserId,VideoId)
        //FK = AppUserId and FK = VideoId
        public Guid VideoId { get; set; }
        public string AppUserId { get; set; }
        public bool Liked { get; set; } = true;
        //Navigations
        public Video Video { get; set; }
        public AppUser AppUser { get; set; }
    }
}
