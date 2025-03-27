using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Database_Video.DTOs;
using Database_Video.Pagination;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Web_Video.ViewModels;
using Web_Video.ViewModels.Channel;
using Web_Video.ViewModels.Home;

namespace Web_Video.Controllers
{
    public class HomeController : CoreController
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index(string page)
        {
            var toReturn = new HomeViewModel();
            if (User.Identity.IsAuthenticated)
            {
                toReturn.Page = page;
                if (page == null || page == "Home")
                {
                    var allCategories = await UnitOfWork.CategoryRepo.GetAllAsync();
                    var categoryList = allCategories.Select(category => new SelectListItem
                    {
                        Text = category.CategoryName,
                        Value = category.Id.ToString()
                    }).ToList();

                    categoryList.Insert(0, new SelectListItem
                    {
                        Text = "All",
                        Value = "0",
                        Selected = true
                    });
                    toReturn.CategoryDropdown = categoryList;
                }
            }
            return View(toReturn);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #region API Endpoints
        [HttpGet]
        public async Task<IActionResult> GetVideosForHomeGrid(HomeParameters parameters)
        {
            var items = await UnitOfWork.VideoRepo.GetVideosForHomeGridAsync(parameters);
            var paginatedResults = new PaginatedResult<VideoForHomeGridDto>(items, items.TotalItemsCount, items.PageNumber, items.PageSize, items.TotalPages);
            
            return Json(new ApiResponse(200, result: paginatedResults));
        }
        #endregion
    }
}
