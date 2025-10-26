using System;
using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Video
{
    public class ReportViewModel
    {
        [Required]
        public Guid VideoId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn lý do khiếu nại.")]
        public string Reason { get; set; }

        public string? OtherReason { get; set; }
        // THÊM: Để chọn celeb nếu reason là celeb-related
        public string ReportedCelebrityName { get; set; }
    }
}