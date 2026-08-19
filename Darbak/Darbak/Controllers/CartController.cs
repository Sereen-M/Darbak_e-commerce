using System.Text.Json;
using Darbak.Data;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Mvc;

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

        // CART INDEX
        public IActionResult Index()
        {
            var cart = GetCart();

            return View(cart);
        }

        // ADD TO CART
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddToCart(int productId)
        {
            var product = _context.Products.Find(productId);

            if (product == null)
            {
                return NotFound();
            }

            if (!product.IsActive || product.StockQuantity <= 0)
            {
                return RedirectToAction(
                    "Details",
                    "Products",
                    new { id = productId }
                );
            }

            var cart = GetCart();

            var existingItem = cart
                .FirstOrDefault(x => x.ProductId == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity < product.StockQuantity)
                {
                    existingItem.Quantity++;
                }
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = 1
                });
            }

            SaveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        // UPDATE QUANTITY
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var cart = GetCart();

            var item = cart
                .FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var product = _context.Products.Find(productId);

            if (product == null)
            {
                cart.Remove(item);

                SaveCart(cart);

                return RedirectToAction(nameof(Index));
            }

            if (quantity <= 0)
            {
                cart.Remove(item);
            }
            else if (quantity <= product.StockQuantity)
            {
                item.Quantity = quantity;
            }
            else
            {
                item.Quantity = product.StockQuantity;
            }

            SaveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        // REMOVE ITEM
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int productId)
        {
            var cart = GetCart();

            var item = cart
                .FirstOrDefault(x => x.ProductId == productId);

            if (item != null)
            {
                cart.Remove(item);
            }

            SaveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        // GET CART FROM SESSION
        private List<CartItemViewModel> GetCart()
        {
            var cartJson =
                HttpContext.Session.GetString(CartSessionKey);

            if (string.IsNullOrEmpty(cartJson))
            {
                return new List<CartItemViewModel>();
            }

            return JsonSerializer
                .Deserialize<List<CartItemViewModel>>(cartJson)
                ?? new List<CartItemViewModel>();
        }

        // SAVE CART TO SESSION
        private void SaveCart(List<CartItemViewModel> cart)
        {
            HttpContext.Session.SetString(
                CartSessionKey,
                JsonSerializer.Serialize(cart)
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(CartSessionKey);

            return RedirectToAction(nameof(Index));
        }
    }
}