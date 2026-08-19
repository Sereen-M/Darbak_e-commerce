using System.Security.Claims;
using System.Text.Json;
using Darbak.Data;
using Darbak.Models;
using Darbak.Models.Enums;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const string CartSessionKey = "Cart";

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // CHECKOUT GET
        // =========================
        [HttpGet]
        public IActionResult Checkout()
        {
            var cart = GetCart();

            if (cart.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            var viewModel = new CheckoutViewModel
            {
                CartItems = cart
            };

            return View(viewModel);
        }

        // =========================
        // CHECKOUT POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(
            CheckoutViewModel viewModel)
        {
            var cart = GetCart();

            if (cart.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            viewModel.CartItems = cart;

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var productIds = cart
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var products =
                await _context.Products
                    .Where(p =>
                        productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

            // Validate products and stock
            foreach (var cartItem in cart)
            {
                if (!products.TryGetValue(
                        cartItem.ProductId,
                        out var product))
                {
                    ModelState.AddModelError(
                        "",
                        $"{cartItem.ProductName} no longer exists."
                    );

                    return View(viewModel);
                }

                if (!product.IsActive)
                {
                    ModelState.AddModelError(
                        "",
                        $"{product.Name} is no longer available."
                    );

                    return View(viewModel);
                }

                if (product.StockQuantity <
                    cartItem.Quantity)
                {
                    ModelState.AddModelError(
                        "",
                        $"Not enough stock for {product.Name}."
                    );

                    return View(viewModel);
                }
            }

            var order = new Order
            {
                UserId = userId,
                ShippingAddress =
                    viewModel.ShippingAddress,

                City =
                    viewModel.City,

                PhoneNumber =
                    viewModel.PhoneNumber,

                TotalAmount = 0
            };

            foreach (var cartItem in cart)
            {
                var product =
                    products[cartItem.ProductId];

                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = cartItem.Quantity,
                    UnitPrice = product.Price
                };

                order.OrderItems.Add(orderItem);

                order.TotalAmount +=
                    product.Price *
                    cartItem.Quantity;

                product.StockQuantity -=
                    cartItem.Quantity;
            }

            _context.Orders.Add(order);

            await _context.SaveChangesAsync();

            // Clear cart only after successful order
            HttpContext.Session.Remove(
                CartSessionKey);

            TempData["OrderSuccess"] =
                $"Order #{order.Id} was placed successfully.";

            return RedirectToAction(
                nameof(Details),
                new { id = order.Id });
        }

        // =========================
        // USER - MY ORDERS
        // =========================
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

            var orders =
                await _context.Orders
                    .Where(o =>
                        o.UserId == userId)
                    .Include(o =>
                        o.OrderItems)
                    .ThenInclude(oi =>
                        oi.Product)
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            return View(orders);
        }

        // =========================
        // USER - ORDER DETAILS
        // =========================
        [HttpGet]
        public async Task<IActionResult> Details(
            int id)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            var order =
                await _context.Orders
                    .Include(o =>
                        o.OrderItems)
                    .ThenInclude(oi =>
                        oi.Product)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id &&
                        o.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // =========================
        // ADMIN - ALL ORDERS
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminIndex()
        {
            var orders =
                await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            return View(orders);
        }

        // =========================
        // ADMIN - ORDER DETAILS
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminDetails(
            int id)
        {
            var order =
                await _context.Orders
                    .Include(o => o.User)
                    .Include(o =>
                        o.OrderItems)
                    .ThenInclude(oi =>
                        oi.Product)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            return View(order);
        }

        // =========================
        // ADMIN - UPDATE ORDER STATUS
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(
            int id,
            OrderStatus status)
        {
            if (!Enum.IsDefined(
                    typeof(OrderStatus),
                    status))
            {
                return BadRequest();
            }

            var order =
                await _context.Orders
                    .Include(o =>
                        o.OrderItems)
                    .ThenInclude(oi =>
                        oi.Product)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // No change needed
            if (order.Status == status)
            {
                return RedirectToAction(
                    nameof(AdminDetails),
                    new { id });
            }

            // If order becomes cancelled,
            // return quantities to stock
            if (order.Status !=
                    OrderStatus.Cancelled &&
                status ==
                    OrderStatus.Cancelled)
            {
                foreach (var item
                         in order.OrderItems)
                {
                    item.Product.StockQuantity +=
                        item.Quantity;
                }
            }

            // If cancelled order is activated again,
            // stock must still be available
            if (order.Status ==
                    OrderStatus.Cancelled &&
                status !=
                    OrderStatus.Cancelled)
            {
                foreach (var item
                         in order.OrderItems)
                {
                    if (item.Product.StockQuantity <
                        item.Quantity)
                    {
                        TempData["OrderError"] =
                            $"Not enough stock for {item.Product.Name}.";

                        return RedirectToAction(
                            nameof(AdminDetails),
                            new { id });
                    }
                }

                foreach (var item
                         in order.OrderItems)
                {
                    item.Product.StockQuantity -=
                        item.Quantity;
                }
            }

            order.Status = status;

            await _context.SaveChangesAsync();

            TempData["AdminOrderSuccess"] =
                "Order status updated successfully.";

            return RedirectToAction(
                nameof(AdminDetails),
                new { id });
        }

        // =========================
        // ADMIN - UPDATE PAYMENT STATUS
        // =========================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            UpdatePaymentStatus(
                int id,
                PaymentStatus paymentStatus)
        {
            if (!Enum.IsDefined(
                    typeof(PaymentStatus),
                    paymentStatus))
            {
                return BadRequest();
            }

            var order =
                await _context.Orders
                    .FirstOrDefaultAsync(o =>
                        o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            order.PaymentStatus =
                paymentStatus;

            await _context.SaveChangesAsync();

            TempData["AdminOrderSuccess"] =
                "Payment status updated successfully.";

            return RedirectToAction(
                nameof(AdminDetails),
                new { id });
        }

        // =========================
        // GET CART FROM SESSION
        // =========================
        private List<CartItemViewModel> GetCart()
        {
            var cartJson =
                HttpContext.Session
                    .GetString(CartSessionKey);

            if (string.IsNullOrEmpty(
                    cartJson))
            {
                return new List<CartItemViewModel>();
            }

            return JsonSerializer
                .Deserialize<List<CartItemViewModel>>(
                    cartJson)
                ?? new List<CartItemViewModel>();
        }
    }
}