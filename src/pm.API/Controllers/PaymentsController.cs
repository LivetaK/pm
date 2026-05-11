using System.Security.Claims;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pm.Application.Interfaces;
using pm.Application.DTOs.Payments;

namespace pm.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IStripeService _stripeService;
    private readonly IPaymentRepository _paymentRepository;

    public PaymentsController(IStripeService stripeService, IPaymentRepository paymentRepository)
    {
        _stripeService = stripeService;
        _paymentRepository = paymentRepository;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var sig = Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;
        await _stripeService.HandleWebhookAsync(body, sig);
        return Ok();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get([FromQuery] Guid? invoiceId)
    {
        if (invoiceId == null) return BadRequest(new { error = "invoiceId is required" });
        var payments = await _paymentRepository.GetByInvoiceIdAsync(invoiceId.Value);
        var dto = payments.Select(p => new PaymentResponse(
            p.Id, p.InvoiceId, p.UserId, p.Amount, p.Currency, p.Provider, p.ProviderPaymentId, p.Status ?? string.Empty, p.CreatedAt));
        return Ok(dto);
    }
}




