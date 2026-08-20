using System.Collections.Concurrent;
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

        private const string PendingPaymentSessionKey =
            "PendingMockPayment";

        private static readonly
            ConcurrentDictionary<string, SemaphoreSlim>
            PaymentLocks = new();

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
        // PREPARE MOCK PAYMENT
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

            viewModel.CartItems = cart;

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            if (cart.Any(x =>
                    x.Quantity <= 0))
            {
                TempData["CartError"] =
                    "Your cart contains an invalid quantity.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var distinctProductCount =
                cart
                    .Select(x => x.ProductId)
                    .Distinct()
                    .Count();

            if (distinctProductCount !=
                cart.Count)
            {
                TempData["CartError"] =
                    "Your cart contains duplicate products.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var synchronizationResult =
                await SynchronizeCartAsync(
                    cart);

            cart =
                synchronizationResult.Cart;

            if (!cart.Any())
            {
                TempData["CartError"] =
                    "There are no available products in your cart.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            /*
             * If price, stock, product name,
             * image or availability changed,
             * return the updated cart first.
             */
            if (synchronizationResult.Changed)
            {
                SaveCart(cart);

                viewModel.CartItems =
                    cart;

                ModelState.AddModelError(
                    "",
                    "Your cart was updated to match the latest prices or stock. Please review it before continuing.");

                return View(viewModel);
            }

            var payment =
                new MockPaymentViewModel
                {
                    PaymentToken =
                        Guid.NewGuid()
                            .ToString("N"),

                    ShippingAddress =
                        viewModel.ShippingAddress,

                    City =
                        viewModel.City,

                    PhoneNumber =
                        viewModel.PhoneNumber,

                    CartItems =
                        cart
                };

            SavePendingPayment(
                payment);

            return RedirectToAction(
                nameof(Payment));
        }

        // ==========================================
        // MOCK PAYMENT GET
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Payment()
        {
            var payment =
                GetPendingPayment();

            if (payment == null)
            {
                TempData["CartError"] =
                    "No pending payment was found.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var cart =
                GetCart();

            if (!cart.Any())
            {
                HttpContext.Session.Remove(
                    PendingPaymentSessionKey);

                TempData["CartError"] =
                    "Your cart is empty.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            var synchronizationResult =
                await SynchronizeCartAsync(
                    cart);

            cart =
                synchronizationResult.Cart;

            if (!cart.Any())
            {
                HttpContext.Session.Remove(
                    PendingPaymentSessionKey);

                TempData["CartError"] =
                    "There are no available products in your cart.";

                return RedirectToAction(
                    "Index",
                    "Cart");
            }

            if (synchronizationResult.Changed)
            {
                SaveCart(cart);

                payment.CartItems =
                    cart;

                SavePendingPayment(
                    payment);

                TempData["PaymentInfo"] =
                    "Your cart was updated to match the latest prices or stock. Please review the new total.";
            }

            payment.CartItems =
                cart;

            return View(payment);
        }

        // ==========================================
        // COMPLETE MOCK PAYMENT
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletePayment(
            string paymentToken)
        {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Challenge();
            }

            /*
             * Prevent simultaneous payment requests
             * for the same user from running through
             * the order creation code together.
             */
            var paymentLock =
                PaymentLocks.GetOrAdd(
                    userId,
                    _ => new SemaphoreSlim(1, 1));

            await paymentLock.WaitAsync();

            try
            {
                var payment =
                    GetPendingPayment();

                var cart =
                    GetCart();

                if (payment == null ||
                    !cart.Any())
                {
                    TempData["CartError"] =
                        "This payment has already been completed or is no longer available.";

                    return RedirectToAction(
                        "Index",
                        "Cart");
                }

                if (string.IsNullOrWhiteSpace(
                        paymentToken) ||
                    !string.Equals(
                        payment.PaymentToken,
                        paymentToken,
                        StringComparison.Ordinal))
                {
                    TempData["PaymentError"] =
                        "The payment request is invalid or expired.";

                    return RedirectToAction(
                        nameof(Payment));
                }

                if (cart.Any(x =>
                        x.Quantity <= 0))
                {
                    TempData["CartError"] =
                        "Your cart contains an invalid quantity.";

                    return RedirectToAction(
                        "Index",
                        "Cart");
                }

                var productIds =
                    cart
                        .Select(x => x.ProductId)
                        .Distinct()
                        .ToList();

                if (productIds.Count !=
                    cart.Count)
                {
                    TempData["CartError"] =
                        "Your cart contains duplicate products.";

                    return RedirectToAction(
                        "Index",
                        "Cart");
                }

                /*
                 * Final synchronization before
                 * starting the transaction.
                 */
                var synchronizationResult =
                    await SynchronizeCartAsync(
                        cart);

                cart =
                    synchronizationResult.Cart;

                if (!cart.Any())
                {
                    SaveCart(cart);

                    HttpContext.Session.Remove(
                        PendingPaymentSessionKey);

                    TempData["CartError"] =
                        "The products in your cart are no longer available.";

                    return RedirectToAction(
                        "Index",
                        "Cart");
                }

                if (synchronizationResult.Changed)
                {
                    SaveCart(cart);

                    payment.CartItems =
                        cart;

                    SavePendingPayment(
                        payment);

                    TempData["PaymentError"] =
                        "Your cart changed before payment. Please review the updated order before completing payment.";

                    return RedirectToAction(
                        nameof(Payment));
                }

                await using var transaction =
                    await _context.Database
                        .BeginTransactionAsync(
                            IsolationLevel.Serializable);

                try
                {
                    var products =
                        await _context.Products
                            .Where(p =>
                                productIds.Contains(
                                    p.Id))
                            .ToDictionaryAsync(
                                p => p.Id);

                    var requiresReview =
                        false;

                    foreach (var cartItem
                             in cart)
                    {
                        if (!products.TryGetValue(
                                cartItem.ProductId,
                                out var product))
                        {
                            requiresReview =
                                true;

                            continue;
                        }

                        if (!product.IsActive ||
                            product.StockQuantity <= 0)
                        {
                            requiresReview =
                                true;

                            continue;
                        }

                        if (product.StockQuantity <
                            cartItem.Quantity)
                        {
                            cartItem.Quantity =
                                product.StockQuantity;

                            requiresReview =
                                true;
                        }

                        if (cartItem.Price !=
                            product.Price)
                        {
                            cartItem.Price =
                                product.Price;

                            requiresReview =
                                true;
                        }

                        if (cartItem.ProductName !=
                            product.Name)
                        {
                            cartItem.ProductName =
                                product.Name;

                            requiresReview =
                                true;
                        }
                    }

                    /*
                     * Something changed between
                     * Payment page and final transaction.
                     */
                    if (requiresReview)
                    {
                        await transaction
                            .RollbackAsync();

                        _context.ChangeTracker.Clear();

                        var refreshedCart =
                            await SynchronizeCartAsync(
                                cart);

                        cart =
                            refreshedCart.Cart;

                        SaveCart(cart);

                        payment.CartItems =
                            cart;

                        SavePendingPayment(
                            payment);

                        TempData["PaymentError"] =
                            "Product information changed before payment. Please review the updated order and try again.";

                        return RedirectToAction(
                            nameof(Payment));
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
                                PaymentStatus.Paid,

                            ShippingAddress =
                                payment.ShippingAddress,

                            City =
                                payment.City,

                            PhoneNumber =
                                payment.PhoneNumber,

                            TotalAmount =
                                0
                        };

                    foreach (var cartItem
                             in cart)
                    {
                        var product =
                            products[
                                cartItem.ProductId];

                        var orderItem =
                            new OrderItem
                            {
                                ProductId =
                                    product.Id,

                                /*
                                 * Snapshot preserves
                                 * the product name used
                                 * when the order was placed.
                                 */
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

                    _context.Orders.Add(
                        order);

                    await _context
                        .SaveChangesAsync();

                    await transaction
                        .CommitAsync();

                    /*
                     * Clear Session only after the
                     * database transaction succeeds.
                     */
                    HttpContext.Session.Remove(
                        CartSessionKey);

                    HttpContext.Session.Remove(
                        PendingPaymentSessionKey);

                    TempData["OrderSuccess"] =
                        $"Payment completed successfully. Order #{order.Id} was placed successfully.";

                    return RedirectToAction(
                        nameof(Details),
                        new
                        {
                            id = order.Id
                        });
                }
                catch (DbUpdateException)
                {
                    await transaction
                        .RollbackAsync();

                    _context.ChangeTracker.Clear();

                    var refreshedCart =
                        await SynchronizeCartAsync(
                            GetCart());

                    SaveCart(
                        refreshedCart.Cart);

                    payment.CartItems =
                        refreshedCart.Cart;

                    SavePendingPayment(
                        payment);

                    TempData["PaymentError"] =
                        "The payment could not be completed because product information changed. Please review the order and try again.";

                    return RedirectToAction(
                        nameof(Payment));
                }
            }
            finally
            {
                paymentLock.Release();
            }
        }

        // ==========================================
        // CANCEL MOCK PAYMENT
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelPayment()
        {
            HttpContext.Session.Remove(
                PendingPaymentSessionKey);

            TempData["CheckoutInfo"] =
                "Payment was cancelled. Your cart was not changed.";

            return RedirectToAction(
                nameof(Checkout));
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
        // ORDER INVOICE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Invoice(
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
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o =>
                        o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            /*
             * Normal users can access only
             * their own invoices.
             * Admin can access every invoice.
             */
            var isAdmin =
                User.IsInRole("Admin");

            if (!isAdmin &&
                order.UserId != userId)
            {
                return NotFound();
            }

            return View(order);
        }

        // ==========================================
        // ADMIN - ALL ORDERS + FILTERING
        // ==========================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> AdminIndex(
            int? orderId,
            string? user,
            OrderStatus? status,
            PaymentStatus? paymentStatus,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query =
                _context.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .AsQueryable();

            // Order ID
            if (orderId.HasValue)
            {
                query = query.Where(o =>
                    o.Id == orderId.Value);
            }

            // User name / email
            if (!string.IsNullOrWhiteSpace(
                    user))
            {
                user =
                    user.Trim();

                query = query.Where(o =>
                    (o.User.FullName != null &&
                     o.User.FullName.Contains(user)) ||
                    (o.User.Email != null &&
                     o.User.Email.Contains(user)));
            }

            // Order Status
            if (status.HasValue)
            {
                query = query.Where(o =>
                    o.Status ==
                    status.Value);
            }

            // Payment Status
            if (paymentStatus.HasValue)
            {
                query = query.Where(o =>
                    o.PaymentStatus ==
                    paymentStatus.Value);
            }

            // From Date
            if (fromDate.HasValue)
            {
                var startDate =
                    fromDate.Value.Date;

                query = query.Where(o =>
                    o.OrderDate >=
                    startDate);
            }

            // To Date - inclusive
            if (toDate.HasValue)
            {
                var endDate =
                    toDate.Value
                        .Date
                        .AddDays(1);

                query = query.Where(o =>
                    o.OrderDate <
                    endDate);
            }

            var orders =
                await query
                    .OrderByDescending(o =>
                        o.OrderDate)
                    .ToListAsync();

            ViewBag.OrderId =
                orderId;

            ViewBag.UserFilter =
                user;

            ViewBag.Status =
                status?.ToString();

            ViewBag.PaymentStatus =
                paymentStatus?.ToString();

            ViewBag.FromDate =
                fromDate?.ToString(
                    "yyyy-MM-dd");

            ViewBag.ToDate =
                toDate?.ToString(
                    "yyyy-MM-dd");

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
                    await transaction
                        .RollbackAsync();

                    return NotFound();
                }

                if (order.Status ==
                    status)
                {
                    await transaction
                        .RollbackAsync();

                    return RedirectToAction(
                        nameof(AdminDetails),
                        new
                        {
                            id
                        });
                }

                /*
                 * Moving INTO Cancelled:
                 * return purchased stock.
                 */
                if (order.Status !=
                        OrderStatus.Cancelled &&
                    status ==
                        OrderStatus.Cancelled)
                {
                    foreach (var item
                             in order.OrderItems)
                    {
                        item.Product
                            .StockQuantity +=
                            item.Quantity;
                    }
                }

                /*
                 * Moving OUT OF Cancelled:
                 * stock must still be available.
                 */
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
                                new
                                {
                                    id
                                });
                        }

                        if (item.Product
                                .StockQuantity <
                            item.Quantity)
                        {
                            await transaction
                                .RollbackAsync();

                            TempData["OrderError"] =
                                $"Not enough stock for {item.Product.Name}.";

                            return RedirectToAction(
                                nameof(AdminDetails),
                                new
                                {
                                    id
                                });
                        }
                    }

                    foreach (var item
                             in order.OrderItems)
                    {
                        item.Product
                            .StockQuantity -=
                            item.Quantity;
                    }
                }

                order.Status =
                    status;

                await _context
                    .SaveChangesAsync();

                await transaction
                    .CommitAsync();

                TempData["AdminOrderSuccess"] =
                    "Order status updated successfully.";

                return RedirectToAction(
                    nameof(AdminDetails),
                    new
                    {
                        id
                    });
            }
            catch (DbUpdateException)
            {
                await transaction
                    .RollbackAsync();

                TempData["OrderError"] =
                    "The order status could not be updated because the data changed. Please try again.";

                return RedirectToAction(
                    nameof(AdminDetails),
                    new
                    {
                        id
                    });
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
                    new
                    {
                        id
                    });
            }

            order.PaymentStatus =
                paymentStatus;

            await _context
                .SaveChangesAsync();

            TempData["AdminOrderSuccess"] =
                "Payment status updated successfully.";

            return RedirectToAction(
                nameof(AdminDetails),
                new
                {
                    id
                });
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
                    Cart =
                        cart,

                    Changed =
                        false
                };
            }

            var productIds =
                cart
                    .Select(x =>
                        x.ProductId)
                    .Distinct()
                    .ToList();

            var products =
                await _context.Products
                    .AsNoTracking()
                    .Include(p =>
                        p.Images)
                    .Where(p =>
                        productIds.Contains(
                            p.Id))
                    .ToDictionaryAsync(
                        p => p.Id);

            var changed =
                false;

            foreach (var item
                     in cart.ToList())
            {
                if (!products.TryGetValue(
                        item.ProductId,
                        out var product))
                {
                    cart.Remove(
                        item);

                    changed =
                        true;

                    continue;
                }

                if (!product.IsActive ||
                    product.StockQuantity <= 0)
                {
                    cart.Remove(
                        item);

                    changed =
                        true;

                    continue;
                }

                var imageUrl =
                    product.Images
                        .OrderByDescending(
                            i => i.IsMain)
                        .ThenBy(i =>
                            i.Id)
                        .Select(i =>
                            i.ImageUrl)
                        .FirstOrDefault();

                if (item.ProductName !=
                    product.Name)
                {
                    item.ProductName =
                        product.Name;

                    changed =
                        true;
                }

                if (item.Price !=
                    product.Price)
                {
                    item.Price =
                        product.Price;

                    changed =
                        true;
                }

                if (item.ImageUrl !=
                    imageUrl)
                {
                    item.ImageUrl =
                        imageUrl;

                    changed =
                        true;
                }

                if (item.Quantity >
                    product.StockQuantity)
                {
                    item.Quantity =
                        product.StockQuantity;

                    changed =
                        true;
                }

                if (item.Quantity <= 0)
                {
                    cart.Remove(
                        item);

                    changed =
                        true;
                }
            }

            return new CartSynchronizationResult
            {
                Cart =
                    cart,

                Changed =
                    changed
            };
        }

        // ==========================================
        // GET CART
        // ==========================================
        private List<CartItemViewModel> GetCart()
        {
            var cartJson =
                HttpContext.Session
                    .GetString(
                        CartSessionKey);

            if (string.IsNullOrWhiteSpace(
                    cartJson))
            {
                return new List<
                    CartItemViewModel>();
            }

            try
            {
                return JsonSerializer
                    .Deserialize<
                        List<CartItemViewModel>>(
                            cartJson)
                    ?? new List<
                        CartItemViewModel>();
            }
            catch (JsonException)
            {
                HttpContext.Session.Remove(
                    CartSessionKey);

                return new List<
                    CartItemViewModel>();
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
                JsonSerializer.Serialize(
                    cart));
        }

        // ==========================================
        // GET PENDING MOCK PAYMENT
        // ==========================================
        private MockPaymentViewModel?
            GetPendingPayment()
        {
            var paymentJson =
                HttpContext.Session
                    .GetString(
                        PendingPaymentSessionKey);

            if (string.IsNullOrWhiteSpace(
                    paymentJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer
                    .Deserialize<
                        MockPaymentViewModel>(
                            paymentJson);
            }
            catch (JsonException)
            {
                HttpContext.Session.Remove(
                    PendingPaymentSessionKey);

                return null;
            }
        }

        // ==========================================
        // SAVE PENDING MOCK PAYMENT
        // ==========================================
        private void SavePendingPayment(
            MockPaymentViewModel payment)
        {
            HttpContext.Session.SetString(
                PendingPaymentSessionKey,
                JsonSerializer.Serialize(
                    payment));
        }

        // ==========================================
        // CART SYNCHRONIZATION RESULT
        // ==========================================
        private sealed class
            CartSynchronizationResult
        {
            public List<CartItemViewModel>
                Cart
            { get; set; } =
                    new();

            public bool Changed { get; set; }
        }
    }
}