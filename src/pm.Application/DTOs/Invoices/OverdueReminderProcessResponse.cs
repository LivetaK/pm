namespace pm.Application.DTOs.Invoices;

public record OverdueReminderProcessResponse(
    int Candidates,
    int Sent,
    int Failed
);
