using System;

namespace Web_Video.ViewModels.Moderator
{
    public class VideoDisplayGridViewModel
    {
        public Guid Id { get; set; }
        public string Thumbnail { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
