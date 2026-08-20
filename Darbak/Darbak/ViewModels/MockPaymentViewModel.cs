namespace Darbak.ViewModels
{
    public class MockPaymentViewModel
    {
        public string PaymentToken { get; set; } =
            string.Empty;

        public string ShippingAddress { get; set; } =
            string.Empty;

        public string City { get; set; } =
            string.Empty;

        public string PhoneNumber { get; set; } =
            string.Empty;

        public List<CartItemViewModel> CartItems { get; set; } =
            new();

        public decimal TotalAmount =>
            CartItems.Sum(x =>
                x.Total);
    }
}