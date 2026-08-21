using System.ComponentModel.DataAnnotations;

namespace Darbak.Models
{
    public class ProductImage
    {
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; } = null!;

        public bool IsMain { get; set; }

        public int ProductId { get; set; }

        public Product Product { get; set; } = null!;
    }
}