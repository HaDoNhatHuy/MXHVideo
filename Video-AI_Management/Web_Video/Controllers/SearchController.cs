using Database_Video.Entities;
using DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Web_Video.Controllers
{
    public class SearchController : Controller
    {
        private readonly DataContext _context;

        public SearchController(DataContext context)
        {
            _context = context;
        }

        // Xử lý yêu cầu GET: /Search?query=...
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ViewData["Query"] = "";
                return View(new List<Video>());
            }

            var videos = await _context.Videos
                .Include(v => v.Category) // Bao gồm Category để hiển thị tên danh mục
                .Include(v => v.Channel)  // Bao gồm Channel để hiển thị thông tin kênh
                .Where(v => v.Title != null && v.Title.ToLower().Contains(query.ToLower()))
                .ToListAsync();

            ViewData["Query"] = query;
            return View(videos);
        }
    }
}