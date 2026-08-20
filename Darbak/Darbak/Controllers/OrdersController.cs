using System.Data;
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

        public OrdersController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // CHECKOUT GET
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCart();

            if (!cart.Any())
            {
                TempData["CartError"] =
                    "Your cart is empty.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var synchronizationResult =
                await SynchronizeCartAsync(cart);

            cart = synchronizationResult.Cart;

            if (!cart.Any())
            {
                TempData["CartError"] =
                    "There are no available products in your cart.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            if (synchronizationResult.Changed)
            {
                SaveCart(cart);

                TempData["CheckoutInfo"] =
                    "Your cart was updated to match the latest product information and stock.";
            }

            var viewModel =
                new CheckoutViewModel
                {
                    CartItems = cart
                };

            return View(viewModel);
        }

        // ==========================================
        // CHECKOUT POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(
            CheckoutViewModel viewModel)
        {
            var cart = GetCart();

            if (!cart.Any())
            {
                TempData["CartError"] =
                    "Your cart is empty.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            // Never trust CartItems posted by the browser.
            // Use the server-side Session cart instead.
            viewModel.CartItems = cart;

            if (!string.IsNullOrWhiteSpace(
                    viewModel.ShippingAddress))
            {
                viewModel.ShippingAddress =
                    viewModel.ShippingAddress.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    viewModel.City))
            {
                viewModel.City =
                    viewModel.City.Trim();
            }

            if (!string.IsNullOrWhiteSpace(
                    viewModel.PhoneNumber))
            {
                viewModel.PhoneNumber =
                    viewModel.PhoneNumber.Trim();
            }

            if (!ModelState.IsValid)
            {
                var sync =
                    await SynchronizeCartAsync(cart);

                viewModel.CartItems =
                    sync.Cart;

                if (sync.Changed)
                {
                    SaveCart(sync.Cart);
                }

                return View(viewModel);
            }

            // Prevent invalid quantities even if Session data
            // somehow becomes corrupted.
            if (cart.Any(x => x.Quantity <= 0))
            {
                TempData["CartError"] =
                    "Your cart contains an invalid quantity.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            try
            {
                var productIds =
                    cart
                        .Select(x => x.ProductId)
                        .Distinct()
                        .ToList();

                // Cart should never contain duplicate rows
                // for the same product.
                if (productIds.Count != cart.Count)
                {
                    await transaction.RollbackAsync();

                    TempData["CartError"] =
                        "Your cart contains duplicate products. Please review your cart.";

                    return RedirectToAction(
                        "Index",
                        "Cart");
                }

                var products =
                    await _context.Products
                        .Where(p =>
                            productIds.Contains(p.Id))
                        .ToDictionaryAsync(
                            p => p.Id);

                var checkoutHasErrors = false;

                // Refresh cart values using DB values.
                foreach (var cartItem in cart)
                {
                    if (!products.TryGetValue(
                            cartItem.ProductId,
                            out var product))
                    {
                        ModelState.AddModelError(
                            "",
                            $"{cartItem.ProductName} no longer exists.");

                        checkoutHasErrors = true;

                        continue;
                    }

                    cartItem.ProductName =
                        product.Name;

                    cartItem.Price =
                        product.Price;

                    if (!product.IsActive)
                    {
                        ModelState.AddModelError(
                            "",
                            $"{product.Name} is no longer available.");

                        checkoutHasErrors = true;

                        continue;
                    }

                    if (product.StockQuantity <= 0)
                    {
                        ModelState.AddModelError(
                            "",
                            $"{product.Name} is out of stock.");

                        checkoutHasErrors = true;

                        continue;
                    }

                    if (product.StockQuantity <
                        cartItem.Quantity)
                    {
                        ModelState.AddModelError(
                            "",
                            $"Only {product.StockQuantity} item(s) of {product.Name} are currently available.");

                        checkoutHasErrors = true;
                    }
                }

                if (checkoutHasErrors)
                {
                    await transaction.RollbackAsync();

                    SaveCart(cart);

                    viewModel.CartItems =
                        cart;

                    return View(viewModel);
                }

                var order =
                    new Order
                    {
                        UserId =
                            userId,

                        OrderDate =
                            DateTime.UtcNow,

                        Status =
                            OrderStatus.Processing,

                        PaymentStatus =
                            PaymentStatus.Pending,

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

                    var orderItem =
    new OrderItem
    {
        ProductId =
            product.Id,

        
        ProductName =
            product.Name,

        Quantity =
            cartItem.Quantity,

       
        UnitPrice =
            product.Price
    };

                    order.OrderItems.Add(
                        orderItem);

                    order.TotalAmount +=
                        orderItem.UnitPrice *
                        orderItem.Quantity;

                    product.StockQuantity -=
                        cartItem.Quantity;
                }

                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // Clear the cart only after
                // the transaction succeeds.
                HttpContext.Session.Remove(
                    CartSessionKey);

                TempData["OrderSuccess"] =
                    $"Order #{order.Id} was placed successfully.";

                return RedirectToAction(
                    nameof(Details),
                    new
                    {
                        id = order.Id
                    });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "The order could not be completed because the product data changed. Please review your cart and try again.");

                var sync =
                    await SynchronizeCartAsync(
                        GetCart());

                viewModel.CartItems =
                    sync.Cart;

                if (sync.Changed)
                {
                    SaveCart(sync.Cart);
                }

                return View(viewModel);
            }
        }

        // ==========================================
        // USER - MY ORDERS
        // ==========================================
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
                    .AsNoTracking()
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

        // ==========================================
        // USER - ORDER DETAILS
        // ==========================================
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
                    .AsNoTracking()
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

        // ==========================================
        // ADMIN - ALL ORDERS
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminIndex()
        {
            var orders =
                await _context.Orders
                    .AsNoTracking()
                    .Include(o =>
                        o.User)
                    .Include(o =>
                        o.OrderItems)
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            return View(orders);
        }

        // ==========================================
        // ADMIN - ORDER DETAILS
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminDetails(
            int id)
        {
            var order =
                await _context.Orders
                    .AsNoTracking()
                    .Include(o =>
                        o.User)
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

        // ==========================================
        // ADMIN - UPDATE ORDER STATUS
        // ==========================================
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

            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync(
                        IsolationLevel.Serializable);

            try
            {
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
                    await transaction.RollbackAsync();

                    return NotFound();
                }

                if (order.Status == status)
                {
                    await transaction.RollbackAsync();

                    return RedirectToAction(
                        nameof(AdminDetails),
                        new { id });
                }

                // Moving INTO Cancelled:
                // return stock.
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

                // Moving OUT OF Cancelled:
                // stock must be available again.
                if (order.Status ==
                        OrderStatus.Cancelled &&
                    status !=
                        OrderStatus.Cancelled)
                {
                    foreach (var item
                             in order.OrderItems)
                    {
                        if (!item.Product.IsActive)
                        {
                            await transaction
                                .RollbackAsync();

                            TempData["OrderError"] =
                                $"{item.Product.Name} is inactive and the order cannot be reactivated.";

                            return RedirectToAction(
                                nameof(AdminDetails),
                                new { id });
                        }

                        if (item.Product.StockQuantity <
                            item.Quantity)
                        {
                            await transaction
                                .RollbackAsync();

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

                await transaction.CommitAsync();

                TempData["AdminOrderSuccess"] =
                    "Order status updated successfully.";

                return RedirectToAction(
                    nameof(AdminDetails),
                    new { id });
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();

                TempData["OrderError"] =
                    "The order status could not be updated because the data changed. Please try again.";

                return RedirectToAction(
                    nameof(AdminDetails),
                    new { id });
            }
        }

        // ==========================================
        // ADMIN - UPDATE PAYMENT STATUS
        // ==========================================
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

            if (order.PaymentStatus ==
                paymentStatus)
            {
                return RedirectToAction(
                    nameof(AdminDetails),
                    new { id });
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

        // ==========================================
        // SYNCHRONIZE SESSION CART WITH DATABASE
        // ==========================================
        private async Task<CartSynchronizationResult>
            SynchronizeCartAsync(
                List<CartItemViewModel> cart)
        {
            if (!cart.Any())
            {
                return new CartSynchronizationResult
                {
                    Cart = cart,
                    Changed = false
                };
            }

            var productIds =
                cart
                    .Select(x => x.ProductId)
                    .Distinct()
                    .ToList();

            var products =
                await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Images)
                    .Where(p =>
                        productIds.Contains(p.Id))
                    .ToDictionaryAsync(
                        p => p.Id);

            var changed = false;

            foreach (var item
                     in cart.ToList())
            {
                if (!products.TryGetValue(
                        item.ProductId,
                        out var product))
                {
                    cart.Remove(item);

                    changed = true;

                    continue;
                }

                if (!product.IsActive ||
                    product.StockQuantity <= 0)
                {
                    cart.Remove(item);

                    changed = true;

                    continue;
                }

                var imageUrl =
                    product.Images
                        .OrderByDescending(
                            i => i.IsMain)
                        .ThenBy(i => i.Id)
                        .Select(i => i.ImageUrl)
                        .FirstOrDefault();

                if (item.ProductName !=
                    product.Name)
                {
                    item.ProductName =
                        product.Name;

                    changed = true;
                }

                if (item.Price !=
                    product.Price)
                {
                    item.Price =
                        product.Price;

                    changed = true;
                }

                if (item.ImageUrl !=
                    imageUrl)
                {
                    item.ImageUrl =
                        imageUrl;

                    changed = true;
                }

                if (item.Quantity >
                    product.StockQuantity)
                {
                    item.Quantity =
                        product.StockQuantity;

                    changed = true;
                }

                if (item.Quantity <= 0)
                {
                    cart.Remove(item);

                    changed = true;
                }
            }

            return new CartSynchronizationResult
            {
                Cart = cart,
                Changed = changed
            };
        }

        // ==========================================
        // GET CART
        // ==========================================
        private List<CartItemViewModel> GetCart()
        {
            var cartJson =
                HttpContext.Session
                    .GetString(CartSessionKey);

            if (string.IsNullOrWhiteSpace(
                    cartJson))
            {
                return new List<CartItemViewModel>();
            }

            try
            {
                return JsonSerializer
                    .Deserialize<
                        List<CartItemViewModel>>(
                        cartJson)
                    ?? new List<CartItemViewModel>();
            }
            catch (JsonException)
            {
                HttpContext.Session.Remove(
                    CartSessionKey);

                return new List<CartItemViewModel>();
            }
        }

        // ==========================================
        // SAVE CART
        // ==========================================
        private void SaveCart(
            List<CartItemViewModel> cart)
        {
            if (!cart.Any())
            {
                HttpContext.Session.Remove(
                    CartSessionKey);

                return;
            }

            HttpContext.Session.SetString(
                CartSessionKey,
                JsonSerializer.Serialize(cart));
        }

        private sealed class
            CartSynchronizationResult
        {
            public List<CartItemViewModel> Cart { get; set; }
                = new();

            public bool Changed { get; set; }
        }
    }
}