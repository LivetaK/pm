using pm.Domain.Entities;

namespace pm.Application.Interfaces;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(Invoice invoice);
    Task HandleWebhookAsync(string payload, string stripeSignatureHeader);
}


