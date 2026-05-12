using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using pm.Application.Interfaces;
using pm.Application.Services;
using pm.Application.Settings;
using pm.Domain.Entities;

namespace pm.Infrastructure.Services
{
    public class StripeService : IStripeService
    {
        private readonly StripeSettings _settings;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly HttpClient _http;

        public StripeService(
            IOptions<StripeSettings> settings,
            IInvoiceRepository invoiceRepository,
            IPaymentRepository paymentRepository,
            IProjectRepository projectRepository)
        {
            _settings = settings.Value;
            _invoiceRepository = invoiceRepository;
            _paymentRepository = paymentRepository;
            _projectRepository = projectRepository;
            _http = new HttpClient { BaseAddress = new Uri("https://api.stripe.com/") };
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        public async Task<string> CreateCheckoutSessionAsync(Invoice invoice)
        {
            var amountMinor = InvoiceAmountCalculator.ToMinorCurrencyUnits(invoice);
            var currency = invoice.Currency?.ToLowerInvariant() ?? "eur";
            var successUrl = string.IsNullOrWhiteSpace(_settings.SuccessUrl)
                ? "http://localhost:5216/swagger?stripe=success"
                : _settings.SuccessUrl;
            var cancelUrl = string.IsNullOrWhiteSpace(_settings.CancelUrl)
                ? "http://localhost:5216/swagger?stripe=cancel"
                : _settings.CancelUrl;
            var dict = new List<KeyValuePair<string, string>>
            {
                new("success_url", successUrl),
                new("cancel_url", cancelUrl),
                new("mode", "payment"),
                new("line_items[0][price_data][currency]", currency),
                new("line_items[0][price_data][product_data][name]", invoice.InvoiceNumber),
                new("line_items[0][price_data][unit_amount]", amountMinor.ToString()),
                new("line_items[0][quantity]", "1"),
                new($"metadata[invoice_id]", invoice.Id.ToString()),
                new($"metadata[user_id]", invoice.UserId.ToString())
            };
            var content = new FormUrlEncodedContent(dict);
            var resp = await _http.PostAsync("v1/checkout/sessions", content);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Stripe API returned {(int)resp.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("url", out var urlElem))
                return urlElem.GetString() ?? throw new InvalidOperationException("Stripe returned empty url");
            throw new InvalidOperationException("Stripe did not return a checkout URL.");
        }

        public async Task HandleWebhookAsync(string payload, string stripeSignatureHeader)
        {
            if (string.IsNullOrWhiteSpace(_settings.WebhookSecret))
                throw new InvalidOperationException("Stripe webhook secret not configured.");
            var parts = stripeSignatureHeader?.Split(',') ?? Array.Empty<string>();
            var tPart = Array.Find(parts, p => p.StartsWith("t="));
            var v1Part = Array.Find(parts, p => p.StartsWith("v1="));
            if (tPart is null || v1Part is null)
                throw new InvalidOperationException("Invalid Stripe signature header.");
            var timestamp = tPart.Substring(2);
            var signature = v1Part.Substring(3);
            var signedPayload = Encoding.UTF8.GetBytes(timestamp + "." + payload);
            var secretBytes = Encoding.UTF8.GetBytes(_settings.WebhookSecret);
            using var hasher = new HMACSHA256(secretBytes);
            var computed = hasher.ComputeHash(signedPayload);
            var computedHex = BitConverter.ToString(computed).Replace("-", "").ToLowerInvariant();
            if (!SecureEquals(computedHex, signature))
                throw new UnauthorizedAccessException("Invalid Stripe webhook signature.");
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (type == "checkout.session.completed" || type == "payment_intent.succeeded")
            {
                var obj = root.GetProperty("data").GetProperty("object");
                var metadata = obj.TryGetProperty("metadata", out var m) ? m : default;
                if (!metadata.ValueKind.Equals(JsonValueKind.Undefined) && metadata.TryGetProperty("invoice_id", out var invoiceIdElem) && metadata.TryGetProperty("user_id", out var userIdElem))
                {
                    var invoiceId = Guid.Parse(invoiceIdElem.GetString()!);
                    var userId = Guid.Parse(userIdElem.GetString()!);
                    long amountTotal = 0;
                    if (obj.TryGetProperty("amount_total", out var at))
                    {
                        amountTotal = at.GetInt64();
                    }
                    else if (obj.TryGetProperty("amount_received", out var ar))
                    {
                        amountTotal = ar.GetInt64();
                    }
                    else if (obj.TryGetProperty("amount", out var a))
                    {
                        amountTotal = a.GetInt64();
                    }
                    var amount = amountTotal > 0 ? amountTotal / 100m : 0m;
                    var currency = obj.TryGetProperty("currency", out var cur) ? cur.GetString()?.ToUpperInvariant() : "EUR";
                    var providerPaymentId = obj.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (obj.TryGetProperty("payment_intent", out var pi2) && !string.IsNullOrWhiteSpace(pi2.GetString()))
                        providerPaymentId = pi2.GetString();

                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        UserId = userId,
                        Amount = amount,
                        Currency = currency ?? "EUR",
                        Provider = "stripe",
                        ProviderPaymentId = providerPaymentId ?? string.Empty,
                        Status = "succeeded",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _paymentRepository.CreateAsync(payment);
                    await _invoiceRepository.MarkAsPaidAsync(userId, invoiceId, amount, DateTime.UtcNow);
                    await SynchronizePaidInvoiceAsync(userId, invoiceId);
                }
            }
            else if (type == "charge.refunded")
            {
                var obj = root.GetProperty("data").GetProperty("object");
                var refunded = obj.TryGetProperty("amount_refunded", out var ar2) ? ar2.GetInt64() : 0L;
                var chargeId = obj.GetProperty("id").GetString();
                var paymentIntentId = obj.TryGetProperty("payment_intent", out var pi3) ? pi3.GetString() : null;
                decimal amount = refunded > 0 ? refunded / 100m : 0m;
                Guid invoiceId = Guid.Empty;
                Guid userId = Guid.Empty;

                if (obj.TryGetProperty("metadata", out var meta) && meta.ValueKind != JsonValueKind.Undefined && meta.TryGetProperty("invoice_id", out var invMeta) && meta.TryGetProperty("user_id", out var usrMeta))
                {
                    invoiceId = Guid.Parse(invMeta.GetString()!);
                    userId = Guid.Parse(usrMeta.GetString()!);
                }
                else if (!string.IsNullOrWhiteSpace(paymentIntentId))
                {
                    var orig = await _paymentRepository.GetByProviderPaymentIdAsync(paymentIntentId);
                    if (orig != null)
                    {
                        invoiceId = orig.InvoiceId;
                        userId = orig.UserId;
                    }
                }

                if (invoiceId != Guid.Empty && userId != Guid.Empty && amount > 0)
                {
                    var refund = new Payment
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        UserId = userId,
                        Amount = -amount,
                        Currency = obj.TryGetProperty("currency", out var cur2) ? cur2.GetString()?.ToUpperInvariant() ?? "EUR" : "EUR",
                        Provider = "stripe",
                        ProviderPaymentId = chargeId ?? string.Empty,
                        Status = "refunded",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _paymentRepository.CreateAsync(refund);
                    await _invoiceRepository.MarkAsPaidAsync(userId, invoiceId, -amount, DateTime.UtcNow);
                }
            }
            else if (type == "payment_intent.payment_failed" || type == "invoice.payment_failed")
            {
                var obj = root.GetProperty("data").GetProperty("object");
                var providerPaymentId = obj.TryGetProperty("id", out var idp) ? idp.GetString() : null;
                var metadata = obj.TryGetProperty("metadata", out var mm) ? mm : default;
                if (!string.IsNullOrWhiteSpace(providerPaymentId) && !metadata.ValueKind.Equals(JsonValueKind.Undefined) && metadata.TryGetProperty("invoice_id", out var invf) && metadata.TryGetProperty("user_id", out var usrf))
                {
                    var invoiceId = Guid.Parse(invf.GetString()!);
                    var userId = Guid.Parse(usrf.GetString()!);
                    var payment = new Payment
                    {
                        Id = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        UserId = userId,
                        Amount = 0m,
                        Currency = obj.TryGetProperty("currency", out var c3) ? c3.GetString()?.ToUpperInvariant() ?? "EUR" : "EUR",
                        Provider = "stripe",
                        ProviderPaymentId = providerPaymentId,
                        Status = "failed",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _paymentRepository.CreateAsync(payment);
                }
            }
        }

        private async Task SynchronizePaidInvoiceAsync(Guid userId, Guid invoiceId)
        {
            var invoice = await _invoiceRepository.GetByIdAsync(userId, invoiceId);
            if (invoice?.Status != "paid")
                return;

            var now = DateTime.UtcNow;
            await _invoiceRepository.DeactivatePaymentLinkAsync(userId, invoiceId, now);

            if (!invoice.ProjectId.HasValue)
                return;

            var project = await _projectRepository.GetByIdAsync(userId, invoice.ProjectId.Value);
            if (project is null || project.Status == "paid")
                return;

            var oldStatus = project.Status;
            project.Status = "paid";
            project.CompletedAt ??= now;
            project.UpdatedAt = now;
            await _projectRepository.UpdateAsync(project);
            await _projectRepository.AddStatusHistoryAsync(project.Id, userId, oldStatus, "paid");
        }

        private static bool SecureEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;
            var diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
