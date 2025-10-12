using DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Web_Video.Controllers
{
    [ApiController]
    [Route("api/search")]
    public class SearchApiController : ControllerBase
    {
        private readonly DataContext _context;

        public SearchApiController(DataContext context)
        {
            _context = context;
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<string>());

            // Lấy tiêu đề video
            var videoTitles = await _context.Videos
                .Where(v => v.Title != null && v.Title.ToLower().Contains(q.ToLower()))
                .Select(v => v.Title)
                .Distinct()
                .Take(10)
                .ToListAsync();

            // Lấy tên kênh
            var channelNames = await _context.Channels
                .Where(c => c.ChannelName != null && c.ChannelName.ToLower().Contains(q.ToLower()))
                .Select(c => c.ChannelName)
                .Distinct()
                .Take(10)
                .ToListAsync();

            // Kết hợp và giới hạn 10 kết quả
            var results = videoTitles
                .Union(channelNames)
                .Distinct()
                .Take(10)
                .ToList();

            return Ok(results);
        }
    }
}