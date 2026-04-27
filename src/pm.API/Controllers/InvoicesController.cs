using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pm.Application.DTOs.Invoices;
using pm.Application.Interfaces;

namespace pm.API.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        _invoiceService = invoiceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _invoiceService.GetAllAsync(GetUserId());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _invoiceService.GetByIdAsync(GetUserId(), id);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request)
    {
        var result = await _invoiceService.CreateAsync(GetUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInvoiceRequest request)
    {
        var result = await _invoiceService.UpdateAsync(GetUserId(), id, request);
        return Ok(result);
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
