using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
    }

    // DTO để hiển thị video trong một Playlist cụ thể
    public class PlaylistItemDto
    {
        public Guid VideoId { get; set; }
        public string Title { get; set; }
        public string Thumbnail { get; set; }
        public string ChannelName { get; set; }
        public string Duration { get; set; }
        public int OrderIndex { get; set; }
    }

    // ViewModel tổng thể cho trang xem Playlist
    public class PlaylistWatchViewModel
    {
        public PlaylistDisplayViewModel PlaylistInfo { get; set; }
        public List<PlaylistItemDto> Items { get; set; } = new List<PlaylistItemDto>();
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