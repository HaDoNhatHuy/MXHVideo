using Database_Video.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Video.Services;
using Web_Video.ViewModels.Admin;
using Web_Video.ViewModels.Channel;
using WebVideo.Utility;

namespace Web_Video.Controllers
{
    [Authorize(Roles = $"{SD.AdminRole}")]
    public class AdminController : CoreController
    {
        private UserManager<AppUser> _userManager;
        private RoleManager<AppRole> _roleManager;
        public AdminController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }
        public IActionResult Category()
        {
            return View();
        }
        public async Task<IActionResult> AllUsers()
        {
            var toReturn = new List<UserDisplayGridViewModel>();
            var users = await _userManager.Users
                .Include(x => x.Channel)
                .Where(x => x.UserName != "admin")
                .ToListAsync();
            foreach (var user in users)
            {
                var userDisplayToAdd = new UserDisplayGridViewModel();
                Mapper.Map(user, userDisplayToAdd);
                userDisplayToAdd.IsLocked = _userManager.IsLockedOutAsync(user).GetAwaiter().GetResult();
                userDisplayToAdd.AssignedRoles = _userManager.GetRolesAsync(user).GetAwaiter().GetResult();
                toReturn.Add(userDisplayToAdd);
            }
            return View(toReturn);
        }
        public async Task<IActionResult> AddEditUser(Guid id)
        {
            var toReturn = new UserAddEditViewModel();
            toReturn.ApplicationRoles = await GetApplicationRolesAsync();

            if (id != Guid.Empty)
            {
                // edit
                var user = await _userManager.FindByIdAsync(id.ToString());
                Mapper.Map(user, toReturn);

                var userRoles = _userManager.GetRolesAsync(user).GetAwaiter().GetResult();
                toReturn.UserRoles = userRoles.ToList();
            }

            return View(toReturn);
        }
        [HttpPost]
        public async Task<IActionResult> AddEditUser(UserAddEditViewModel model)
        {
            if (ModelState.IsValid)
            {
                bool proceed = true;

                if (model.Id == Guid.Empty)
                {
                    // Creating a user
                    if (string.IsNullOrEmpty(model.Password))
                    {
                        proceed = false;
                        ModelState.AddModelError("Password", "Password is required");
                    }

                    if (proceed && model.UserRoles.Count == 0)
                    {
                        proceed = false;
                        ModelState.AddModelError("UserRoles", "Please select at least one role");
                    }

                    if (proceed && CheckNameExistsAsync(model.FullName).GetAwaiter().GetResult())
                    {
                        proceed = false;
                        ModelState.AddModelError("Name", $"The name of '{model.FullName} is taken. Please try another name.");
                    }

                    if (proceed && CheckEmailExistsAsync(model.Email).GetAwaiter().GetResult())
                    {
                        proceed = false;
                        ModelState.AddModelError("Email", $"Email address of {model.Email} is taken. Please try using another email address.");
                    }

                    if (proceed)
                    {
                        var userToAdd = new AppUser
                        {
                            FullName = model.FullName,
                            UserName = model.FullName.ToLower(),
                            Email = model.Email,
                        };

                        var result = await _userManager.CreateAsync(userToAdd, model.Password);
                        await _userManager.AddToRolesAsync(userToAdd, model.UserRoles);

                        if (result.Succeeded)
                        {
                            return RedirectToAction("AllUsers");
                        }
                        else
                        {
                            foreach (var error in result.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                        }
                    }
                }
                else
                {
                    // Editing an user
                    var user = await _userManager.FindByIdAsync(model.Id.ToString());

                    if (user == null)
                    {
                        TempData["notification"] = "false;Not Found;The requested user was not found";
                        return RedirectToAction("AllUsers");
                    }

                    if (IsSuperAdminUserId(model.Id))
                    {
                        TempData["notification"] = "false;Bad Request;Super Admin change is not allowed!";
                        return RedirectToAction("AllUsers");
                    }

                    if (model.UserRoles.Count == 0)
                    {
                        proceed = false;
                        ModelState.AddModelError("UserRoles", "Please select at least one role");
                    }

                    if (proceed && !user.FullName.Equals(model.FullName))
                    {
                        if (CheckNameExistsAsync(model.FullName).GetAwaiter().GetResult())
                        {
                            proceed = false;
                            ModelState.AddModelError("Name", $"The name of '{model.FullName} is taken. Please try another name.");
                        }
                    }

                    if (proceed && !user.Email.Equals(model.Email))
                    {
                        if (CheckEmailExistsAsync(model.Email).GetAwaiter().GetResult())
                        {
                            proceed = false;
                            ModelState.AddModelError("Email", $"Email address of {model.Email} is taken. Please try using another email address.");
                        }
                    }

                    if (proceed && !string.IsNullOrEmpty(model.Password))
                    {
                        // Changing the user's password
                        await _userManager.RemovePasswordAsync(user);
                        var result = await _userManager.AddPasswordAsync(user, model.Password);

                        if (!result.Succeeded)
                        {
                            proceed = false;
                            foreach (var error in result.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                        }
                    }

                    if (proceed)
                    {
                        user.FullName = model.FullName;
                        user.UserName = model.FullName.ToLower();
                        user.Email = model.Email;

                        var userRoles = await _userManager.GetRolesAsync(user);
                        // remove user's existing roles
                        await _userManager.RemoveFromRolesAsync(user, userRoles);

                        // adding the new roles
                        foreach (var role in model.UserRoles)
                        {
                            var roleToAdd = await _roleManager.Roles.FirstOrDefaultAsync(r => r.Name == role);
                            if (roleToAdd != null)
                            {
                                await _userManager.AddToRoleAsync(user, role);
                            }
                        }

                        return RedirectToAction("AllUsers");
                    }
                }
            }

            model.ApplicationRoles = await GetApplicationRolesAsync();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> LockUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                TempData["notification"] = "false;Not Found;The requested user was not found";
                return RedirectToAction("AllUsers");
            }

            if (IsSuperAdminUserId(id))
            {
                TempData["notification"] = "false;Bad Request;Super Admin change is not allowed!";
                return RedirectToAction("AllUsers");
            }

            // Lock the user for 5 days
            user.LockoutEnabled = true;
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTime.Now.AddDays(5));

            if (!result.Succeeded)
            {
                TempData["notification"] = "false;Server Error;Server Error";
            }

            return RedirectToAction("AllUsers");
        }
        [HttpPost]
        public async Task<IActionResult> UnlockUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
            {
                TempData["notification"] = "false;Not Found;The requested user was not found";
                return RedirectToAction("AllUsers");
            }

