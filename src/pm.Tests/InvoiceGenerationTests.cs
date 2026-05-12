using System.Collections.Concurrent;
using pm.Application.DTOs.Invoices;
using pm.Application.DTOs.InvoicePdfs;
using pm.Application.DTOs.Projects;
using pm.Application.Exceptions;
using pm.Application.Interfaces;
using pm.Application.Services;
using pm.Domain.Entities;
using Xunit;

namespace pm.Tests;

public class InvoiceGenerationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    [Fact]
    public async Task ProjectCompletionCreatesInvoiceAndPdf()
    {
        var fixture = CreateFixture();

        var result = await fixture.ProjectService.UpdateStatusAsync(_userId, _projectId, new UpdateProjectStatusRequest("completed"));

        Assert.Equal("invoiced", result.Status);
        Assert.NotNull(result.GeneratedInvoice);
        Assert.True(result.GeneratedInvoice.HasPdf);
        Assert.Equal(_projectId, result.GeneratedInvoice.ProjectId);
        Assert.Equal(_clientId, result.GeneratedInvoice.ClientId);
        Assert.Single(fixture.InvoiceRepository.Invoices);
    }

    [Fact]
    public async Task MissingProjectOrClientPreventsPdfGeneration()
    {
        var fixture = CreateFixture();
        fixture.ProjectRepository.Projects.Clear();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId));

        fixture = CreateFixture();
        fixture.ClientRepository.Clients.Clear();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId));

        Assert.Equal(0, fixture.PdfService.GeneratedCount);
    }

    [Fact]
    public async Task MissingRequiredInvoiceFieldsPreventPdfGeneration()
    {
        var fixture = CreateFixture();
        fixture.ProjectRepository.Projects[_projectId].AgreedAmount = 0;
        fixture.ClientRepository.Clients[_clientId].Email = "";

        var ex = await Assert.ThrowsAsync<ApiValidationException>(() =>
            fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId));

        Assert.Contains("Project agreed amount must be greater than 0.", ex.Errors);
        Assert.Contains("Client email is required for invoice generation.", ex.Errors);
        Assert.Equal(0, fixture.PdfService.GeneratedCount);
    }

    [Fact]
    public async Task UniqueInvoiceNumberIsGeneratedAndSaved()
    {
        var fixture = CreateFixture();

        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);

        Assert.StartsWith($"INV-{DateTime.UtcNow.Year}-", invoice.InvoiceNumber);
        Assert.EndsWith("-0001", invoice.InvoiceNumber);
        Assert.Equal(invoice.InvoiceNumber, fixture.InvoiceRepository.Invoices.Single().InvoiceNumber);
    }

    [Fact]
    public async Task ConcurrentProjectInvoiceGenerationCreatesOnlyOneInvoice()
    {
        var fixture = CreateFixture();

        var attempts = await Task.WhenAll(
            Capture(() => fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId)),
            Capture(() => fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId)));

        Assert.Single(attempts, x => x.Invoice is not null);
        Assert.Single(attempts, x => x.Exception is ApiConflictException);
        Assert.Single(fixture.InvoiceRepository.Invoices);
        Assert.Equal(fixture.InvoiceRepository.Invoices.Select(x => x.InvoiceNumber).Distinct().Count(), fixture.InvoiceRepository.Invoices.Count);
    }

    [Fact]
    public async Task GeneratedInvoiceStoresPdfReference()
    {
        var fixture = CreateFixture();

        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);

        Assert.True(invoice.HasPdf);
        Assert.NotNull(invoice.PdfGeneratedAt);
        Assert.False(string.IsNullOrWhiteSpace(fixture.InvoiceRepository.Invoices.Single().PdfFilePath));
    }

    [Fact]
    public async Task PdfAndPaymentUseSharedFinalTotalCalculation()
    {
        var fixture = CreateFixture();

        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);
        var storedInvoice = fixture.InvoiceRepository.Invoices.Single();

        Assert.Equal(121m, invoice.TotalAmount);
        Assert.Equal(12100, InvoiceAmountCalculator.ToMinorCurrencyUnits(storedInvoice));
    }

    [Fact]
    public async Task PaymentLinkIsGeneratedOnceAndSavedOnInvoice()
    {
        var fixture = CreateFixture();
        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);

        var first = await fixture.InvoiceService.CreatePaymentLinkAsync(_userId, invoice.Id);
        var second = await fixture.InvoiceService.CreatePaymentLinkAsync(_userId, invoice.Id);
        var storedInvoice = fixture.InvoiceRepository.Invoices.Single();

        Assert.Equal("https://stripe.example.test/session", first);
        Assert.Equal(first, second);
        Assert.Equal("active", storedInvoice.PaymentLinkStatus);
        Assert.Equal(first, storedInvoice.PaymentLinkUrl);
        Assert.Equal(1, fixture.StripeService.CreateCheckoutSessionCallCount);
    }

    [Fact]
    public async Task InvalidInvoiceDoesNotCallStripeForPaymentLink()
    {
        var fixture = CreateFixture();
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            ClientId = _clientId,
            InvoiceNumber = "INV-2026-9999",
            Status = "draft",
            IssueDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            Currency = "EUR",
            TotalAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        fixture.InvoiceRepository.Invoices.Add(invoice);

        await Assert.ThrowsAsync<ApiValidationException>(() =>
            fixture.InvoiceService.CreatePaymentLinkAsync(_userId, invoice.Id));

        Assert.Equal(0, fixture.StripeService.CreateCheckoutSessionCallCount);
        Assert.Equal("not_created", invoice.PaymentLinkStatus);
    }

    [Fact]
    public async Task SendInvoiceEmailsPdfAttachmentAndPaymentLinkAndRecordsResult()
    {
        var fixture = CreateFixture();
        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);

        var result = await fixture.InvoiceService.SendAsync(_userId, invoice.Id);
        var storedInvoice = fixture.InvoiceRepository.Invoices.Single();

        Assert.Equal("sent", result.Status);
        Assert.Equal("sent", storedInvoice.EmailSendStatus);
        Assert.NotNull(storedInvoice.EmailSentAt);
        Assert.Equal(1, fixture.EmailService.InvoiceSendCallCount);
        Assert.Equal("https://stripe.example.test/session", fixture.EmailService.LastPaymentLinkUrl);
        Assert.Equal("%PDF", fixture.EmailService.LastPdfHeader);
    }

    [Fact]
    public async Task OverdueReminderProcessingMarksAndSendsReminder()
    {
        var fixture = CreateFixture();
        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);
        var storedInvoice = fixture.InvoiceRepository.Invoices.Single();
        storedInvoice.Status = "sent";
        storedInvoice.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        var result = await fixture.InvoiceService.ProcessOverdueRemindersAsync(_userId);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Sent);
        Assert.Equal(0, result.Failed);
        Assert.Equal("overdue", storedInvoice.Status);
        Assert.Equal(1, storedInvoice.ReminderCount);
        Assert.Equal("sent", storedInvoice.ReminderSendStatus);
        Assert.Equal(1, fixture.EmailService.ReminderSendCallCount);
    }

    [Fact]
    public async Task MarkAsPaidUpdatesStatusAndAmount()
    {
        var fixture = CreateFixture();
        var invoice = await fixture.InvoiceService.CreateForCompletedProjectAsync(_userId, _projectId);
        var storedInvoice = fixture.InvoiceRepository.Invoices.Single();
        storedInvoice.PaymentLinkStatus = "active";

        await fixture.InvoiceRepository.MarkAsPaidAsync(_userId, invoice.Id, invoice.TotalAmount, DateTime.UtcNow);

        Assert.Equal(invoice.TotalAmount, storedInvoice.AmountPaid);
        Assert.Equal("paid", storedInvoice.Status);
    }

    private static async Task<(InvoiceResponse? Invoice, Exception? Exception)> Capture(Func<Task<InvoiceResponse>> action)
    {
        try
        {
            return (await action(), null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private TestFixture CreateFixture()
    {
        var projectRepository = new InMemoryProjectRepository();
        var clientRepository = new InMemoryClientRepository();
        var userRepository = new InMemoryUserRepository();
        var invoiceRepository = new InMemoryInvoiceRepository();
        var pdfService = new FakeInvoicePdfService();
        var emailService = new FakeEmailService();
        var stripeService = new FakeStripeService();

        userRepository.Users[_userId] = new User
        {
            Id = _userId,
            Email = "seller@example.test",
            FirstName = "Adomas",
            LastName = "Jasiukevičius",
            PreferredLanguage = "lt"
        };

        clientRepository.Clients[_clientId] = new Client
        {
            Id = _clientId,
            UserId = _userId,
            ClientType = "company",
            CompanyName = "Klientas UAB",
            Email = "client@example.test"
        };

        projectRepository.Projects[_projectId] = new Project
        {
            Id = _projectId,
            UserId = _userId,
            ClientId = _clientId,
            Name = "Svetainės kūrimas",
            Status = "active",
            AgreedAmount = 100m,
            Currency = "EUR",
            VatRate = 21m,
            PaymentTermsDays = 14,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var invoiceService = new InvoiceService(
            invoiceRepository,
            clientRepository,
            projectRepository,
            userRepository,
            emailService,
            pdfService,
            stripeService);

        var projectService = new ProjectService(projectRepository, invoiceService);

        return new TestFixture(projectService, invoiceService, projectRepository, clientRepository, invoiceRepository, pdfService, emailService, stripeService);
    }

    private sealed record TestFixture(
        ProjectService ProjectService,
        InvoiceService InvoiceService,
        InMemoryProjectRepository ProjectRepository,
        InMemoryClientRepository ClientRepository,
        InMemoryInvoiceRepository InvoiceRepository,
        FakeInvoicePdfService PdfService,
        FakeEmailService EmailService,
        FakeStripeService StripeService);

    private sealed class InMemoryInvoiceRepository : IInvoiceRepository
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, int> _yearCounters = new();
        public List<Invoice> Invoices { get; } = [];

        public Task<IReadOnlyList<Invoice>> GetAllByUserIdAsync(Guid userId) =>
            WithLock(() => (IReadOnlyList<Invoice>)Invoices.Where(x => x.UserId == userId).ToList());

        public Task<Invoice?> GetByIdAsync(Guid userId, Guid id) =>
            WithLock(() => Invoices.SingleOrDefault(x => x.UserId == userId && x.Id == id));

        public Task<Invoice?> GetByProjectIdAsync(Guid userId, Guid projectId) =>
            WithLock(() => Invoices.SingleOrDefault(x => x.UserId == userId && x.ProjectId == projectId));

        public Task<IReadOnlyList<Guid>> GetUserIdsWithOverdueInvoicesAsync(DateOnly today) =>
            WithLock(() => (IReadOnlyList<Guid>)Invoices
                .Where(x => x.DueDate < today && x.TotalAmount > x.AmountPaid && x.Status is "sent" or "overdue" or "partially_paid")
                .Select(x => x.UserId)
                .Distinct()
                .ToList());

        public Task<Invoice> CreateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems)
        {
            lock (_lock)
            {
                if (Invoices.Any(x => x.UserId == invoice.UserId && x.InvoiceNumber == invoice.InvoiceNumber) ||
                    Invoices.Any(x => x.ProjectId == invoice.ProjectId && x.ProjectId.HasValue))
                {
                    throw new ApiConflictException("Invoice number or project invoice already exists.");
                }

                invoice.LineItems = lineItems.ToList();
                Invoices.Add(invoice);
                return Task.FromResult(invoice);
            }
        }

        public Task UpdateAsync(Invoice invoice, IReadOnlyList<InvoiceLineItem> lineItems) => Task.CompletedTask;

        public Task<IReadOnlyList<Invoice>> GetOverdueReminderCandidatesAsync(Guid userId, DateOnly today) =>
            WithLock(() =>
            {
                foreach (var invoice in Invoices.Where(x => x.UserId == userId && x.Status == "sent" && x.DueDate < today && x.TotalAmount > x.AmountPaid))
                    invoice.Status = "overdue";

                return (IReadOnlyList<Invoice>)Invoices
                    .Where(x => x.UserId == userId && x.DueDate < today && x.TotalAmount > x.AmountPaid && x.Status is "overdue" or "partially_paid")
                    .ToList();
            });

        public Task<int> GetNextInvoiceSequenceAsync(Guid userId, int year)
        {
            lock (_lock)
            {
                _yearCounters.TryGetValue(year, out var current);
                _yearCounters[year] = current + 1;
                return Task.FromResult(_yearCounters[year]);
            }
        }

        public Task AddStatusHistoryAsync(Guid invoiceId, Guid changedByUserId, string? fromStatus, string toStatus) => Task.CompletedTask;

        public Task SetPdfReferenceAsync(Guid userId, Guid id, string pdfFilePath, DateTime generatedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.PdfFilePath = pdfFilePath;
            invoice.PdfGeneratedAt = generatedAt;
            return Task.CompletedTask;
        }

        public Task SavePaymentLinkAsync(Guid userId, Guid id, string paymentLinkUrl, DateTime generatedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.PaymentLinkUrl = paymentLinkUrl;
            invoice.PaymentLinkStatus = "active";
            invoice.PaymentLinkGeneratedAt = generatedAt;
            return Task.CompletedTask;
        }

        public Task RecordPaymentLinkFailureAsync(Guid userId, Guid id, string error, DateTime failedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.PaymentLinkStatus = "failed";
            invoice.PaymentLinkError = error;
            return Task.CompletedTask;
        }

        public Task DeactivatePaymentLinkAsync(Guid userId, Guid id, DateTime deactivatedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.PaymentLinkStatus = "inactive";
            invoice.PaymentLinkDeactivatedAt = deactivatedAt;
            return Task.CompletedTask;
        }

        public Task RecordInvoiceEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.EmailSendStatus = sent ? "sent" : "failed";
            invoice.EmailSentAt = sent ? attemptedAt : invoice.EmailSentAt;
            invoice.EmailLastError = error;
            return Task.CompletedTask;
        }

        public Task RecordReminderEmailResultAsync(Guid userId, Guid id, bool sent, string? error, DateTime attemptedAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.ReminderSendStatus = sent ? "sent" : "failed";
            invoice.ReminderLastSentAt = sent ? attemptedAt : invoice.ReminderLastSentAt;
            invoice.ReminderLastError = error;
            return Task.CompletedTask;
        }

        public Task<int> MarkOverdueAsync(Guid userId, DateOnly today, DateTime updatedAt)
        {
            var count = 0;
            foreach (var invoice in Invoices.Where(x => x.UserId == userId && x.Status == "sent" && x.DueDate < today && x.TotalAmount > x.AmountPaid))
            {
                invoice.Status = "overdue";
                count++;
            }

            return Task.FromResult(count);
        }

        public Task MarkSentAsync(Guid userId, Guid id, DateTime sentAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.Status = "sent";
            invoice.SentAt = sentAt;
            return Task.CompletedTask;
        }

        public Task MarkAsPaidAsync(Guid userId, Guid id, decimal amount, DateTime paidAt)
        {
            var invoice = Invoices.Single(x => x.UserId == userId && x.Id == id);
            invoice.AmountPaid = Math.Max(0, invoice.AmountPaid + amount);
            invoice.Status = invoice.AmountPaid >= invoice.TotalAmount
                ? "paid"
                : invoice.AmountPaid > 0 ? "partially_paid" : invoice.Status;
            return Task.CompletedTask;
        }

        private Task<T> WithLock<T>(Func<T> action)
        {
            lock (_lock)
            {
                return Task.FromResult(action());
            }
        }
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public ConcurrentDictionary<Guid, Project> Projects { get; } = new();

        public Task<IReadOnlyList<Project>> GetAllByUserIdAsync(Guid userId) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Values.Where(x => x.UserId == userId).ToList());

        public Task<Project?> GetByIdAsync(Guid userId, Guid id) =>
            Task.FromResult(Projects.TryGetValue(id, out var project) && project.UserId == userId ? project : null);

        public Task<Project> CreateAsync(Project project)
        {
            Projects[project.Id] = project;
            return Task.FromResult(project);
        }

        public Task UpdateAsync(Project project)
        {
            Projects[project.Id] = project;
            return Task.CompletedTask;
        }

        public Task SoftDeleteAsync(Guid userId, Guid id) => Task.CompletedTask;
        public Task AddStatusHistoryAsync(Guid projectId, Guid changedByUserId, string? fromStatus, string toStatus) => Task.CompletedTask;
    }

    private sealed class InMemoryClientRepository : IClientRepository
    {
        public ConcurrentDictionary<Guid, Client> Clients { get; } = new();
        public Task<IReadOnlyList<Client>> GetAllByUserIdAsync(Guid userId) =>
            Task.FromResult<IReadOnlyList<Client>>(Clients.Values.Where(x => x.UserId == userId).ToList());

        public Task<Client?> GetByIdAsync(Guid userId, Guid id) =>
            Task.FromResult(Clients.TryGetValue(id, out var client) && client.UserId == userId ? client : null);

        public Task<Client> CreateAsync(Client client) => Task.FromResult(client);
        public Task UpdateAsync(Client client) => Task.CompletedTask;
        public Task SoftDeleteAsync(Guid userId, Guid id) => Task.CompletedTask;
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public ConcurrentDictionary<Guid, User> Users { get; } = new();
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(Users.TryGetValue(id, out var user) ? user : null);
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<User> CreateAsync(User user) => Task.FromResult(user);
        public Task UpdateAsync(User user) => Task.CompletedTask;
        public Task UpdateLastLoginAsync(Guid userId, DateTime loginAt) => Task.CompletedTask;
        public Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash) => Task.FromResult<UserSession?>(null);
        public Task CreateSessionAsync(UserSession session) => Task.CompletedTask;
        public Task RevokeSessionAsync(string tokenHash) => Task.CompletedTask;
        public Task RevokeAllUserSessionsAsync(Guid userId) => Task.CompletedTask;
    }

    private sealed class FakeInvoicePdfService : IInvoicePdfService
    {
        public int GeneratedCount { get; private set; }

        public Task<InvoicePdfReference> GenerateAsync(Invoice invoice, Client client, Project project, User seller)
        {
            GeneratedCount++;
            return Task.FromResult(new InvoicePdfReference($"{invoice.Id:N}.pdf", DateTime.UtcNow));
        }

        public Task<InvoicePdfDownloadResponse> GetAsync(Invoice invoice) =>
            Task.FromResult(new InvoicePdfDownloadResponse($"{invoice.InvoiceNumber}.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]));
    }

    private sealed class FakeEmailService : IEmailService
    {
        public int InvoiceSendCallCount { get; private set; }
        public int ReminderSendCallCount { get; private set; }
        public string? LastPaymentLinkUrl { get; private set; }
        public string? LastPdfHeader { get; private set; }

        public Task SendInvoiceAsync(Invoice invoice, Client client, User sender, InvoicePdfDownloadResponse pdf, string paymentLinkUrl)
        {
            InvoiceSendCallCount++;
            LastPaymentLinkUrl = paymentLinkUrl;
            LastPdfHeader = System.Text.Encoding.ASCII.GetString(pdf.Content.Take(4).ToArray());
            return Task.CompletedTask;
        }

        public Task SendInvoiceReminderAsync(Invoice invoice, Client client, User sender, string? paymentLinkUrl)
        {
            ReminderSendCallCount++;
            LastPaymentLinkUrl = paymentLinkUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStripeService : IStripeService
    {
        public int CreateCheckoutSessionCallCount { get; private set; }

        public Task<string> CreateCheckoutSessionAsync(Invoice invoice)
        {
            CreateCheckoutSessionCallCount++;
            return Task.FromResult("https://stripe.example.test/session");
        }

        public Task HandleWebhookAsync(string payload, string stripeSignatureHeader) => Task.CompletedTask;
    }
}
