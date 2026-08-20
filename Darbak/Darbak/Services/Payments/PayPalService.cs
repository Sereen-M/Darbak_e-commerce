using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Darbak.Services.Payments
{
    public class PayPalService : IPayPalService
    {
        private readonly HttpClient _httpClient;
        private readonly PayPalOptions _options;

        public PayPalService(
            HttpClient httpClient,
            IOptions<PayPalOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<PayPalCreateOrderResult>
            CreateOrderAsync(
                int localOrderId,
                decimal amount,
                string returnUrl,
                string cancelUrl,
                CancellationToken cancellationToken = default)
        {
            if (amount <= 0)
            {
                return new PayPalCreateOrderResult
                {
                    Success = false,
                    ErrorMessage =
                        "The payment amount must be greater than zero."
                };
            }

            var accessToken =
                await GetAccessTokenAsync(
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    accessToken))
            {
                return new PayPalCreateOrderResult
                {
                    Success = false,
                    ErrorMessage =
                        "PayPal authentication failed."
                };
            }

            var amountValue =
                amount.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture);

            var requestBody = new
            {
                intent = "CAPTURE",

                purchase_units = new[]
                {
                    new
                    {
                        reference_id =
                            $"Darbak-{localOrderId}",

                        custom_id =
                            localOrderId.ToString(),

                        invoice_id =
                            $"DARB-{localOrderId}",

                        amount = new
                        {
                            currency_code =
                                _options.Currency,

                            value =
                                amountValue
                        }
                    }
                },

                payment_source = new
                {
                    paypal = new
                    {
                        experience_context = new
                        {
                            payment_method_preference =
                                "IMMEDIATE_PAYMENT_REQUIRED",

                            user_action =
                                "PAY_NOW",

                            return_url =
                                returnUrl,

                            cancel_url =
                                cancelUrl
                        }
                    }
                }
            };

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildUrl(
                        "/v2/checkout/orders"));

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            /*
             * Deterministic request ID.
             * Retrying Create for the same local
             * order won't intentionally create
             * another PayPal operation.
             */
            request.Headers.Add(
                "PayPal-Request-Id",
                $"darbak-create-{localOrderId}");

            request.Headers.Add(
                "Prefer",
                "return=representation");

            request.Content =
                JsonContent.Create(
                    requestBody);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            var responseText =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PayPalCreateOrderResult
                {
                    Success = false,

                    ErrorMessage =
                        $"PayPal Create Order failed with status {(int)response.StatusCode}."
                };
            }

            using var document =
                JsonDocument.Parse(
                    responseText);

            var root =
                document.RootElement;

            var payPalOrderId =
                root.TryGetProperty(
                    "id",
                    out var idElement)
                    ? idElement.GetString()
                    : null;

            string? approvalUrl = null;

            if (root.TryGetProperty(
                    "links",
                    out var linksElement))
            {
                foreach (var link
                         in linksElement.EnumerateArray())
                {
                    var relation =
                        link.TryGetProperty(
                            "rel",
                            out var relElement)
                            ? relElement.GetString()
                            : null;

                    if (relation != "payer-action" &&
                        relation != "approve")
                    {
                        continue;
                    }

                    approvalUrl =
                        link.TryGetProperty(
                            "href",
                            out var hrefElement)
                            ? hrefElement.GetString()
                            : null;

                    if (!string.IsNullOrWhiteSpace(
                            approvalUrl))
                    {
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(
                    payPalOrderId) ||
                string.IsNullOrWhiteSpace(
                    approvalUrl))
            {
                return new PayPalCreateOrderResult
                {
                    Success = false,

                    ErrorMessage =
                        "PayPal did not return a valid order ID and approval URL."
                };
            }

            return new PayPalCreateOrderResult
            {
                Success = true,
                PayPalOrderId = payPalOrderId,
                ApprovalUrl = approvalUrl
            };
        }

        public async Task<PayPalCaptureResult>
            CaptureOrderAsync(
                string payPalOrderId,
                int localOrderId,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(
                    payPalOrderId))
            {
                return new PayPalCaptureResult
                {
                    Success = false,
                    ErrorMessage =
                        "The PayPal order ID is missing."
                };
            }

            var accessToken =
                await GetAccessTokenAsync(
                    cancellationToken);

            if (string.IsNullOrWhiteSpace(
                    accessToken))
            {
                return new PayPalCaptureResult
                {
                    Success = false,
                    ErrorMessage =
                        "PayPal authentication failed."
                };
            }

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildUrl(
                        $"/v2/checkout/orders/{Uri.EscapeDataString(payPalOrderId)}/capture"));

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            request.Headers.Add(
                "PayPal-Request-Id",
                $"darbak-capture-{localOrderId}");

            request.Headers.Add(
                "Prefer",
                "return=representation");

            request.Content =
                JsonContent.Create(
                    new { });

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            var responseText =
                await response.Content
                    .ReadAsStringAsync(
                        cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PayPalCaptureResult
                {
                    Success = false,

                    PayPalOrderId =
                        payPalOrderId,

                    ErrorMessage =
                        $"PayPal Capture failed with status {(int)response.StatusCode}."
                };
            }

            using var document =
                JsonDocument.Parse(
                    responseText);

            var root =
                document.RootElement;

            var returnedOrderId =
                root.TryGetProperty(
                    "id",
                    out var orderIdElement)
                    ? orderIdElement.GetString()
                    : null;

            var orderStatus =
                root.TryGetProperty(
                    "status",
                    out var statusElement)
                    ? statusElement.GetString()
                    : null;

            string? captureId = null;
            string? captureStatus = null;
            string? currency = null;
            decimal? capturedAmount = null;

            if (root.TryGetProperty(
                    "purchase_units",
                    out var purchaseUnits) &&
                purchaseUnits.GetArrayLength() > 0)
            {
                var purchaseUnit =
                    purchaseUnits[0];

                if (purchaseUnit.TryGetProperty(
                        "payments",
                        out var payments) &&
                    payments.TryGetProperty(
                        "captures",
                        out var captures) &&
                    captures.GetArrayLength() > 0)
                {
                    var capture =
                        captures[0];

                    captureId =
                        capture.TryGetProperty(
                            "id",
                            out var captureIdElement)
                            ? captureIdElement.GetString()
                            : null;

                    captureStatus =
                        capture.TryGetProperty(
                            "status",
                            out var captureStatusElement)
                            ? captureStatusElement.GetString()
                            : null;

                    if (capture.TryGetProperty(
                            "amount",
                            out var amountElement))
                    {
                        currency =
                            amountElement.TryGetProperty(
                                "currency_code",
                                out var currencyElement)
                                ? currencyElement.GetString()
                                : null;

                        var amountText =
                            amountElement.TryGetProperty(
                                "value",
                                out var valueElement)
                                ? valueElement.GetString()
                                : null;

                        if (decimal.TryParse(
                                amountText,
                                NumberStyles.Number,
                                CultureInfo.InvariantCulture,
                                out var parsedAmount))
                        {
                            capturedAmount =
                                parsedAmount;
                        }
                    }
                }
            }

            var completed =
                string.Equals(
                    orderStatus,
                    "COMPLETED",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    captureStatus,
                    "COMPLETED",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(
                    captureId);

            return new PayPalCaptureResult
            {
                Success =
                    completed,

                PayPalOrderId =
                    returnedOrderId
                    ?? payPalOrderId,

                CaptureId =
                    captureId,

                Status =
                    captureStatus
                    ?? orderStatus,

                Amount =
                    capturedAmount,

                Currency =
                    currency,

                ErrorMessage =
                    completed
                        ? null
                        : "PayPal did not return a completed payment."
            };
        }

        private async Task<string?>
            GetAccessTokenAsync(
                CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(
                    _options.ClientId) ||
                string.IsNullOrWhiteSpace(
                    _options.ClientSecret))
            {
                return null;
            }

            var credentials =
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        $"{_options.ClientId}:{_options.ClientSecret}"));

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Post,
                    BuildUrl(
                        "/v1/oauth2/token"));

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Basic",
                    credentials);

            request.Content =
                new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] =
                            "client_credentials"
                    });

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document =
                JsonDocument.Parse(
                    await response.Content
                        .ReadAsStringAsync(
                            cancellationToken));

            return document.RootElement
                .TryGetProperty(
                    "access_token",
                    out var accessTokenElement)
                ? accessTokenElement.GetString()
                : null;
        }

        private string BuildUrl(
            string path)
        {
            return
                $"{_options.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}