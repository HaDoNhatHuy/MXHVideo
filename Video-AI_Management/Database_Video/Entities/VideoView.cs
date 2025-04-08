using Database_Video.IRepo;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.Entities
{
    [Table("VideoView")]
    public class VideoView : BaseEntity
    {
        //bridge table between AppUser and Video
        // FK= AppUserId and FK= VideoId
        public string AppUserId { get; set; }
        public Guid VideoId { get; set; }

        //IP2 Location
        public string IpAddress { get; set; }
        public int NumberOfVisit { get; set; } = 1;
        public string? City { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public bool? Is_Proxy { get; set; }
        public DateTime LastVisit { get; set; } = DateTime.UtcNow;

        //Navigation
        public AppUser AppUser { get; set; }
        public Video Video { get; set; }
    }
}
