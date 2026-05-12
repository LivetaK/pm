using pm.Application.DTOs.Invoices;
using pm.Application.DTOs.InvoicePdfs;
using pm.Application.Exceptions;
using pm.Application.Interfaces;
using pm.Domain.Entities;

namespace pm.Application.Services;

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IInvoicePdfService _invoicePdfService;
    private readonly IStripeService _stripeService;

    public InvoiceService(
        IInvoiceRepository invoiceRepository,
        IClientRepository clientRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IInvoicePdfService invoicePdfService)
    {
        _invoiceRepository = invoiceRepository;
        _clientRepository = clientRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _invoicePdfService = invoicePdfService;
        _stripeService = null!;
    }
    
    public InvoiceService(
        IInvoiceRepository invoiceRepository,
        IClientRepository clientRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IInvoicePdfService invoicePdfService,
        IStripeService stripeService)
        : this(invoiceRepository, clientRepository, projectRepository, userRepository, emailService, invoicePdfService)
    {
        _stripeService = stripeService;
    }

    public async Task<IReadOnlyList<InvoiceResponse>> GetAllAsync(Guid userId)
    {
        await _invoiceRepository.MarkOverdueAsync(userId, DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow);
        var invoices = await _invoiceRepository.GetAllByUserIdAsync(userId);
        return invoices.Select(MapToResponse).ToList();
    }

    public async Task<InvoiceResponse> GetByIdAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> CreateAsync(Guid userId, CreateInvoiceRequest request)
    {
        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new ApiValidationException("Invoice must have at least one line item.");

        var issueDate = request.IssueDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var invoiceNumber = await GenerateInvoiceNumberAsync(userId, issueDate.Year);
        var lineItems = BuildLineItems(Guid.Empty, request.LineItems);
        var (subtotal, vatAmount, total) = InvoiceAmountCalculator.ComputeTotals(lineItems);

        await ValidateManualInvoiceAsync(userId, request, lineItems, total);

        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = request.ClientId,
            ProjectId = request.ProjectId,
            InvoiceNumber = invoiceNumber,
            Status = "draft",
            LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? "lt" : request.LanguageCode,
            IssueDate = issueDate,
            DueDate = request.DueDate,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EUR" : request.Currency.ToUpperInvariant(),
            SubtotalAmount = subtotal,
            VatAmount = vatAmount,
            TotalAmount = total,
            AmountPaid = 0,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var li in lineItems) li.InvoiceId = invoice.Id;

        await _invoiceRepository.CreateAsync(invoice, lineItems);
        await _invoiceRepository.AddStatusHistoryAsync(invoice.Id, userId, null, invoice.Status);

        invoice.LineItems = lineItems;
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> CreateForCompletedProjectAsync(Guid userId, Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(userId, projectId)
            ?? throw new KeyNotFoundException("Project not found.");

        if (project.Status is "invoiced" or "paid" or "cancelled")
            throw new ApiConflictException($"Cannot generate invoice for a project with status '{project.Status}'.");

        var existingInvoice = await _invoiceRepository.GetByProjectIdAsync(userId, project.Id);
        if (existingInvoice is not null)
            throw new ApiConflictException("Invoice has already been generated for this project.");

        var client = await _clientRepository.GetByIdAsync(userId, project.ClientId)
            ?? throw new KeyNotFoundException("Client not found.");

        var seller = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        ValidateProjectInvoiceData(project, client, seller);

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dueDate = issueDate.AddDays(project.PaymentTermsDays);
        var invoiceNumber = await GenerateInvoiceNumberAsync(userId, issueDate.Year);
        var lineItems = BuildLineItems(Guid.Empty,
        [
            new LineItemRequest(
                Description: project.Name,
                Quantity: 1m,
                Unit: "vnt.",
                UnitPrice: project.AgreedAmount!.Value,
                VatRate: project.VatRate,
                SortOrder: 1)
        ]);
        var (subtotal, vatAmount, total) = InvoiceAmountCalculator.ComputeTotals(lineItems);

        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ClientId = project.ClientId,
            ProjectId = project.Id,
            InvoiceNumber = invoiceNumber,
            Status = "draft",
            LanguageCode = seller.PreferredLanguage,
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = project.Currency,
            SubtotalAmount = subtotal,
            VatAmount = vatAmount,
            TotalAmount = total,
            AmountPaid = 0,
            Notes = project.AgreedScope,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var li in lineItems) li.InvoiceId = invoice.Id;

        try
        {
            await _invoiceRepository.CreateAsync(invoice, lineItems);
        }
        catch (ApiConflictException)
        {
            throw;
        }

        await _invoiceRepository.AddStatusHistoryAsync(invoice.Id, userId, null, invoice.Status);

        invoice.LineItems = lineItems;
        var pdfReference = await _invoicePdfService.GenerateAsync(invoice, client, project, seller);
        await _invoiceRepository.SetPdfReferenceAsync(userId, invoice.Id, pdfReference.FilePath, pdfReference.GeneratedAt);

        invoice.PdfFilePath = pdfReference.FilePath;
        invoice.PdfGeneratedAt = pdfReference.GeneratedAt;
        invoice.UpdatedAt = pdfReference.GeneratedAt;
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> UpdateAsync(Guid userId, Guid id, UpdateInvoiceRequest request)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status is "paid" or "cancelled")
            throw new ApiConflictException($"Cannot edit an invoice with status '{invoice.Status}'.");

        if (request.LineItems == null || request.LineItems.Count == 0)
            throw new ApiValidationException("Invoice must have at least one line item.");

        var lineItems = BuildLineItems(invoice.Id, request.LineItems);
        var (subtotal, vatAmount, total) = InvoiceAmountCalculator.ComputeTotals(lineItems);

        invoice.ClientId = request.ClientId;
        invoice.ProjectId = request.ProjectId;
        invoice.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? invoice.LanguageCode : request.LanguageCode;
        invoice.IssueDate = request.IssueDate;
        invoice.DueDate = request.DueDate;
        invoice.Currency = string.IsNullOrWhiteSpace(request.Currency) ? invoice.Currency : request.Currency.ToUpperInvariant();
        invoice.SubtotalAmount = subtotal;
        invoice.VatAmount = vatAmount;
        invoice.TotalAmount = total;
        invoice.Notes = request.Notes;
        invoice.UpdatedAt = DateTime.UtcNow;

        await _invoiceRepository.UpdateAsync(invoice, lineItems);

        invoice.LineItems = lineItems;
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> SendAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status is "cancelled")
            throw new InvalidOperationException("Cannot send a cancelled invoice.");

        if (invoice.Status is "paid")
            throw new InvalidOperationException("Cannot send a paid invoice.");

        var client = await _clientRepository.GetByIdAsync(userId, invoice.ClientId)
            ?? throw new KeyNotFoundException("Client not found.");

        var sender = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (string.IsNullOrWhiteSpace(invoice.PdfFilePath))
            throw new ApiValidationException("Invoice PDF must be generated before sending.");

        var paymentLinkUrl = await CreatePaymentLinkAsync(userId, id);
        invoice.PaymentLinkUrl = paymentLinkUrl;
        invoice.PaymentLinkStatus = "active";

        var pdf = await _invoicePdfService.GetAsync(invoice);
        var attemptedAt = DateTime.UtcNow;

        try
        {
            await _emailService.SendInvoiceAsync(invoice, client, sender, pdf, paymentLinkUrl);
            await _invoiceRepository.RecordInvoiceEmailResultAsync(userId, id, true, null, attemptedAt);
        }
        catch (Exception ex)
        {
            await _invoiceRepository.RecordInvoiceEmailResultAsync(userId, id, false, ex.Message, attemptedAt);
            throw;
        }

        var sentAt = DateTime.UtcNow;
        var previousStatus = invoice.Status;
        await _invoiceRepository.MarkSentAsync(userId, id, sentAt);
        await _invoiceRepository.AddStatusHistoryAsync(invoice.Id, userId, previousStatus, "sent");

        invoice.Status = "sent";
        invoice.SentAt = sentAt;
        invoice.EmailSendStatus = "sent";
        invoice.EmailSentAt = attemptedAt;
        invoice.EmailLastError = null;
        invoice.UpdatedAt = sentAt;
        return MapToResponse(invoice);
    }

    public async Task<InvoiceResponse> SendReminderAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (invoice.Status is "draft")
            throw new InvalidOperationException("Cannot send a reminder for a draft invoice. Send the invoice first.");

        if (invoice.Status is "paid")
            throw new InvalidOperationException("Invoice is already paid.");

        if (invoice.Status is "cancelled")
            throw new InvalidOperationException("Cannot send a reminder for a cancelled invoice.");

        var client = await _clientRepository.GetByIdAsync(userId, invoice.ClientId)
            ?? throw new KeyNotFoundException("Client not found.");

        var sender = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var paymentLinkUrl = invoice.PaymentLinkStatus == "active" && !string.IsNullOrWhiteSpace(invoice.PaymentLinkUrl)
            ? invoice.PaymentLinkUrl
            : null;

        if (paymentLinkUrl is null && invoice.Status is not "cancelled" and not "paid")
        {
            paymentLinkUrl = await CreatePaymentLinkAsync(userId, id);
            invoice.PaymentLinkUrl = paymentLinkUrl;
            invoice.PaymentLinkStatus = "active";
        }

        var attemptedAt = DateTime.UtcNow;
        try
        {
            await _emailService.SendInvoiceReminderAsync(invoice, client, sender, paymentLinkUrl);
            await _invoiceRepository.RecordReminderEmailResultAsync(userId, id, true, null, attemptedAt);
        }
        catch (Exception ex)
        {
            await _invoiceRepository.RecordReminderEmailResultAsync(userId, id, false, ex.Message, attemptedAt);
            throw;
        }

        invoice.ReminderSendStatus = "sent";
        invoice.ReminderLastSentAt = attemptedAt;
        invoice.ReminderCount += 1;
        invoice.ReminderLastError = null;

        return MapToResponse(invoice);
    }

    public async Task<OverdueReminderProcessResponse> ProcessOverdueRemindersAsync(Guid userId)
    {
        var candidates = await _invoiceRepository.GetOverdueReminderCandidatesAsync(
            userId,
            DateOnly.FromDateTime(DateTime.UtcNow));

        var sent = 0;
        var failed = 0;

        foreach (var invoice in candidates)
        {
            try
            {
                await SendReminderAsync(userId, invoice.Id);
                sent++;
            }
            catch
            {
                failed++;
            }
        }

        return new OverdueReminderProcessResponse(candidates.Count, sent, failed);
    }

    public async Task<string> CreatePaymentLinkAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        ValidatePaymentLinkInvoice(invoice);

        if (invoice.PaymentLinkStatus == "active" && !string.IsNullOrWhiteSpace(invoice.PaymentLinkUrl))
            return invoice.PaymentLinkUrl;

        if (invoice.Status is "cancelled")
            throw new InvalidOperationException("Cannot create payment link for a cancelled invoice.");

        if (invoice.Status is "paid")
            throw new InvalidOperationException("Invoice is already paid.");

        if (_stripeService is null)
            throw new InvalidOperationException("Payment provider is not configured.");

        try
        {
            var url = await _stripeService.CreateCheckoutSessionAsync(invoice);
            await _invoiceRepository.SavePaymentLinkAsync(userId, id, url, DateTime.UtcNow);
            return url;
        }
        catch (Exception ex)
        {
            await _invoiceRepository.RecordPaymentLinkFailureAsync(userId, id, ex.Message, DateTime.UtcNow);
            throw;
        }
    }

    public async Task<InvoicePdfDownloadResponse> GetPdfAsync(Guid userId, Guid id)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(userId, id)
            ?? throw new KeyNotFoundException("Invoice not found.");

        if (string.IsNullOrWhiteSpace(invoice.PdfFilePath))
            throw new KeyNotFoundException("Invoice PDF not found.");

        return await _invoicePdfService.GetAsync(invoice);
    }

    private async Task<string> GenerateInvoiceNumberAsync(Guid userId, int year)
    {
        var sequence = await _invoiceRepository.GetNextInvoiceSequenceAsync(userId, year);
        return $"INV-{year}-{sequence:0000}";
    }

    private static void ValidatePaymentLinkInvoice(Invoice invoice)
    {
        var errors = new List<string>();

        if (invoice.TotalAmount <= 0)
            errors.Add("Invoice total amount must be greater than 0 before creating a payment link.");

        if (invoice.TotalAmount < invoice.SubtotalAmount + invoice.VatAmount)
            errors.Add("Invoice final amount must include VAT before creating a payment link.");

        if (string.IsNullOrWhiteSpace(invoice.Currency))
            errors.Add("Invoice currency is required before creating a payment link.");

        if (errors.Count > 0)
            throw new ApiValidationException(errors);

        if (invoice.Status is "paid" or "cancelled")
            throw new ApiConflictException($"Cannot create payment link for an invoice with status '{invoice.Status}'.");
    }

    private static List<InvoiceLineItem> BuildLineItems(Guid invoiceId, List<LineItemRequest> requests)
    {
        return requests.Select((r, i) =>
        {
            return InvoiceAmountCalculator.BuildLineItem(invoiceId, r, i + 1);
        }).ToList();
    }

    private async Task ValidateManualInvoiceAsync(
        Guid userId,
        CreateInvoiceRequest request,
        IReadOnlyList<InvoiceLineItem> lineItems,
        decimal total)
    {
        var errors = ValidateLineItems(lineItems).ToList();

        if (total <= 0)
            errors.Add("Invoice total amount must be greater than 0.");

        if (request.DueDate == default)
            errors.Add("Invoice due date is required.");

        var client = await _clientRepository.GetByIdAsync(userId, request.ClientId);
        if (client is null)
            throw new KeyNotFoundException("Client not found.");

        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(userId, request.ProjectId.Value);
            if (project is null)
                throw new KeyNotFoundException("Project not found.");

            if (project.ClientId != request.ClientId)
                errors.Add("Invoice project must belong to the selected client.");
        }

        if (errors.Count > 0)
            throw new ApiValidationException(errors);
    }

    private static void ValidateProjectInvoiceData(Project project, Client client, User seller)
    {
        var errors = new List<string>();

        if (project.ClientId == Guid.Empty)
            errors.Add("Project must be linked to a client.");

        if (string.IsNullOrWhiteSpace(project.Name))
            errors.Add("Project name is required for invoice generation.");

        if (!project.AgreedAmount.HasValue || project.AgreedAmount <= 0)
            errors.Add("Project agreed amount must be greater than 0.");

        if (project.VatRate < 0)
            errors.Add("Project VAT rate must be 0 or greater.");

        if (project.PaymentTermsDays < 0)
            errors.Add("Project payment terms must be 0 days or greater.");

        if (string.IsNullOrWhiteSpace(project.Currency))
            errors.Add("Project currency is required.");

        if (client.UserId != project.UserId)
            errors.Add("Client does not belong to the project owner.");

        if (client.ClientType == "company" && string.IsNullOrWhiteSpace(client.CompanyName))
            errors.Add("Company client name is required for invoice generation.");

        if (client.ClientType != "company" && string.IsNullOrWhiteSpace(client.FirstName) && string.IsNullOrWhiteSpace(client.LastName))
            errors.Add("Individual client name is required for invoice generation.");

        if (string.IsNullOrWhiteSpace(client.Email))
            errors.Add("Client email is required for invoice generation.");

        if (string.IsNullOrWhiteSpace(seller.FirstName) || string.IsNullOrWhiteSpace(seller.LastName))
            errors.Add("Seller name is required for invoice generation.");

        if (string.IsNullOrWhiteSpace(seller.Email))
            errors.Add("Seller email is required for invoice generation.");

        if (errors.Count > 0)
            throw new ApiValidationException(errors);
    }

    private static IEnumerable<string> ValidateLineItems(IReadOnlyList<InvoiceLineItem> lineItems)
    {
        foreach (var item in lineItems)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                yield return "Invoice line item description is required.";

            if (item.Quantity <= 0)
                yield return "Invoice line item quantity must be greater than 0.";

            if (item.UnitPrice <= 0)
                yield return "Invoice line item unit price must be greater than 0.";

            if (item.VatRate < 0)
                yield return "Invoice line item VAT rate must be 0 or greater.";
        }
    }

    private static InvoiceResponse MapToResponse(Invoice i) =>
        new(i.Id, i.UserId, i.ClientId, i.ProjectId,
            i.InvoiceNumber, i.Status, i.LanguageCode,
            i.IssueDate, i.DueDate, i.SentAt,
            i.Currency, i.SubtotalAmount, i.VatAmount, i.TotalAmount,
            i.AmountPaid, i.TotalAmount - i.AmountPaid,
            i.PaymentLinkUrl, i.PaymentLinkStatus, i.PaymentLinkGeneratedAt,
            i.PaymentLinkDeactivatedAt, i.PaymentLinkError,
            !string.IsNullOrWhiteSpace(i.PdfFilePath), i.PdfGeneratedAt,
            i.EmailSendStatus, i.EmailSentAt, i.EmailLastError,
            i.ReminderSendStatus, i.ReminderLastSentAt, i.ReminderCount, i.ReminderLastError,
            i.Notes,
            i.LineItems.Select(li => new LineItemResponse(
                li.Id, li.SortOrder, li.Description, li.Quantity, li.Unit,
                li.UnitPrice, li.VatRate, li.LineSubtotal, li.LineVatAmount, li.LineTotal
            )).ToList(),
            i.CreatedAt, i.UpdatedAt);
}
