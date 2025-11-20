using System.ComponentModel.DataAnnotations;

namespace Database_Video.Entities
{
    public class BaseEntity
    {
        [Key]
        public Guid Id { get; set; }
    }
}
