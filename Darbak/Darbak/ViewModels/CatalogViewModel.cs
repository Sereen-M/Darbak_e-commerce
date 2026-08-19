using Darbak.Models;

namespace Darbak.ViewModels
{
    public class CatalogViewModel
    {
        public List<CatalogProductViewModel> Products { get; set; }
            = new();

        public List<Category> Categories { get; set; }
            = new();

        public string? Search { get; set; }

        public int? CategoryId { get; set; }

        public string? Sort { get; set; }
    }

    public class CatalogProductViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string CategoryName { get; set; } = null!;

        public string? MainImageUrl { get; set; }

        public double AverageRating { get; set; }

        public int ReviewCount { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}