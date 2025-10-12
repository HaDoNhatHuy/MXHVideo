using Database_Video.Entities;
using DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Web_Video.Controllers
{
    public class SearchController : CoreController
    {
        private readonly DataContext _context;

        public SearchController(DataContext context)
        {
            _context = context;
        }

        // ViewModel để chứa kết quả tìm kiếm
        public class SearchViewModel
        {
            public List<Video> Videos { get; set; } = new List<Video>();
            public List<Channel> Channels { get; set; } = new List<Channel>();
        }

        // Xử lý yêu cầu GET: /Search?query=...
        public async Task<IActionResult> Index(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ViewData["Query"] = "";
                return View(new SearchViewModel());
            }

            // Tìm kiếm video
            var videos = await _context.Videos
                .Include(v => v.Category)
                .Include(v => v.Channel)
                .Where(v => (v.Title != null && v.Title.ToLower().Contains(query.ToLower())) ||
                            (v.Channel != null && v.Channel.ChannelName != null && v.Channel.ChannelName.ToLower().Contains(query.ToLower())))
                .ToListAsync();

            // Tìm kiếm kênh
            var channels = await _context.Channels
                .Where(c => c.ChannelName != null && c.ChannelName.ToLower().Contains(query.ToLower()))
                .ToListAsync();

            var viewModel = new SearchViewModel
            {
                Videos = videos,
                Channels = channels
            };

            ViewData["Query"] = query;
            return View(viewModel);
        }
    }
}