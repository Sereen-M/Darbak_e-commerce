using Darbak.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // INDEX
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        // CREATE GET
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (!string.IsNullOrWhiteSpace(category.Name))
            {
                var nameExists = await _context.Categories
                    .AnyAsync(c => c.Name == category.Name);

                if (nameExists)
                {
                    ModelState.AddModelError(
                        nameof(Category.Name),
                        "A category with this name already exists."
                    );
                }
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            category.Name = category.Name.Trim();

            if (!string.IsNullOrWhiteSpace(category.Description))
            {
                category.Description = category.Description.Trim();
            }

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            TempData["CategorySuccess"] =
                "Category created successfully.";

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

            var category =
                await _context.Categories.FindAsync(id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // EDIT POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Category category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(category.Name))
            {
                var nameExists =
                    await _context.Categories
                        .AnyAsync(c =>
                            c.Name == category.Name &&
                            c.Id != category.Id);

                if (nameExists)
                {
                    ModelState.AddModelError(
                        nameof(Category.Name),
                        "A category with this name already exists."
                    );
                }
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            var existingCategory =
                await _context.Categories.FindAsync(id);

            if (existingCategory == null)
            {
                return NotFound();
            }

            existingCategory.Name =
                category.Name.Trim();

            existingCategory.Description =
                string.IsNullOrWhiteSpace(category.Description)
                    ? null
                    : category.Description.Trim();

            await _context.SaveChangesAsync();

            TempData["CategorySuccess"] =
                "Category updated successfully.";

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

            var category =
                await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c =>
                        c.Id == id);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
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

                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["CategorySuccess"] =
                "Category deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}