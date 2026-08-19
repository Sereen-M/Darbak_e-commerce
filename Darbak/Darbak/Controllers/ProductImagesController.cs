using Darbak.Data;
using Darbak.Models;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        // INDEX
        [HttpGet]
        public async Task<IActionResult> Index(
            int productId)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == productId
                );

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create(
            int productId)
        {
            var product = await _context.Products
                .FindAsync(productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.ProductName = product.Name;

            var viewModel =
                new ProductImageCreateViewModel
                {
                    ProductId = product.Id
                };

            return View(viewModel);
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            ProductImageCreateViewModel viewModel)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == viewModel.ProductId
                );

            if (product == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(
                    viewModel.ImageUrl))
            {
                viewModel.ImageUrl =
                    viewModel.ImageUrl.Trim();

                if (!IsValidImageUrl(
                        viewModel.ImageUrl))
                {
                    ModelState.AddModelError(
                        nameof(viewModel.ImageUrl),
                        "Enter a valid HTTP/HTTPS URL or a local path beginning with /."
                    );
                }
                else
                {
                    var imageExists =
                        await _context.ProductImages
                            .AnyAsync(i =>
                                i.ProductId ==
                                    viewModel.ProductId &&
                                i.ImageUrl ==
                                    viewModel.ImageUrl);

                    if (imageExists)
                    {
                        ModelState.AddModelError(
                            nameof(viewModel.ImageUrl),
                            "This image has already been added to the product."
                        );
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProductName =
                    product.Name;

                return View(viewModel);
            }

            var isFirstImage =
                !product.Images.Any();

            var shouldBeMain =
                viewModel.IsMain ||
                isFirstImage;

            if (shouldBeMain)
            {
                foreach (var existingImage
                         in product.Images)
                {
                    existingImage.IsMain = false;
                }
            }

            var productImage =
                new ProductImage
                {
                    ProductId =
                        product.Id,

                    ImageUrl =
                        viewModel.ImageUrl.Trim(),

                    IsMain =
                        shouldBeMain
                };

            _context.ProductImages.Add(
                productImage
            );

            await _context.SaveChangesAsync();

            TempData["ImageSuccess"] =
                isFirstImage
                    ? "Image added successfully and set as the main image."
                    : "Image added successfully.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    productId = product.Id
                }
            );
        }

        // SET MAIN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetMain(
            int id)
        {
            var image =
                await _context.ProductImages
                    .FirstOrDefaultAsync(
                        i => i.Id == id
                    );

            if (image == null)
            {
                return NotFound();
            }

            var productId =
                image.ProductId;

            if (image.IsMain)
            {
                return RedirectToAction(
                    nameof(Index),
                    new
                    {
                        productId
                    }
                );
            }

            var productImages =
                await _context.ProductImages
                    .Where(i =>
                        i.ProductId ==
                        productId)
                    .ToListAsync();

            foreach (var productImage
                     in productImages)
            {
                productImage.IsMain = false;
            }

            image.IsMain = true;

            await _context.SaveChangesAsync();

            TempData["ImageSuccess"] =
                "Main image updated successfully.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    productId
                }
            );
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            int id)
        {
            var image =
                await _context.ProductImages
                    .FirstOrDefaultAsync(
                        i => i.Id == id
                    );

            if (image == null)
            {
                return NotFound();
            }

            var productId =
                image.ProductId;

            var wasMain =
                image.IsMain;

            _context.ProductImages.Remove(
                image
            );

            if (wasMain)
            {
                var replacementImage =
                    await _context.ProductImages
                        .Where(i =>
                            i.ProductId ==
                                productId &&
                            i.Id != id)
                        .OrderBy(i => i.Id)
                        .FirstOrDefaultAsync();

                if (replacementImage != null)
                {
                    replacementImage.IsMain =
                        true;
                }
            }

            await _context.SaveChangesAsync();

            TempData["ImageSuccess"] =
                "Image deleted successfully.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    productId
                }
            );
        }

        private static bool IsValidImageUrl(
            string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(
                    imageUrl))
            {
                return false;
            }

            imageUrl = imageUrl.Trim();

            // Local path:
            // /images/products/example.jpg
            if (imageUrl.StartsWith("/") &&
                !imageUrl.StartsWith("//"))
            {
                return true;
            }

            if (!Uri.TryCreate(
                    imageUrl,
                    UriKind.Absolute,
                    out var uri))
            {
                return false;
            }

            return uri.Scheme ==
                       Uri.UriSchemeHttp ||
                   uri.Scheme ==
                       Uri.UriSchemeHttps;
        }
    }
}