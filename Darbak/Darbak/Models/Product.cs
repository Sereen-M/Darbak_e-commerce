using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Darbak.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = null!;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Required]
        [Range(
            typeof(decimal),
            "0.01",
            "999999999.99",
            ErrorMessage = "Price must be greater than 0."
        )]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Range(
            0,
            int.MaxValue,
            ErrorMessage = "Stock quantity cannot be negative."
        )]
        public int StockQuantity { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Please select a category."
        )]
        public int CategoryId { get; set; }

        public Category Category { get; set; } = null!;

        public ICollection<ProductImage> Images { get; set; }
            = new List<ProductImage>();

        public ICollection<Review> Reviews { get; set; }
            = new List<Review>();

        public ICollection<WishlistItem> WishlistItems { get; set; }
            = new List<WishlistItem>();

        public ICollection<OrderItem> OrderItems { get; set; }
            = new List<OrderItem>();
    }
}