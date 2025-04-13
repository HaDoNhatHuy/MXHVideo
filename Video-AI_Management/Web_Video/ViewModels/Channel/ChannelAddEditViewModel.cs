using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Channel
{
    public class ChannelAddEditViewModel
    {
        [Required]
        [Display(Name = "Channel name")]
        [RegularExpression("^[a-zA-Z]{3,15}", ErrorMessage = "Name must be between 3 and 15 characters long and can only contain letters (A-Z, a-z)")]
        public string Name { get; set; }

        [Required(ErrorMessage = "About field is required")]
        [StringLength(200, MinimumLength = 20, ErrorMessage = "About must be at least {2}, and maximum {1} characters")]
        [Display(Name = "About your channel")]
        public string About { get; set; }
        public List<ModelErrorViewModel> Errors { get; set; } = new List<ModelErrorViewModel>();
        public int SubcribersCount { get; set; }
        public IFormFile Avatar { get; set; } // Thêm thuộc tính để upload avatar
        public string AvatarUrl { get; set; } // URL của avatar hiện tại (dùng khi chỉnh sửa)
        public DateTime CreatedDate { get; set; } 
    }
}
