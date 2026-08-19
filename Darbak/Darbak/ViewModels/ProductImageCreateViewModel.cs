using System.ComponentModel.DataAnnotations;

namespace Darbak.ViewModels
{
    public class ProductImageCreateViewModel
    {
        public int ProductId { get; set; }

        [Required]
        public string ImageUrl { get; set; } = null!;

        public bool IsMain { get; set; }
    }
}