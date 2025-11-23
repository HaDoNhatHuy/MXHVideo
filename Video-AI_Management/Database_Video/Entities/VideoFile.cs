using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    public class VideoFile : BaseEntity
    {
        [Required]
        public string ContentType { get; set; }
        [Required]
        //public byte[] Contents { get; set; }
        // Thay byte[] bằng đường dẫn file
        public string FilePath { get; set; }
        [Required]
        public string Extension { get; set; }
        public Guid VideoId { get; set; }
        //Navigation property
        [ForeignKey("VideoId")]
        public Video Video { get; set; }
    }
}
