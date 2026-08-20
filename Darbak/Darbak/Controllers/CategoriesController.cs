using Darbak.Data;
using Darbak.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX + ADMIN FILTERING
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search)
        {
            var query =
                _context.Categories
                    .AsNoTracking()
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(c =>
                    c.Name.Contains(search));
            }

            var categories =
                await query
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            ViewBag.Search =
                search;

            return View(categories);
        }

        // ==========================================
        // CREATE GET
        // ==========================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // ==========================================
        // CREATE POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,Description")]
            Category category)
        {
            category.Name =
                category.Name?.Trim()
                ?? string.Empty;

            category.Description =
                string.IsNullOrWhiteSpace(
                    category.Description)
                    ? null
                    : category.Description.Trim();

            if (string.IsNullOrWhiteSpace(
                    category.Name))
            {
                ModelState.AddModelError(
                    nameof(Category.Name),
                    "Category name is required.");
            }

            if (!string.IsNullOrWhiteSpace(
                    category.Name))
            {
                var nameExists =
                    await _context.Categories
                        .AsNoTracking()
                        .AnyAsync(c =>
                            c.Name == category.Name);

                if (nameExists)
                {
                    ModelState.AddModelError(
                        nameof(Category.Name),
                        "A category with this name already exists.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            try
            {
                _context.Categories.Add(
                    category);

                await _context
                    .SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    nameof(Category.Name),
                    "The category could not be saved. A category with this name may already exist.");

                return View(category);
            }

            TempData["CategorySuccess"] =
                "Category created successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // ==========================================
        // EDIT GET
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Edit(
            int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category =
                await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.Id == id.Value);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // ==========================================
        // EDIT POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,Description")]
            Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            category.Name =
                category.Name?.Trim()
                ?? string.Empty;

            category.Description =
                string.IsNullOrWhiteSpace(
                    category.Description)
                    ? null
                    : category.Description.Trim();

            if (string.IsNullOrWhiteSpace(
                    category.Name))
            {
                ModelState.AddModelError(
                    nameof(Category.Name),
                    "Category name is required.");
            }

            if (!string.IsNullOrWhiteSpace(
                    category.Name))
            {
                var nameExists =
                    await _context.Categories
                        .AsNoTracking()
                        .AnyAsync(c =>
                            c.Id != category.Id &&
                            c.Name == category.Name);

                if (nameExists)
                {
                    ModelState.AddModelError(
                        nameof(Category.Name),
                        "A category with this name already exists.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            var existingCategory =
                await _context.Categories
                    .FirstOrDefaultAsync(c =>
                        c.Id == id);

            if (existingCategory == null)
            {
                return NotFound();
            }

            existingCategory.Name =
                category.Name;

            existingCategory.Description =
                category.Description;

            try
            {
                await _context
                    .SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(
                    nameof(Category.Name),
                    "The category could not be updated. A category with this name may already exist.");

                return View(category);
            }

            TempData["CategorySuccess"] =
                "Category updated successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // ==========================================
        // DELETE GET
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Delete(
            int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var category =
                await _context.Categories
                    .AsNoTracking()
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c =>
                        c.Id == id.Value);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // ==========================================
        // DELETE POST
        // ==========================================
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConfirmed(int id)
        {
            var category =
                await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c =>
                        c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            if (category.Products.Any())
            {
                TempData["CategoryError"] =
                    "This category cannot be deleted because it contains products.";

                return RedirectToAction(
                    nameof(Index));
            }

            try
            {
                _context.Categories.Remove(
                    category);

                await _context
                    .SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["CategoryError"] =
                    "This category could not be deleted because it is being used by other data.";

                return RedirectToAction(
                    nameof(Index));
            }

            TempData["CategorySuccess"] =
                "Category deleted successfully.";

            return RedirectToAction(
                nameof(Index));
        }
    }
}