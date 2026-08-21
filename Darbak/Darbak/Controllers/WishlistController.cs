using Darbak.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId =
                User.FindFirstValue(
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(
            int productId)
        {
            var userId =
                User.FindFirstValue(
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
                    nameof(Index));
            }

            var alreadyExists =
                await _context.WishlistItems
                    .AsNoTracking()
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
                    new
                    {
                        id = productId
                    });
            }

            var wishlistItem =
                new WishlistItem
                {
                    UserId =
                        userId,

                    ProductId =
                        productId,

                    CreatedAt =
                        DateTime.UtcNow
                };

            _context.WishlistItems.Add(
                wishlistItem);

            try
            {
                await _context
                    .SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var existsNow =
                    await _context.WishlistItems
                        .AsNoTracking()
                        .AnyAsync(w =>
                            w.UserId == userId &&
                            w.ProductId == productId);

                if (existsNow)
                {
                    TempData["WishlistInfo"] =
                        "This product is already in your wishlist.";
                }
                else
                {
                    TempData["WishlistError"] =
                        "The product could not be added to your wishlist.";
                }

                return RedirectToAction(
                    "Details",
                    "Products",
                    new
                    {
                        id = productId
                    });
            }

            TempData["WishlistSuccess"] =
                "Product added to wishlist successfully.";

            return RedirectToAction(
                "Details",
                "Products",
                new
                {
                    id = productId
                });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(
            int id)
        {
            var userId =
                User.FindFirstValue(
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

            try
            {
                await _context
                    .SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["WishlistError"] =
                    "The product could not be removed from your wishlist.";

                return RedirectToAction(
                    nameof(Index));
            }

            TempData["WishlistSuccess"] =
                "Product removed from wishlist.";

            return RedirectToAction(
                nameof(Index));
        }
    }
}