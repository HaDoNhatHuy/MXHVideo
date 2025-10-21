using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Playlist")]
    public class Playlist : BaseEntity // Kế thừa từ BaseEntity (Guid Id)
    {
        public string Name { get; set; }

        public string? Description { get; set; }

        // FK tới người sở hữu
        [ForeignKey("AppUserId")]
        public string AppUserId { get; set; }
        public AppUser AppUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        // Tập hợp các video trong playlist
        public ICollection<PlaylistItem> PlaylistItems { get; set; } = new HashSet<PlaylistItem>();
        public int Privacy { get; set; } = 0; // Default Public (0: Public, 1: Private, 2: Unlisted)
    }
}

 