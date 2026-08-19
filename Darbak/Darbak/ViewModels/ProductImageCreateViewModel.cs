using System.ComponentModel.DataAnnotations;

namespace Darbak.ViewModels
{
    public class ProductImageCreateViewModel
    {
        [Range(
            1,
            int.MaxValue,
            ErrorMessage = "Invalid product."
        )]
        public int ProductId { get; set; }

        [Required]
        [StringLength(
            2048,
            ErrorMessage = "Image URL is too long."
        )]
        [Display(Name = "Image URL")]
        public string ImageUrl { get; set; } = null!;

        [Display(Name = "Main Image")]
        public bool IsMain { get; set; }
    }
}