using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("LikeDislike")]
    public class LikeDislike
    {
        public LikeDislike()
        {

        }
        public LikeDislike(Guid videoId, string appUserId, bool liked)
        {
            VideoId = videoId;
            AppUserId = appUserId;
            Liked = liked;
        }

        //PK(AppUserId,VideoId)
        //FK = AppUserId and FK = VideoId
        public Guid VideoId { get; set; }
        public string AppUserId { get; set; }
        public bool Liked { get; set; }
        //Navigations
        public Video Video { get; set; }
        public AppUser AppUser { get; set; }
    }
}
