using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.Entities
{
    [Table("Comment")]
    public class Comment : BaseEntity, IAuditable
    {
        //FK = AppUserId and FK = VideoId
        public Comment()
        {

        }

        public Comment(Guid videoId, string appUserId, string? content)
        {
            VideoId = videoId;
            AppUserId = appUserId;
            Content = content;
        }

        public Guid VideoId { get; set; }
        public string AppUserId { get; set; }
        public string? Content { get; set; }
        public DateTime? CreatedDate { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        //Navigations
        public AppUser AppUser { get; set; }
        public Video Video { get; set; }
    }
}
