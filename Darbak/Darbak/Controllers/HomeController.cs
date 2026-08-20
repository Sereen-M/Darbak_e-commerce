using System.Diagnostics;
using Darbak.Data;
using Darbak.Models;
using Darbak.Models.Enums;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var latestProducts =
                await _context.Products
                    .AsNoTracking()
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(6)
                    .Select(p =>
                        new HomeProductViewModel
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
                                    .Select(i =>
                                        i.ImageUrl)
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
                                    ApprovalStatus.Approved)
                        })
                    .ToListAsync();

            var categories =
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c =>
                        new HomeCategoryViewModel
                        {
                            Id = c.Id,

                            Name = c.Name,

                            Description =
                                c.Description,

                            ProductCount =
                                c.Products.Count(p =>
                                    p.IsActive)
                        })
                    .ToListAsync();

            var testimonials =
                await _context.Testimonials
                    .AsNoTracking()
                    .Where(t =>
                        t.Status ==
                        ApprovalStatus.Approved)
                    .OrderByDescending(t =>
                        t.CreatedAt)
                    .Take(3)
                    .Select(t =>
                        new HomeTestimonialViewModel
                        {
                            Id = t.Id,

                            Content =
                                t.Content,

                            UserName =
                                t.User.FullName
                                ?? t.User.UserName
                                ?? "User",

                            CreatedAt =
                                t.CreatedAt
                        })
                    .ToListAsync();

            var viewModel =
                new HomeViewModel
                {
                    LatestProducts =
                        latestProducts,

                    Categories =
                        categories,

                    Testimonials =
                        testimonials
                };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id
                        ?? HttpContext.TraceIdentifier
                });
        }
    }
}