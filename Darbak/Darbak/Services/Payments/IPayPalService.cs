namespace Darbak.Services.Payments
{
    public interface IPayPalService
    {
        Task<PayPalCreateOrderResult> CreateOrderAsync(
            int localOrderId,
            decimal amount,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default);

        Task<PayPalCaptureResult> CaptureOrderAsync(
            string payPalOrderId,
            int localOrderId,
            CancellationToken cancellationToken = default);
    }

    public class PayPalCreateOrderResult
    {
        public bool Success { get; set; }

        public string? PayPalOrderId { get; set; }

        public string? ApprovalUrl { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public class PayPalCaptureResult
    {
        public bool Success { get; set; }

        public string? PayPalOrderId { get; set; }

        public string? CaptureId { get; set; }

        public string? Status { get; set; }

        public decimal? Amount { get; set; }

        public string? Currency { get; set; }

        public string? ErrorMessage { get; set; }
    }
}