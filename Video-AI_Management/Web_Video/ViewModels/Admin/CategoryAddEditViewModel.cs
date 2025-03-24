using System;
using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Admin
{
    public class CategoryAddEditViewModel
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; }
    }
}
