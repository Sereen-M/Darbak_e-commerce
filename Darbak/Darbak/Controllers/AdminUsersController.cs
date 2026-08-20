using System.Security.Claims;
using Darbak.Data;
using Darbak.Models;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser>
            _userManager;

        private readonly ApplicationDbContext
            _context;

        public AdminUsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _userManager =
                userManager;

            _context =
                context;
        }

        // ==========================================
        // USERS LIST
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users =
                await _userManager.Users
                    .AsNoTracking()
                    .OrderBy(u => u.FullName)
                    .ThenBy(u => u.Email)
                    .ToListAsync();

            var userItems =
                new List<AdminUserListItemViewModel>();

            foreach (var user in users)
            {
                var roles =
                    await _userManager
                        .GetRolesAsync(user);

                userItems.Add(
                    new AdminUserListItemViewModel
                    {
                        Id =
                            user.Id,

                        FullName =
                            user.FullName,

                        Email =
                            user.Email
                            ?? "No email",

                        Roles =
                            roles.ToList()
                    });
            }

            var viewModel =
                new AdminUsersViewModel
                {
                    Users =
                        userItems
                };

            return View(viewModel);
        }

        // ==========================================
        // USER DETAILS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Details(
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            var user =
                await _userManager
                    .FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var roles =
                await _userManager
                    .GetRolesAsync(user);

            var selectedRole =
                roles.Contains("Admin")
                    ? "Admin"
                    : "User";

            var viewModel =
                new AdminUserDetailsViewModel
                {
                    Id =
                        user.Id,

                    FullName =
                        user.FullName,

                    Email =
                        user.Email
                        ?? "No email",

                    UserName =
                        user.UserName,

                    Roles =
                        roles.ToList(),

                    SelectedRole =
                        selectedRole
                };

            return View(viewModel);
        }

        // ==========================================
        // CHANGE ROLE
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(
            string userId,
            string selectedRole)
        {
            if (string.IsNullOrWhiteSpace(
                    userId))
            {
                return BadRequest();
            }

            if (selectedRole != "Admin" &&
                selectedRole != "User")
            {
                TempData["UserError"] =
                    "Invalid role selected.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = userId
                    });
            }

            var user =
                await _userManager
                    .FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var currentUserId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            var currentRoles =
                await _userManager
                    .GetRolesAsync(user);

            // Never allow the current Admin
            // to remove Admin from themselves.
            if (user.Id == currentUserId &&
                currentRoles.Contains("Admin") &&
                selectedRole != "Admin")
            {
                TempData["UserError"] =
                    "You cannot remove the Admin role from your own account.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = userId
                    });
            }

            // Exactly the requested role already exists.
            if (currentRoles.Count == 1 &&
                currentRoles.Contains(
                    selectedRole))
            {
                TempData["UserInfo"] =
                    "The user already has this role.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = userId
                    });
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                // Add the requested role first.
                if (!currentRoles.Contains(
                        selectedRole))
                {
                    var addResult =
                        await _userManager
                            .AddToRoleAsync(
                                user,
                                selectedRole);

                    if (!addResult.Succeeded)
                    {
                        await transaction
                            .RollbackAsync();

                        TempData["UserError"] =
                            string.Join(
                                " ",
                                addResult.Errors
                                    .Select(e =>
                                        e.Description));

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id = userId
                            });
                    }
                }

                // Only Admin/User are managed by this
                // controller. Remove all roles except
                // the selected one.
                var rolesToRemove =
                    currentRoles
                        .Where(role =>
                            role != selectedRole)
                        .ToList();

                if (rolesToRemove.Any())
                {
                    var removeResult =
                        await _userManager
                            .RemoveFromRolesAsync(
                                user,
                                rolesToRemove);

                    if (!removeResult.Succeeded)
                    {
                        await transaction
                            .RollbackAsync();

                        TempData["UserError"] =
                            string.Join(
                                " ",
                                removeResult.Errors
                                    .Select(e =>
                                        e.Description));

                        return RedirectToAction(
                            nameof(Details),
                            new
                            {
                                id = userId
                            });
                    }
                }

                await transaction
                    .CommitAsync();
            }
            catch
            {
                await transaction
                    .RollbackAsync();

                TempData["UserError"] =
                    "The user role could not be changed. No changes were saved.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = userId
                    });
            }

            TempData["UserSuccess"] =
                $"User role changed to {selectedRole} successfully.";

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id = userId
                });
        }
    }
}