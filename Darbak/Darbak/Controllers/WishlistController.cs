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

            var productExists =
                await _context.Products
                    .AnyAsync(p =>
                        p.Id == productId &&
                        p.IsActive);

            if (!productExists)
            {
                return NotFound();
            }

            var alreadyExists =
                await _context.WishlistItems
                    .AnyAsync(w =>
                        w.UserId == userId &&
                        w.ProductId == productId);

            if (!alreadyExists)
            {
                var wishlistItem =
                    new WishlistItem
                    {
                        UserId = userId,
                        ProductId = productId
                    };

                _context.WishlistItems.Add(
                    wishlistItem);

                await _context.SaveChangesAsync();

                TempData["WishlistSuccess"] =
                    "Product added to wishlist.";
            }
            else
            {
                TempData["WishlistInfo"] =
                    "Product is already in your wishlist.";
            }

            return RedirectToAction(
                "Details",
                "Products",
                new { id = productId });
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
                return NotFound();
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