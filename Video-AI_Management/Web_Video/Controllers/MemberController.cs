using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web_Video.Controllers
{
    [Authorize]
    public class MemberController : CoreController
    {
        public IActionResult Channel()
        {
            return View();
        }
    }
}
