namespace CarInsuranceTestBot.Services;

/// <summary>
/// Contract for the Mindee OCR service that extracts structured data
/// from document images (passports, vehicle documents, etc.).
/// </summary>
public interface IMindeeService
{
    /// <summary>
    /// Extracts structured data from a document image using Mindee API.
    /// </summary>
    /// <param name="fileStream">The document image as a stream (PNG/JPEG).</param>
    /// <param name="documentType">The type of document: "passport" or "vehicle".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A nicely formatted string containing the extracted data.</returns>
    Task<string> ExtractDocumentDataAsync(
        Stream fileStream,
        string documentType,
        CancellationToken ct = default);
}
