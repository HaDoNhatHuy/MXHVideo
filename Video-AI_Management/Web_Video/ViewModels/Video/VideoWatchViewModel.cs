using System;

namespace Web_Video.ViewModels.Video
{
    public class VideoWatchViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; }
        public bool IsSubscribed { get; set; }
        public bool IsLiked { get; set; }
        public bool IsDisiked { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public int ViewersCount { get; set; }
        public int SubscribersCount { get; set; }
        public CommentViewModel CommentVM { get; set; } = new();
        // Thêm thuộc tính mới
        public string RecognizedCelebrities { get; set; }
    }
}
