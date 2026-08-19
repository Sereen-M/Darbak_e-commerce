using System.ComponentModel.DataAnnotations;

namespace Darbak.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        [StringLength(200)]
        public string ShippingAddress { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string City { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string PhoneNumber { get; set; } = null!;

        public List<CartItemViewModel> CartItems { get; set; } = new();

        public decimal TotalAmount =>
            CartItems.Sum(x => x.Total);
    }
}