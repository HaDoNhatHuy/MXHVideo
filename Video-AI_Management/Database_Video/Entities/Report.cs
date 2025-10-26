using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Report")]
    public class Report : BaseEntity
    {
        // Khóa ngoại tới Video bị báo cáo
        public Guid VideoId { get; set; }
        public Video Video { get; set; } // Navigation property [3]

        // Khóa ngoại tới người báo cáo
        public string AppUserId { get; set; }
        public AppUser AppUser { get; set; } // Navigation property [4]

        // Nội dung khiếu nại
        public string? Reason { get; set; }

        // Trạng thái báo cáo
        public string Status { get; set; } = "New"; // New, Reviewed, Closed, etc.

        // Cờ kích hoạt chức năng làm mờ (Blurring) cho video này
        public bool IsBlurringActivated { get; set; } = false;
        // THÊM MỚI: Tên Celebrity bị tố cáo (Nếu Report liên quan đến Celeb)
        public string? ReportedCelebrityName { get; set; }

        public DateTime ReportedDate { get; set; } = DateTime.UtcNow;
    }
}
