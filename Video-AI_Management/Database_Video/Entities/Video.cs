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
        public string? Duration { get; set; } // Thêm trường Duration (ví dụ: "03:45")
        public int? Views { get; set; }
        [ForeignKey("CategoryId")]
        public Guid? CategoryId { get; set; }
        public Category? Category { get; set; }
        [ForeignKey("ChannelId")]
        public Guid? ChannelId { get; set; }
        public Channel? Channel { get; set; }
        public VideoFile VideoFile { get; set; }
        public string? RecognizedCelebrities { get; set; }
        public string? CelebrityFrames { get; set; }  // Thêm mới: Lưu JSON như "{\"Celeb1\": [{\"time\": 5.2, \"frame\": \"base64_data\"}, ...], ...}"
        public bool IsBlurActivated { get; set; } = false; // THÊM MỚI: Cờ bật làm mờ
        public ICollection<RecognizeCelebrities> RecognizeCelebrities { get; set; } = new HashSet<RecognizeCelebrities>();
        public ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();
        public ICollection<LikeDislike> LikeDislikes { get; set; } = new HashSet<LikeDislike>();
        public ICollection<VideoView> Viewers { get; set; } = new HashSet<VideoView>();
    }
}