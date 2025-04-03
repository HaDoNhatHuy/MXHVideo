using AutoMapper;
using DataAccess.Data;
using Database_Video.IRepo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Web_Video.Controllers
{
    public class CoreController : Controller
    {
        private IUnitOfWork _unitOfWork;
        private DataContext _context;
        private IConfiguration _configuration;
        private IMapper _mapper;
        protected IUnitOfWork UnitOfWork => _unitOfWork ??= HttpContext.RequestServices.GetService<IUnitOfWork>();
        protected DataContext Context => _context ??= HttpContext.RequestServices.GetService<DataContext>();
        protected IConfiguration Configuration => _configuration ??= HttpContext.RequestServices.GetService<IConfiguration>();
        protected IMapper Mapper => _mapper ??= HttpContext.RequestServices.GetService<IMapper>();
    }
}
