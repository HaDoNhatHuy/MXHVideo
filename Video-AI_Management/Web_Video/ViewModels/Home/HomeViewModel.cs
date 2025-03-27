using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Web_Video.ViewModels.Home
{
    public class HomeViewModel
    {
        public string Page { get; set; }
        public IEnumerable<SelectListItem> CategoryDropdown { get; set; }
    }
}
