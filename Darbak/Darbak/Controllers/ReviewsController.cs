using Darbak.Data;
using Darbak.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Darbak.Controllers
{
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == productId &&
                    p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.ProductName = product.Name;

            var review = new Review
            {
                ProductId = product.Id
            };

            return View(review);
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ProductId,Rating,Comment")]
            Review review)
        {
            var userId = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(p =>
                    p.Id == review.ProductId &&
                    p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            // These properties are assigned by the server,
            // not by the user.
            ModelState.Remove(nameof(Review.UserId));
            ModelState.Remove(nameof(Review.User));
            ModelState.Remove(nameof(Review.Product));
            ModelState.Remove(nameof(Review.Status));
            ModelState.Remove(nameof(Review.CreatedAt));

            if (!string.IsNullOrWhiteSpace(review.Comment))
            {
                review.Comment = review.Comment.Trim();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProductName = product.Name;

                return View(review);
            }

            review.UserId = userId;
            review.Status = ApprovalStatus.Pending;
            review.CreatedAt = DateTime.UtcNow;

            _context.Reviews.Add(review);

            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] =
                "Your review was submitted and is waiting for approval.";

            return RedirectToAction(
                "Details",
                "Products",
                new { id = review.ProductId });
        }

        // ==========================================
        // ADMIN INDEX + FILTERING
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Index(
            ApprovalStatus? status,
            string? product,
            string? user,
            int? rating)
        {
            var query = _context.Reviews
                .AsNoTracking()
                .Include(r => r.Product)
                .Include(r => r.User)
                .AsQueryable();

            // Filter by approval status
            if (status.HasValue)
            {
                query = query.Where(r =>
                    r.Status == status.Value);
            }

            // Filter by product name
            if (!string.IsNullOrWhiteSpace(product))
            {
                product = product.Trim();

                query = query.Where(r =>
                    r.Product.Name.Contains(product));
            }

            // Filter by user name or email
            if (!string.IsNullOrWhiteSpace(user))
            {
                user = user.Trim();

                query = query.Where(r =>
                    (r.User.FullName != null &&
                     r.User.FullName.Contains(user)) ||
                    (r.User.Email != null &&
                     r.User.Email.Contains(user)));
            }

            // Rating filter
            if (rating.HasValue &&
                rating.Value >= 1 &&
                rating.Value <= 5)
            {
                query = query.Where(r =>
                    r.Rating == rating.Value);
            }
            else if (rating.HasValue)
            {
                rating = null;
            }

            var reviews = await query
                .OrderByDescending(r =>
                    r.CreatedAt)
                .ToListAsync();

            ViewBag.Status =
                status?.ToString();

            ViewBag.ProductFilter =
                product;

            ViewBag.UserFilter =
                user;

            ViewBag.Rating =
                rating;

            return View(reviews);
        }

        // APPROVE
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var review = await _context.Reviews
                .FindAsync(id);

            if (review == null)
            {
                return NotFound();
            }

            review.Status = ApprovalStatus.Approved;

            await _context.SaveChangesAsync();

            TempData["ReviewAdminSuccess"] =
                "Review approved successfully.";

            return RedirectToAction(nameof(Index));
        }

        // REJECT
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var review = await _context.Reviews
                .FindAsync(id);

            if (review == null)
            {
                return NotFound();
            }

            review.Status = ApprovalStatus.Rejected;

            await _context.SaveChangesAsync();

            TempData["ReviewAdminSuccess"] =
                "Review rejected successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}