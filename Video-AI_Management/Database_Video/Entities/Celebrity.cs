using System.ComponentModel.DataAnnotations.Schema;

namespace Database_Video.Entities
{
    [Table("Celebrity")]
    public class Celebrity : BaseEntity
    {
        public string Name { get; set; }
        public string? Age { get; set; }
        public string? Gender { get; set; }
        public string? Job { get; set; }
        public ICollection<RecognizeCelebrities> RecognizeCelebrities { get; set; } = new HashSet<RecognizeCelebrities>();
    }
}
