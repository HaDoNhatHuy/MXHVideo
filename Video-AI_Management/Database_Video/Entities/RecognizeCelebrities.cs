using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("RecognizeCelebrities")]
    public class RecognizeCelebrities
    {
        [ForeignKey("VideoId")]
        public Guid? VideoId { get; set; }
        public Video? Video { get; set; }
        [ForeignKey("CelebrityId")]
        public Guid? CelebrityId { get; set; }
        public Celebrity? Celebrity { get; set; }
    }
}
