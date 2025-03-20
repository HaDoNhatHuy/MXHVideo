using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles =$"{SD.UserRole}")]
    public class ChannelController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
