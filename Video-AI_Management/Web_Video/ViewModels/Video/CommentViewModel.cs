using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Video
{
    public class CommentViewModel
    {
        public CommentPostViewModel PostComment { get; set; } = new();
        public IEnumerable<AvailableCommentViewModel> AvailableComments { get; set; }
    }
    public class CommentPostViewModel
    {
        [Required]
        public Guid VideoId { get; set; }
        [Required]
        public string Content { get; set; }
    }
    public class AvailableCommentViewModel
    {
        public string Content { get; set; }
        public string FromName { get; set; }
        public Guid FromChannelId { get; set; }
        public DateTime PostedAt { get; set; }
    }
}
