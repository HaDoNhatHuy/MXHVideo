using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Web_Video.ViewModels.Video;

namespace Web_Video.ViewModels.Playlist
{
    // Model cho việc tạo/chỉnh sửa Playlist
    public class PlaylistAddEditViewModel
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Tên danh sách phát là bắt buộc")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên phải từ {2} đến {1} ký tự")]
        public string Name { get; set; }
    }

    // DTO để hiển thị danh sách Playlist của người dùng
    public class PlaylistDisplayViewModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int VideoCount { get; set; }
        public string CreatedAtTimeAgo { get; set; }
        public string FirstVideoThumbnail { get; set; } // Thumbnail của video đầu tiên
        public string Description { get; set; }  // Add this
    }

    // DTO để hiển thị video trong một Playlist cụ thể
    public class PlaylistItemDto
    {
        public Guid VideoId { get; set; }
        public string Title { get; set; }
        public string Thumbnail { get; set; }
        public string ChannelName { get; set; }
        public Guid ChannelId { get; set; }  // Add for channel link
        public string ChannelAvatar { get; set; } // Thêm ChannelAvatar
        public string Duration { get; set; }
        public int OrderIndex { get; set; }
        public string Description { get; set; }  // Add for description
        public string CategoryName { get; set; }  // Add for category
        public string RecognizedCelebrities { get; set; }  // Add for cast/tags
        public string CelebrityFramesJson { get; set; }  // Add for frames
        public DateTime CreatedAt { get; set; }  // Add for publish date
        public Dictionary<string, List<VideoWatchViewModel.CelebrityFrame>> CelebrityFrames { get; set; } // Thêm CelebrityFrames
    }

    // ViewModel tổng thể cho trang xem Playlist
    public class PlaylistWatchViewModel
    {
        public PlaylistDisplayViewModel PlaylistInfo { get; set; }
        public List<PlaylistItemDto> Items { get; set; } = new List<PlaylistItemDto>();
        public List<VideoWatchViewModel.RecommendedVideoViewModel> RecommendedVideos { get; set; } // Sử dụng RecommendedVideoViewModel từ Video namespace
        public CommentViewModel CommentVM { get; set; } = new CommentViewModel(); // Thêm CommentVM
    }

    // Model cho API thêm/xóa video khỏi playlist
    public class AddRemoveVideoToPlaylistViewModel
    {
        [Required]
        public Guid PlaylistId { get; set; }
        [Required]
        public Guid VideoId { get; set; }
    }
}