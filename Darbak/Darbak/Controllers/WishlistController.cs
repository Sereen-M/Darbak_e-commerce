using System.Security.Claims;
using Darbak.Data;
using Darbak.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // INDEX
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var wishlistItems =
                await _context.WishlistItems
                    .AsNoTracking()
                    .Where(w =>
                        w.UserId == userId)
                    .Include(w =>
                        w.Product)
                    .ThenInclude(p =>
                        p.Images)
                    .OrderByDescending(w =>
                        w.CreatedAt)
                    .ToListAsync();

            return View(wishlistItems);
        }

        // =========================
        // ADD
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int productId)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var product =
                await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p =>
                        p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            if (!product.IsActive)
            {
                TempData["WishlistError"] =
                    "This product is no longer available.";

                return RedirectToAction(
                    "Index",
                    "Wishlist");
            }

            var alreadyExists =
                await _context.WishlistItems
                    .AnyAsync(w =>
                        w.UserId == userId &&
                        w.ProductId == productId);

            if (alreadyExists)
            {
                TempData["WishlistInfo"] =
                    "This product is already in your wishlist.";

                return RedirectToAction(
                    "Details",
                    "Products",
                    new { id = productId });
            }

            var wishlistItem =
                new WishlistItem
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAt = DateTime.UtcNow
                };

            _context.WishlistItems.Add(
                wishlistItem);

            await _context.SaveChangesAsync();

            TempData["WishlistSuccess"] =
                "Product added to wishlist successfully.";

            return RedirectToAction(
                "Details",
                "Products",
                new { id = productId });
        }

        // =========================
        // REMOVE
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(
            int id)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var wishlistItem =
                await _context.WishlistItems
                    .FirstOrDefaultAsync(w =>
                        w.Id == id &&
                        w.UserId == userId);

            if (wishlistItem == null)
            {
                TempData["WishlistError"] =
                    "Wishlist item could not be found.";

                return RedirectToAction(
                    nameof(Index));
            }

            _context.WishlistItems.Remove(
                wishlistItem);

            await _context.SaveChangesAsync();

            TempData["WishlistSuccess"] =
                "Product removed from wishlist.";

            return RedirectToAction(
                nameof(Index));
        }
    }
}