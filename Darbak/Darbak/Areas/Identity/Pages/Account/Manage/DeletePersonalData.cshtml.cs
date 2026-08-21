using Darbak.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Darbak.Areas.Identity.Pages.Account.Manage;

public class DeletePersonalDataModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeletePersonalDataModel> _logger;

    public DeletePersonalDataModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        ILogger<DeletePersonalDataModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = default!;
    }

    public bool RequirePassword { get; set; }

    public async Task<IActionResult> OnGet()
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound(
                $"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        RequirePassword =
            await _userManager.HasPasswordAsync(user);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound(
                $"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        RequirePassword =
            await _userManager.HasPasswordAsync(user);

        if (RequirePassword)
        {
            if (string.IsNullOrWhiteSpace(Input.Password) ||
                !await _userManager.CheckPasswordAsync(
                    user,
                    Input.Password))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Incorrect password.");

                return Page();
            }
        }

        var hasOrders =
            await _context.Orders
                .AsNoTracking()
                .AnyAsync(o =>
                    o.UserId == user.Id);

        if (hasOrders)
        {
            ModelState.AddModelError(
                string.Empty,
                "Your account cannot be deleted because it has order history that must be preserved.");

            return Page();
        }

        var userId = user.Id;

        try
        {
            var result =
                await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error
                         in result.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                return Page();
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(
                ex,
                "User with ID '{UserId}' could not be deleted because related data prevents deletion.",
                userId);

            ModelState.AddModelError(
                string.Empty,
                "Your account cannot be deleted because it contains data that must be preserved.");

            return Page();
        }

        await _signInManager.SignOutAsync();

        _logger.LogInformation(
            "User with ID '{UserId}' deleted their account.",
            userId);

        return Redirect("~/");
    }
}