            if (IsSuperAdminUserId(id))
            {
                TempData["notification"] = "false;Bad Request;Super Admin change is not allowed!";
                return RedirectToAction("AllUsers");
            }


            var result = await _userManager.SetLockoutEndDateAsync(user, null);

            if (!result.Succeeded)
            {
                TempData["notification"] = "false;Server Error;Server Error";
            }

            return RedirectToAction("AllUsers");
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
                return Json(new ApiResponse(404, message: "The requested category was not found"));

            var categoryVideoIdsAndThumbnails = await Context.Videos
                .Where(x => x.CategoryId == id)
                .Select(x => new
                {
                    x.Id,
                    x.Thumbnail
                }).ToListAsync();
            if (categoryVideoIdsAndThumbnails.Any())
            {
                foreach(var video in categoryVideoIdsAndThumbnails)
                {
                    PhotoService.DeletePhotoLocally(video.Thumbnail);
                    await UnitOfWork.VideoRepo.RemoveVideoAsync(video.Id);
                    await UnitOfWork.CompleteAsync();
                }
            }
            UnitOfWork.CategoryRepo.Remove(category);
            await UnitOfWork.CompleteAsync();
            return Json(new ApiResponse(200, "Deleted", $"Category of {category.CategoryName} has been deleted"));
        }
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.Users
                .Include(x => x.Channel)
                .Where(x => x.Id == id)
                .FirstOrDefaultAsync();

            if (user != null)
            {
                if (IsSuperAdminUserId(Guid.Parse(id)))
                {
                    return Json(new ApiResponse(400, message: "Super admin cannot be deleted"));
                }

                if (user.Channel != null)
                {
                    var userChannelWithVideos = await Context.Channels
                        .Where(x => x.AppUserId == id)
                        .Select(x => new
                        {
                            Videos = x.Videos.Select(x => new
                            {
                                x.Id,
                                x.Thumbnail
                            })
                        }).FirstOrDefaultAsync();

                    foreach (var video in userChannelWithVideos.Videos)
                    {
                        PhotoService.DeletePhotoLocally(video.Thumbnail);
                        await UnitOfWork.VideoRepo.RemoveVideoAsync(video.Id);
                        await UnitOfWork.CompleteAsync();
                    }
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    TempData["notification"] = $"true;Deleted;User of {user.FullName} has been permanently removed";
                    return Json(new ApiResponse(200));
                }
                else
                {
                    return Json(new ApiResponse(400, message: result.Errors.Select(x => x.Description).FirstOrDefault()));
                }
            }

            return Json(new ApiResponse(404, message: "The requested user was not found"));
        }
        #endregion
        #region Private Methods
        public async Task<List<string>> GetApplicationRolesAsync()
        {
            return await _roleManager.Roles
                .Select(x => x.Name)
                .ToListAsync();
        }
        private async Task<bool> CheckEmailExistsAsync(string email)
        {
            return await _userManager.Users.AnyAsync(x => x.Email == email.ToLower());
        }

        private async Task<bool> CheckNameExistsAsync(string name)
        {
            return await _userManager.Users.AnyAsync(x => x.FullName.ToLower() == name.ToLower());
        }
        private bool IsSuperAdminUserId(Guid userId)
        {
            return _userManager.FindByIdAsync(userId.ToString()).GetAwaiter().GetResult().UserName.Equals("admin");
        }
        #endregion
    }
}
