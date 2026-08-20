namespace Darbak.ViewModels
{
    public class HomeViewModel
    {
        public List<HomeProductViewModel> LatestProducts { get; set; }
            = new();

        public List<HomeCategoryViewModel> Categories { get; set; }
            = new();

        public List<HomeTestimonialViewModel> Testimonials { get; set; }
            = new();
    }

    public class HomeProductViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public decimal Price { get; set; }

        public int StockQuantity { get; set; }

        public string CategoryName { get; set; } = null!;

        public string? MainImageUrl { get; set; }

        public double AverageRating { get; set; }

        public int ReviewCount { get; set; }
    }

    public class HomeCategoryViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int ProductCount { get; set; }
    }

    public class HomeTestimonialViewModel
    {
        public int Id { get; set; }

        public string Content { get; set; } = null!;

        public string UserName { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}