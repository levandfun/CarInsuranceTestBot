namespace CarInsuranceTestBot.Services;

/// <summary>
/// Service for generating conversational AI responses and policy documents using OpenAI API.
/// Uses pure HttpClient to call OpenAI REST API.
/// </summary>
public interface IOpenAiChatService
{
    /// <summary>
    /// Generates a friendly, contextual conversational reply using OpenAI.
    /// </summary>
    /// <param name="systemContext">System prompt to set the AI's behavior and context.</param>
    /// <param name="userMessage">The user's message to respond to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated conversational reply as string.</returns>
    Task<string> GenerateConversationalReplyAsync(
        string systemContext,
        string userMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a professional dummy car insurance policy document based on extracted user data.
    /// </summary>
    /// <param name="passportData">Extracted passport data from OCR.</param>
    /// <param name="vehicleData">Extracted vehicle data from OCR.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated policy document text.</returns>
    Task<string> GeneratePolicyDocumentAsync(
        string passportData,
        string vehicleData,
        CancellationToken ct = default);
}
