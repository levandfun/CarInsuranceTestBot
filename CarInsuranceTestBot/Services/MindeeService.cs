using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarInsuranceTestBot.Services;

/// <summary>
/// A drop-in replacement for OCR extraction.
/// Uses OpenAI Vision (GPT-4o-mini) to read documents directly,
/// bypassing Mindee API versioning issues.
/// </summary>
public class MindeeService : IMindeeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MindeeService> _logger;

    public MindeeService(IConfiguration configuration, ILogger<MindeeService> logger)
    {
        _logger = logger;

        // Читаем ключ OpenAI вместо Mindee
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is missing from configuration.");

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> ExtractDocumentDataAsync(Stream fileStream, string documentType, CancellationToken ct = default)
    {
        try
        {
            // 1. Конвертируем картинку в Base64 для отправки в нейронку
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream, ct);
            var base64Image = Convert.ToBase64String(memoryStream.ToArray());

            // 2. Готовим промпт (инструкцию) для AI в зависимости от типа документа
            var prompt = documentType.ToLowerInvariant() == "passport"
                ? "Analyze this passport image and extract the following data: Given Names, Surname, Country, Passport Number, Date of Birth, Expiry Date. Format the output as a clean list without markdown symbols. If a field is not visible, write 'Not found'."
                : "Analyze this vehicle document or receipt image and extract the following data: Vendor/Dealership name, Date, Total Amount, and any vehicle details (like VIN or model) if present. Format the output as a clean list without markdown symbols. If a field is not visible, write 'Not found'.";

            // 3. Формируем JSON-запрос для OpenAI API (gpt-4o-mini отлично и дешево читает фото)
            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:image/jpeg;base64,{base64Image}" }
                            }
                        }
                    }
                },
                max_tokens = 300
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // 4. Отправляем прямой HTTP-запрос в OpenAI
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", jsonContent, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenAI API Error: {StatusCode} - {Error}", response.StatusCode, errorBody);
                return "❌ Error: AI Vision service failed to process the image.";
            }

            // 5. Парсим ответ
            var responseString = await response.Content.ReadAsStringAsync(ct);
            using var jsonDoc = JsonDocument.Parse(responseString);

            var extractedText = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var emoji = documentType.ToLowerInvariant() == "passport" ? "📋" : "🚗";
            return $"{emoji} **Data Extracted via AI Vision:**\n\n{extractedText}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document with OpenAI Vision.");
            return $"❌ Error extracting {documentType} data: {ex.Message}";
        }
    }
}