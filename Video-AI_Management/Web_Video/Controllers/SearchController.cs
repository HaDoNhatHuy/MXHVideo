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
    public class SearchController : Controller
    {
        private readonly DataContext _context;
        public SearchController(DataContext context)
        {
            _context = context;
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<string>());

            var results = await _context.Videos
                .Where(v => v.Title.ToLower().Contains(q.ToLower()))
                .Select(v => v.Title)
                .Distinct()
                .Take(10)
                .ToListAsync();

            return Ok(results);
        }
    }
}
