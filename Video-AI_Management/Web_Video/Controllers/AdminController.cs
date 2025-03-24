using Database_Video.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.ViewModels.Admin;
using Web_Video.ViewModels.Channel;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.AdminRole}")]
    public class AdminController : CoreController
    {
        public IActionResult Category()
        {
            return View();
        }

        #region API Endpoints
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await UnitOfWork.CategoryRepo.GetAllAsync();
            var toReturn = categories.Select(x => new CategoryAddEditViewModel
            {
                Id = x.Id,
                Name = x.CategoryName,
            });
            return Json(new ApiResponse(200, result: toReturn));

        }

        [HttpPost]
        public async Task<IActionResult> AddEditCategory(CategoryAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id == Guid.Empty)
                {
                    UnitOfWork.CategoryRepo.Add(new Category() { CategoryName = model.Name, CreatedDate = DateTime.Now });
                    await UnitOfWork.CompleteAsync();
                    return Json(new ApiResponse(201, "Created", "New Category was added"));
                }
                else
                {
                    var category = await UnitOfWork.CategoryRepo.GetByIdAsync(model.Id);
                    if (category == null)
                        return Json(new ApiResponse(404));
                    var oldName = category.CategoryName;
                    category.CategoryName = model.Name;
                    await UnitOfWork.CompleteAsync();
                    return Json(new ApiResponse(200, "Edited", $"Category of {oldName} has been renamed  to {model.Name}"));
                }
            }
            return Json(new ApiResponse(400, message: "Category name is required"));
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var category = await UnitOfWork.CategoryRepo.GetByIdAsync(id);
            if (category == null)
                return Json(new ApiResponse(404,message: "The requested category was not found"));
            UnitOfWork.CategoryRepo.Remove(category);
            await UnitOfWork.CompleteAsync();
            return Json(new ApiResponse(200, "Deleted", $"Category of {category.CategoryName} has been deleted"));
        }
        #endregion
    }
}
