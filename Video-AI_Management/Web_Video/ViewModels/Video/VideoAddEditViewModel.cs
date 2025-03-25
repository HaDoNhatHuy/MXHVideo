using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Video.ViewModels.Video
{
    public class VideoAddEditViewModel
    {
        public Guid Id { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        public string Description { get; set; }
        [Display(Name = "Upload thumbnail here")]
        public IFormFile ImageUpload { get; set; }
        [Display(Name = "Upload your video here")]
        public IFormFile VideoUpload { get; set; }
        [Display(Name = "Choose the category for your video")]
        [Required(ErrorMessage = "Please choose a category")]
        public Guid? CategoryId { get; set; }
        public IEnumerable<SelectListItem> CategoryDropdown { get; set; }
        public string ImageContentTypes { get; set; }
        public string VideoContentTypes { get; set; }
        public string ImageUrl { get; set; }
    }
}
