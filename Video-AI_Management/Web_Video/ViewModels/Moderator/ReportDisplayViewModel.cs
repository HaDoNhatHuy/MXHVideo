using System;

namespace Web_Video.ViewModels.Moderator
{
    public class ReportDisplayViewModel
    {
        public Guid Id { get; set; }
        public Guid VideoId { get; set; }
        public string VideoTitle { get; set; }
        public string ThumbnailUrl { get; set; }
        public string ReportedByUserId { get; set; }
        public string ReportedByUserName { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public bool IsBlurringActivated { get; set; }
        public DateTime ReportedDate { get; set; }
        public string ReportedCelebrityName { get; set; } 
    }
}
