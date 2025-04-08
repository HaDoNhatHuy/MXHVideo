using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Video")]
    public class Video : BaseEntity
    {
        [Required]
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? VideoUrl { get; set; }
        [Required]
        public string? Thumbnail { get; set; }
        public DateTime UploadDate { get; set; } = DateTime.Now;        
        public int? Views { get; set; }
        [ForeignKey("CategoryId")]
        public Guid? CategoryId { get; set; }
        public Category? Category { get; set; }
        [ForeignKey("ChannelId")]
        public Guid? ChannelId { get; set; }
        public Channel? Channel { get; set; }
        public VideoFile VideoFile { get; set; }
        public string? RecognizedCelebrities { get; set; }
        public ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();
        public ICollection<LikeDislike> LikeDislikes { get; set; } = new HashSet<LikeDislike>();
        public ICollection<VideoView> Viewers { get; set; } = new HashSet<VideoView>();
    }
}
