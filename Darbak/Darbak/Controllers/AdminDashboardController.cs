using Darbak.Data;
using Darbak.Models.Enums;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminDashboardController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var totalProducts =
                await _context.Products
                    .AsNoTracking()
                    .CountAsync();

            var activeProducts =
                await _context.Products
                    .AsNoTracking()
                    .CountAsync(p => p.IsActive);

            var totalCategories =
                await _context.Categories
                    .AsNoTracking()
                    .CountAsync();

            var totalUsers =
                await _context.Users
                    .AsNoTracking()
                    .CountAsync();

            var totalOrders =
                await _context.Orders
                    .AsNoTracking()
                    .CountAsync();

            var totalOrderValue =
                await _context.Orders
                    .AsNoTracking()
                    .SumAsync(o =>
                        (decimal?)o.TotalAmount)
                ?? 0;

            var pendingReviews =
                await _context.Reviews
                    .AsNoTracking()
                    .CountAsync(r =>
                        r.Status ==
                        ApprovalStatus.Pending);

            var pendingTestimonials =
                await _context.Testimonials
                    .AsNoTracking()
                    .CountAsync(t =>
                        t.Status ==
                        ApprovalStatus.Pending);

            var recentOrders =
                await _context.Orders
                    .AsNoTracking()
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .Take(5)
                    .Select(o =>
                        new DashboardOrderViewModel
                        {
                            Id = o.Id,

                            UserName =
                                o.User.FullName
                                ?? o.User.UserName
                                ?? "User",

                            OrderDate =
                                o.OrderDate,

                            TotalAmount =
                                o.TotalAmount,

                            Status =
                                o.Status,

                            PaymentStatus =
                                o.PaymentStatus
                        })
                    .ToListAsync();

            var lowStockProducts =
                await _context.Products
                    .AsNoTracking()
                    .Where(p =>
                        p.IsActive &&
                        p.StockQuantity <= 5)
                    .OrderBy(p =>
                        p.StockQuantity)
                    .ThenBy(p =>
                        p.Name)
                    .Take(5)
                    .Select(p =>
                        new LowStockProductViewModel
                        {
                            Id = p.Id,

                            Name = p.Name,

                            StockQuantity =
                                p.StockQuantity,

                            Price =
                                p.Price
                        })
                    .ToListAsync();

            var statusCounts =
                await _context.Orders
                    .AsNoTracking()
                    .GroupBy(o =>
                        o.Status)
                    .Select(g =>
                        new
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                    .ToListAsync();

            var ordersByStatus =
                Enum.GetValues<OrderStatus>()
                    .ToDictionary(
                        status => status,
                        status => 0);

            foreach (var item in statusCounts)
            {
                ordersByStatus[item.Status] =
                    item.Count;
            }

            var paymentCounts =
                await _context.Orders
                    .AsNoTracking()
                    .GroupBy(o =>
                        o.PaymentStatus)
                    .Select(g =>
                        new
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                    .ToListAsync();

            var ordersByPaymentStatus =
                Enum.GetValues<PaymentStatus>()
                    .ToDictionary(
                        status => status,
                        status => 0);

            foreach (var item in paymentCounts)
            {
                ordersByPaymentStatus[item.Status] =
                    item.Count;
            }

            var viewModel =
                new AdminDashboardViewModel
                {
                    TotalProducts =
                        totalProducts,

                    ActiveProducts =
                        activeProducts,

                    TotalCategories =
                        totalCategories,

                    TotalUsers =
                        totalUsers,

                    TotalOrders =
                        totalOrders,

                    TotalOrderValue =
                        totalOrderValue,

                    PendingReviews =
                        pendingReviews,

                    PendingTestimonials =
                        pendingTestimonials,

                    RecentOrders =
                        recentOrders,

                    LowStockProducts =
                        lowStockProducts,

                    OrdersByStatus =
                        ordersByStatus,

                    OrdersByPaymentStatus =
                        ordersByPaymentStatus
                };

            return View(viewModel);
        }
    }
}