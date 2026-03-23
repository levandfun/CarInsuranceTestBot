using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarInsuranceTestBot.Services;

/// <summary>
/// OpenAI Chat Service implementation using pure HttpClient.
/// Calls https://api.openai.com/v1/chat/completions with gpt-4o-mini model.
/// </summary>
public class OpenAiChatService : IOpenAiChatService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAiChatService> _logger;

    // OpenAI API endpoint
    private const string OpenAiApiEndpoint = "https://api.openai.com/v1/chat/completions";
    private const string Model = "gpt-4o-mini";

    public OpenAiChatService(IConfiguration configuration, ILogger<OpenAiChatService> logger)
    {
        _logger = logger;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException(
                "OpenAI:ApiKey is missing from configuration.");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    /// <summary>
    /// Generates a friendly, contextual conversational reply using OpenAI.
    /// </summary>
    public async Task<string> GenerateConversationalReplyAsync(
        string systemContext,
        string userMessage,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Generating conversational reply for message: {Message}", userMessage);

            // Build request
            var request = new OpenAiChatRequest
            {
                Model = Model,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemContext },
                    new ChatMessage { Role = "user", Content = userMessage }
                },
                Temperature = 0.7,
                MaxTokens = 500
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            // Call OpenAI API
            var response = await _httpClient.PostAsync(OpenAiApiEndpoint, jsonContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "OpenAI API error: {StatusCode} - {Error}",
                    response.StatusCode,
                    errorContent);

                throw new InvalidOperationException(
                    $"OpenAI API returned status {response.StatusCode}: {errorContent}");
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var openAiResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (openAiResponse?.Choices?.Length > 0)
            {
                var reply = openAiResponse.Choices[0].Message.Content;
                _logger.LogInformation("Successfully generated conversational reply");
                return reply;
            }

            throw new InvalidOperationException("No response content from OpenAI API");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating conversational reply");
            throw;
        }
    }

    /// <summary>
    /// Generates a professional dummy car insurance policy document using OpenAI.
    /// </summary>
    public async Task<string> GeneratePolicyDocumentAsync(
        string passportData,
        string vehicleData,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Generating policy document");

            var systemPrompt =
                "You are a professional insurance policy document generator. " +
                "Generate a realistic-looking dummy car insurance policy document in Markdown format. " +
                "The policy should look professional and include all typical sections: " +
                "policy number, coverage details, terms and conditions, and premium information. " +
                "The policy number should be realistic (e.g., POL-2024-XXXXXXXXXX). " +
                "Make it look authentic but clearly indicate it's a SAMPLE POLICY. " +
                "Keep it between 500-800 words.";

            var userMessage =
                $"Generate a car insurance policy document based on this customer data:\n\n" +
                $"PASSPORT DATA:\n{passportData}\n\n" +
                $"VEHICLE DATA:\n{vehicleData}\n\n" +
                $"Please generate a complete, professional-looking policy document.";

            // Build request
            var request = new OpenAiChatRequest
            {
                Model = Model,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userMessage }
                },
                Temperature = 0.8,
                MaxTokens = 1500
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            // Call OpenAI API
            var response = await _httpClient.PostAsync(OpenAiApiEndpoint, jsonContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "OpenAI API error generating policy: {StatusCode} - {Error}",
                    response.StatusCode,
                    errorContent);

                throw new InvalidOperationException(
                    $"OpenAI API returned status {response.StatusCode}: {errorContent}");
            }

            // Parse response
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var openAiResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(
                responseContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (openAiResponse?.Choices?.Length > 0)
            {
                var policyDocument = openAiResponse.Choices[0].Message.Content;
                _logger.LogInformation("Successfully generated policy document");
                return policyDocument;
            }

            throw new InvalidOperationException("No response content from OpenAI API");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating policy document");
            throw;
        }
    }

    /// <summary>
    /// DTO for OpenAI Chat Completions API request.
    /// </summary>
    private class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("messages")]
        public ChatMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 500;
    }

    /// <summary>
    /// DTO for individual chat message.
    /// </summary>
    private class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    /// <summary>
    /// DTO for OpenAI Chat Completions API response.
    /// </summary>
    private class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public Choice[] Choices { get; set; }
    }

    /// <summary>
    /// DTO for response choice.
    /// </summary>
    private class Choice
    {
        [JsonPropertyName("message")]
        public ChatMessage Message { get; set; }
    }
}
