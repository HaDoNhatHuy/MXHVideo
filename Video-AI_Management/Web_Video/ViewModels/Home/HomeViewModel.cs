using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace Web_Video.ViewModels.Home
{
    public class HomeViewModel
    {
        public string Page { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; }
        public IEnumerable<SelectListItem> CategoryDropdown { get; set; }
    }
}
