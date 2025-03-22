using Database_Video.IRepo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Web_Video.Controllers
{
    public class CoreController : Controller
    {
        private IUnitOfWork _unitOfWork;
        protected IUnitOfWork UnitOfWork => _unitOfWork ??= HttpContext.RequestServices.GetService<IUnitOfWork>();
    }
}
