using System;
using System.Collections.Generic;
using Web_Video.ViewModels.Playlist; // <=== THÊM DÒNG NÀY

namespace Web_Video.ViewModels.Video
{
    public class VideoWatchViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Thumbnail { get; set; }
        // THÊM MỚI: Đường dẫn phụ đề
        public string? SubtitleUrl { get; set; }
        public Guid ChannelId { get; set; }
        public string ChannelName { get; set; }
        public string ChannelAvatar { get; set; }
        public string CategoryName { get; set; } // thêm trường CategoryName
        public bool IsSubscribed { get; set; }
        public bool IsLiked { get; set; }
        public bool IsDisiked { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public int ViewersCount { get; set; }
        public int SubscribersCount { get; set; }
        public CommentViewModel CommentVM { get; set; } = new();
        public float? ProgressSeconds { get; set; } // Thêm trường ProgressSeconds
        public string VideoContentType { get; set; } // Lưu ContentType của video
        public string RecognizedCelebrities { get; set; }
        public string CelebrityFramesJson { get; set; } // JSON thô từ DB
        public Dictionary<string, List<CelebrityFrame>> CelebrityFrames { get; set; } // Dữ liệu đã parse
        // === THÊM CÁC TRƯỜNG CHO PLAYLIST ===
        public Guid? CurrentPlaylistId { get; set; } // ID của playlist hiện tại
        public string CurrentPlaylistName { get; set; } // Tên playlist
        public List<PlaylistItemDto> CurrentPlaylistItems { get; set; } = new List<PlaylistItemDto>(); // Danh sách video trong playlist
        public class CelebrityFrame
        {
            public float Time { get; set; } // Giây xuất hiện
            public string FrameImage { get; set; } // Dữ liệu base64 của ảnh frame
        }

        // Thêm thuộc tính cho danh sách video đề xuất
        public List<RecommendedVideoViewModel> RecommendedVideos { get; set; }

        // ViewModel cho mỗi video đề xuất
        public class RecommendedVideoViewModel
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
            public string Thumbnail { get; set; } // Đường dẫn đến thumbnail của video
            public string ChannelName { get; set; }
            public string Duration { get; set; } // Thêm trường Duration (ví dụ: "03:45")
            public int ViewersCount { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
