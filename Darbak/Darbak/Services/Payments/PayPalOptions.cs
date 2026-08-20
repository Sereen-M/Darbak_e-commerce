namespace Darbak.Services.Payments
{
    public class PayPalOptions
    {
        public const string SectionName = "PayPal";

        public string BaseUrl { get; set; } =
            "https://api-m.sandbox.paypal.com";

        public string ClientId { get; set; } =
            string.Empty;

        public string ClientSecret { get; set; } =
            string.Empty;

        public string Currency { get; set; } =
            "USD";
    }
}