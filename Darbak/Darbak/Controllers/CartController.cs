using Darbak.Data;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Darbak.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        private const string CartSessionKey = "Cart";

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // CART INDEX
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await GetSynchronizedCartAsync();

            return View(cart);
        }

        // =========================
        // ADD TO CART
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(
            int productId)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            if (!product.IsActive)
            {
                TempData["CartError"] =
                    "This product is no longer available.";

                return RedirectToAction(nameof(Index));
            }

            if (product.StockQuantity <= 0)
            {
                TempData["CartError"] =
                    "This product is currently out of stock.";

                return RedirectToAction(nameof(Index));
            }

            var cart = GetCart();

            var existingItem = cart
                .FirstOrDefault(x =>
                    x.ProductId == productId);

            var imageUrl = product.Images
                .OrderByDescending(i => i.IsMain)
                .ThenBy(i => i.Id)
                .Select(i => i.ImageUrl)
                .FirstOrDefault();

            if (existingItem != null)
            {
                // Always refresh data from DB
                existingItem.ProductName =
                    product.Name;

                existingItem.Price =
                    product.Price;

                existingItem.ImageUrl =
                    imageUrl;

                if (existingItem.Quantity >=
                    product.StockQuantity)
                {
                    existingItem.Quantity =
                        product.StockQuantity;

                    SaveCart(cart);

                    TempData["CartError"] =
                        "You cannot add more than the available stock.";

                    return RedirectToAction(
                        nameof(Index));
                }

                existingItem.Quantity++;
            }
            else
            {
                cart.Add(
                    new CartItemViewModel
                    {
                        ProductId =
                            product.Id,

                        ProductName =
                            product.Name,

                        Price =
                            product.Price,

                        Quantity = 1,

                        ImageUrl =
                            imageUrl
                    }
                );
            }

            SaveCart(cart);

            TempData["CartSuccess"] =
                "Product added to cart successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // UPDATE QUANTITY
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(
            int productId,
            int quantity)
        {
            var cart = GetCart();

            var item = cart
                .FirstOrDefault(x =>
                    x.ProductId == productId);

            if (item == null)
            {
                TempData["CartError"] =
                    "The cart item could not be found.";

                return RedirectToAction(nameof(Index));
            }

            if (quantity <= 0)
            {
                cart.Remove(item);

                SaveCart(cart);

                TempData["CartSuccess"] =
                    "Product removed from cart.";

                return RedirectToAction(nameof(Index));
            }

            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == productId);

            if (product == null ||
                !product.IsActive)
            {
                cart.Remove(item);

                SaveCart(cart);

                TempData["CartError"] =
                    "This product is no longer available and was removed from your cart.";

                return RedirectToAction(nameof(Index));
            }

            if (product.StockQuantity <= 0)
            {
                cart.Remove(item);

                SaveCart(cart);

                TempData["CartError"] =
                    "This product is out of stock and was removed from your cart.";

                return RedirectToAction(nameof(Index));
            }

            // Refresh product data
            item.ProductName =
                product.Name;

            item.Price =
                product.Price;

            item.ImageUrl =
                product.Images
                    .OrderByDescending(i => i.IsMain)
                    .ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault();

            if (quantity >
                product.StockQuantity)
            {
                item.Quantity =
                    product.StockQuantity;

                TempData["CartError"] =
                    $"Only {product.StockQuantity} item(s) are currently available.";
            }
            else
            {
                item.Quantity =
                    quantity;

                TempData["CartSuccess"] =
                    "Cart updated successfully.";
            }

            SaveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // REMOVE ITEM
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId)
        {
            var cart = GetCart();

            var item = cart
                .FirstOrDefault(x =>
                    x.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);

                SaveCart(cart);

                TempData["CartSuccess"] =
                    "Product removed from cart.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // CLEAR CART
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(
                CartSessionKey);

            TempData["CartSuccess"] =
                "Cart cleared successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // SYNCHRONIZE CART WITH DB
        // =========================
        private async Task<List<CartItemViewModel>>
            GetSynchronizedCartAsync()
        {
            var cart = GetCart();

            if (!cart.Any())
            {
                return cart;
            }

            var productIds = cart
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var products =
                await _context.Products
                    .Include(p => p.Images)
                    .Where(p =>
                        productIds.Contains(p.Id))
                    .ToListAsync();

            var productsDictionary =
                products.ToDictionary(
                    p => p.Id);

            var cartChanged = false;
            var removedItems = false;
            var adjustedItems = false;

            foreach (var item in cart.ToList())
            {
                if (!productsDictionary
                    .TryGetValue(
                        item.ProductId,
                        out var product))
                {
                    cart.Remove(item);

                    cartChanged = true;
                    removedItems = true;

                    continue;
                }

                if (!product.IsActive ||
                    product.StockQuantity <= 0)
                {
                    cart.Remove(item);

                    cartChanged = true;
                    removedItems = true;

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

                    cartChanged = true;
                }

                if (item.Price !=
                    product.Price)
                {
                    item.Price =
                        product.Price;

                    cartChanged = true;
                }

                if (item.ImageUrl !=
                    imageUrl)
                {
                    item.ImageUrl =
                        imageUrl;

                    cartChanged = true;
                }

                if (item.Quantity >
                    product.StockQuantity)
                {
                    item.Quantity =
                        product.StockQuantity;

                    cartChanged = true;
                    adjustedItems = true;
                }

                if (item.Quantity <= 0)
                {
                    cart.Remove(item);

                    cartChanged = true;
                    removedItems = true;
                }
            }

            if (cartChanged)
            {
                SaveCart(cart);
            }

            if (removedItems)
            {
                TempData["CartError"] =
                    "Some unavailable products were removed from your cart.";
            }
            else if (adjustedItems)
            {
                TempData["CartError"] =
                    "Some quantities were adjusted to match the available stock.";
            }

            return cart;
        }

        // =========================
        // GET CART FROM SESSION
        // =========================
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

        // =========================
        // SAVE CART TO SESSION
        // =========================
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
                JsonSerializer.Serialize(cart)
            );
        }
    }
}