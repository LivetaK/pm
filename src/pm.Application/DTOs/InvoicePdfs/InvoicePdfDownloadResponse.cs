namespace pm.Application.DTOs.InvoicePdfs;

public record InvoicePdfDownloadResponse(
    string FileName,
    string ContentType,
    byte[] Content
);
