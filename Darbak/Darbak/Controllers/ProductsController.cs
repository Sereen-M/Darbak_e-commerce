using Darbak.Data;
using Darbak.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Darbak.Models.Enums;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX + ADMIN FILTERING
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            int? categoryId,
            bool? isActive,
            string? stockStatus)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .AsQueryable();

            // Product name - partial search
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(p =>
                    p.Name.Contains(search));
            }

            // Category
            if (categoryId.HasValue)
            {
                query = query.Where(p =>
                    p.CategoryId == categoryId.Value);
            }

            // Active / Inactive
            if (isActive.HasValue)
            {
                query = query.Where(p =>
                    p.IsActive == isActive.Value);
            }

            // Stock status
            if (!string.IsNullOrWhiteSpace(stockStatus))
            {
                stockStatus =
                    stockStatus.Trim().ToLowerInvariant();

                switch (stockStatus)
                {
                    case "in_stock":
                        query = query.Where(p =>
                            p.StockQuantity > 0);
                        break;

                    case "out_of_stock":
                        query = query.Where(p =>
                            p.StockQuantity == 0);
                        break;

                    default:
                        stockStatus = null;
                        break;
                }
            }

            var categories =
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            var products =
                await query
                    .OrderByDescending(p =>
                        p.CreatedAt)
                    .ToListAsync();

            ViewBag.Search =
                search;

            ViewBag.CategoryId =
                categoryId;

            ViewBag.IsActive =
                isActive?.ToString()
                    .ToLowerInvariant();

            ViewBag.StockStatus =
                stockStatus;

            ViewBag.Categories =
                categories;

            return View(products);
        }

        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategories();

            return View();
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind(
                "Name,Description,Price,StockQuantity,IsActive,CategoryId"
            )]
            Product product)
        {
            ModelState.Remove(nameof(Product.Category));

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == product.CategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(Product.CategoryId),
                    "The selected category does not exist."
                );
            }

            if (!ModelState.IsValid)
            {
                await LoadCategories(product.CategoryId);

                return View(product);
            }

            product.Name = product.Name.Trim();

            product.Description =
                string.IsNullOrWhiteSpace(product.Description)
                    ? null
                    : product.Description.Trim();

            product.CreatedAt = DateTime.UtcNow;

            _context.Products.Add(product);

            await _context.SaveChangesAsync();

            TempData["ProductSuccess"] =
                "Product created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // EDIT GET
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product =
                await _context.Products.FindAsync(id);

            if (product == null)
            {
                return NotFound();
            }

            await LoadCategories(product.CategoryId);

            return View(product);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind(
                "Id,Name,Description,Price,StockQuantity,IsActive,CategoryId"
            )]
            Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            ModelState.Remove(nameof(Product.Category));

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == product.CategoryId);

            if (!categoryExists)
            {
                ModelState.AddModelError(
                    nameof(Product.CategoryId),
                    "The selected category does not exist."
                );
            }

            if (!ModelState.IsValid)
            {
                await LoadCategories(product.CategoryId);

                return View(product);
            }

            var existingProduct =
                await _context.Products.FindAsync(id);

            if (existingProduct == null)
            {
                return NotFound();
            }

            existingProduct.Name =
                product.Name.Trim();

            existingProduct.Description =
                string.IsNullOrWhiteSpace(product.Description)
                    ? null
                    : product.Description.Trim();

            existingProduct.Price =
                product.Price;

            existingProduct.StockQuantity =
                product.StockQuantity;

            existingProduct.IsActive =
                product.IsActive;

            existingProduct.CategoryId =
                product.CategoryId;

            await _context.SaveChangesAsync();

            TempData["ProductSuccess"] =
                "Product updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // DELETE GET
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.OrderItems)
                .FirstOrDefaultAsync(
                    p => p.Id == id
                );

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var product = await _context.Products
                .Include(p => p.OrderItems)
                .FirstOrDefaultAsync(
                    p => p.Id == id
                );

            if (product == null)
            {
                return NotFound();
            }

            if (product.OrderItems.Any())
            {
                TempData["ProductError"] =
                    "This product cannot be deleted because it exists in previous orders. You can deactivate it instead.";

                return RedirectToAction(nameof(Index));
            }

            _context.Products.Remove(product);

            await _context.SaveChangesAsync();

            TempData["ProductSuccess"] =
                "Product deleted successfully.";

            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Images)
                .Include(p => p.Reviews
                    .Where(r =>
                        r.Status == ApprovalStatus.Approved))
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // Normal users and guests must not
            // access inactive products directly.
            if (!product.IsActive &&
                !User.IsInRole("Admin"))
            {
                return NotFound();
            }

            return View(product);
        }

        private async Task LoadCategories(
            int? selectedCategoryId = null)
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.CategoryId = new SelectList(
                categories,
                "Id",
                "Name",
                selectedCategoryId
            );
        }
    }
}