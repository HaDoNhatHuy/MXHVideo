using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("VideoView")]
    public class VideoView : BaseEntity
    {
        //bridge table between AppUser and Video
        // FK= AppUserId and FK= VideoId
        public string AppUserId { get; set; }
        public Guid VideoId { get; set; }

        //IP2 Location
        public string IpAddress { get; set; }
        public int NumberOfVisit { get; set; } = 1;
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool? Is_Proxy { get; set; }
        public float? ProgressSeconds { get; set; } = 0; // Thêm trường này để lưu tiến độ (tính bằng giây)
        public DateTime LastVisit { get; set; } = DateTime.Now;
        public DateTime ViewDate { get; set; } = DateTime.UtcNow.Date;  // Mới: Để phân biệt entry theo ngày
        // THÊM TRƯỜNG REFERER MỚI
        public string? RefererUrl { get; set; }

        //Navigation
        public AppUser AppUser { get; set; }
        public Video Video { get; set; }
    }
}
