using Darbak.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Darbak.Models;
using Darbak.ViewModels;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductImagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductImagesController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
        [HttpGet]
        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products
                .FindAsync(productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.ProductName = product.Name;

            var viewModel = new ProductImageCreateViewModel
            {
                ProductId = product.Id
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ProductImageCreateViewModel viewModel)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == viewModel.ProductId);

            if (product == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProductName = product.Name;

                return View(viewModel);
            }

            
            if (viewModel.IsMain)
            {
                foreach (var image in product.Images)
                {
                    image.IsMain = false;
                }
            }

            var productImage = new ProductImage
            {
                ProductId = viewModel.ProductId,
                ImageUrl = viewModel.ImageUrl,
                IsMain = viewModel.IsMain
            };

            _context.ProductImages.Add(productImage);

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Index),
                new { productId = viewModel.ProductId }
            );
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMain(int id)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i => i.Id == id);

            if (image == null)
            {
                return NotFound();
            }

            var productImages = await _context.ProductImages
                .Where(i => i.ProductId == image.ProductId)
                .ToListAsync();

            foreach (var productImage in productImages)
            {
                productImage.IsMain = false;
            }

            image.IsMain = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Index),
                new { productId = image.ProductId }
            );
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var image = await _context.ProductImages
                .FirstOrDefaultAsync(i => i.Id == id);

            if (image == null)
            {
                return NotFound();
            }

            var productId = image.ProductId;
            var wasMain = image.IsMain;

            _context.ProductImages.Remove(image);

            
            if (wasMain)
            {
                var replacementImage =
                    await _context.ProductImages
                        .Where(i =>
                            i.ProductId == productId &&
                            i.Id != id)
                        .FirstOrDefaultAsync();

                if (replacementImage != null)
                {
                    replacementImage.IsMain = true;
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(Index),
                new { productId = productId }
            );
        }
    }
}