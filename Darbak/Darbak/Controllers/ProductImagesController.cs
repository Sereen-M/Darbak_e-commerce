using Darbak.Data;
using Darbak.Models;
using Darbak.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Darbak.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ProductImagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaxImageSize =
            5 * 1024 * 1024;

        public ProductImagesController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // INDEX
        [HttpGet]
        public async Task<IActionResult> Index(
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

            ViewBag.ProductName =
                product.Name;

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
            ProductImageCreateViewModel viewModel,
            IFormFile? imageFile)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .FirstOrDefaultAsync(
                    p => p.Id == viewModel.ProductId);

            if (product == null)
            {
                return NotFound();
            }

            /*
             * ImageUrl is no longer entered by the user.
             * It will be generated after securely saving
             * the uploaded image.
             */
            ModelState.Remove(
                nameof(viewModel.ImageUrl));

            var validationError =
                await ValidateImageAsync(
                    imageFile);

            if (validationError != null)
            {
                ModelState.AddModelError(
                    "imageFile",
                    validationError);
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ProductName =
                    product.Name;

                return View(viewModel);
            }

            var extension =
                Path.GetExtension(
                        imageFile!.FileName)
                    .ToLowerInvariant();

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var webRootPath =
                _environment.WebRootPath
                ?? Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");

            var uploadDirectory =
                Path.Combine(
                    webRootPath,
                    "images",
                    "products");

            Directory.CreateDirectory(
                uploadDirectory);

            var physicalPath =
                Path.Combine(
                    uploadDirectory,
                    fileName);

            var relativePath =
                $"/images/products/{fileName}";

            try
            {
                await using (
                    var fileStream =
                    new FileStream(
                        physicalPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
                {
                    await imageFile.CopyToAsync(
                        fileStream);
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
                        existingImage.IsMain =
                            false;
                    }
                }

                var productImage =
                    new ProductImage
                    {
                        ProductId =
                            product.Id,

                        ImageUrl =
                            relativePath,

                        IsMain =
                            shouldBeMain
                    };

                _context.ProductImages.Add(
                    productImage);

                await _context.SaveChangesAsync();

                TempData["ImageSuccess"] =
                    isFirstImage
                        ? "Image uploaded successfully and set as the main image."
                        : "Image uploaded successfully.";
            }
            catch
            {
                if (System.IO.File.Exists(
                        physicalPath))
                {
                    System.IO.File.Delete(
                        physicalPath);
                }

                throw;
            }

            return RedirectToAction(
                nameof(Index),
                new
                {
                    productId = product.Id
                });
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
                        i => i.Id == id);

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
                    });
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
                });
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
                        i => i.Id == id);

            if (image == null)
            {
                return NotFound();
            }

            var productId =
                image.ProductId;

            var wasMain =
                image.IsMain;

            var imageUrl =
                image.ImageUrl;

            _context.ProductImages.Remove(
                image);

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

            DeleteLocalImageFile(
                imageUrl);

            TempData["ImageSuccess"] =
                "Image deleted successfully.";

            return RedirectToAction(
                nameof(Index),
                new
                {
                    productId
                });
        }

        private static async Task<string?>
            ValidateImageAsync(
                IFormFile? imageFile)
        {
            if (imageFile == null ||
                imageFile.Length == 0)
            {
                return "Please select an image.";
            }

            if (imageFile.Length >
                MaxImageSize)
            {
                return
                    "The image must not exceed 5 MB.";
            }

            var extension =
                Path.GetExtension(
                        imageFile.FileName)
                    .ToLowerInvariant();

            var expectedContentType =
                extension switch
                {
                    ".jpg" =>
                        "image/jpeg",

                    ".jpeg" =>
                        "image/jpeg",

                    ".png" =>
                        "image/png",

                    ".webp" =>
                        "image/webp",

                    _ =>
                        null
                };

            if (expectedContentType == null)
            {
                return
                    "Only JPG, JPEG, PNG, and WebP images are allowed.";
            }

            if (!string.Equals(
                    imageFile.ContentType,
                    expectedContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return
                    "The uploaded file type does not match its extension.";
            }

            if (!await HasValidImageSignatureAsync(
                    imageFile,
                    extension))
            {
                return
                    "The selected file is not a valid image.";
            }

            return null;
        }

        private static async Task<bool>
            HasValidImageSignatureAsync(
                IFormFile imageFile,
                string extension)
        {
            var header =
                new byte[12];

            await using var stream =
                imageFile.OpenReadStream();

            var bytesRead =
                await stream.ReadAsync(
                    header.AsMemory(
                        0,
                        header.Length));

            if (extension == ".jpg" ||
                extension == ".jpeg")
            {
                return bytesRead >= 3 &&
                       header[0] == 0xFF &&
                       header[1] == 0xD8 &&
                       header[2] == 0xFF;
            }

            if (extension == ".png")
            {
                return bytesRead >= 8 &&
                       header[0] == 0x89 &&
                       header[1] == 0x50 &&
                       header[2] == 0x4E &&
                       header[3] == 0x47 &&
                       header[4] == 0x0D &&
                       header[5] == 0x0A &&
                       header[6] == 0x1A &&
                       header[7] == 0x0A;
            }

            if (extension == ".webp")
            {
                return bytesRead >= 12 &&
                       header[0] == 0x52 &&
                       header[1] == 0x49 &&
                       header[2] == 0x46 &&
                       header[3] == 0x46 &&
                       header[8] == 0x57 &&
                       header[9] == 0x45 &&
                       header[10] == 0x42 &&
                       header[11] == 0x50;
            }

            return false;
        }

        private void DeleteLocalImageFile(
            string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(
                    imageUrl))
            {
                return;
            }

            const string localPrefix =
                "/images/products/";

            if (!imageUrl.StartsWith(
                    localPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Old external URL.
                return;
            }

            var fileName =
                Path.GetFileName(
                    imageUrl);

            if (string.IsNullOrWhiteSpace(
                    fileName))
            {
                return;
            }

            var webRootPath =
                _environment.WebRootPath
                ?? Path.Combine(
                    _environment.ContentRootPath,
                    "wwwroot");

            var physicalPath =
                Path.Combine(
                    webRootPath,
                    "images",
                    "products",
                    fileName);

            if (System.IO.File.Exists(
                    physicalPath))
            {
                System.IO.File.Delete(
                    physicalPath);
            }
        }
    }
}