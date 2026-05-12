namespace pm.Application.DTOs.InvoicePdfs;

public record InvoicePdfReference(
    string FilePath,
    DateTime GeneratedAt
);
