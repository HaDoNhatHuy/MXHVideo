using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.Entities
{
    [Table("Category")]
    public class Category : IMeta, IAuditable
    {
        [Key]
        public Guid CategoryId { get; set; }
        [StringLength(50)]
        public string CategoryName { get; set; }
        [ForeignKey("ParentId")]
        public Guid? ParentId { get; set; }
        public Category? Parent { get; set; }
        public string? MetaKeywords { get; set; }
        public string? MetaDescription { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public ICollection<Video> Videos { get; set; } = new HashSet<Video>();
    }
}
