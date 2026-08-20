using Darbak.Data;
using Darbak.Models.Enums;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    public class CatalogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CatalogController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            int? categoryId,
            string? sort)
        {
            var query = _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive)
                .AsQueryable();

            // Search by:
            // - Product name
            // - Category name
            // Supports partial text
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(p =>
                    p.Name.Contains(search) ||
                    p.Category.Name.Contains(search));
            }

            // Category filter
            if (categoryId.HasValue)
            {
                query = query.Where(p =>
                    p.CategoryId == categoryId.Value);
            }

            // Sorting
            query = sort switch
            {
                "price_asc" =>
                    query.OrderBy(p => p.Price),

                "price_desc" =>
                    query.OrderByDescending(p => p.Price),

                _ =>
                    query.OrderByDescending(p => p.CreatedAt)
            };

            var products = await query
                .Select(p =>
                    new CatalogProductViewModel
                    {
                        Id = p.Id,

                        Name = p.Name,

                        Price = p.Price,

                        StockQuantity =
                            p.StockQuantity,

                        CategoryName =
                            p.Category.Name,

                        MainImageUrl =
                            p.Images
                                .OrderByDescending(i =>
                                    i.IsMain)
                                .ThenBy(i => i.Id)
                                .Select(i => i.ImageUrl)
                                .FirstOrDefault(),

                        AverageRating =
                            p.Reviews
                                .Where(r =>
                                    r.Status ==
                                    ApprovalStatus.Approved)
                                .Select(r =>
                                    (double?)r.Rating)
                                .Average() ?? 0,

                        ReviewCount =
                            p.Reviews.Count(r =>
                                r.Status ==
                                ApprovalStatus.Approved),

                        CreatedAt =
                            p.CreatedAt
                    })
                .ToListAsync();

            var categories =
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

            var viewModel =
                new CatalogViewModel
                {
                    Products = products,

                    Categories = categories,

                    Search = search,

                    CategoryId = categoryId,

                    Sort = sort
                };

            return View(viewModel);
        }
    }
}