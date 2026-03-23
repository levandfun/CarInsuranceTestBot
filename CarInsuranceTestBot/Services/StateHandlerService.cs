using System.Collections.Concurrent;
using System.Text;
using CarInsuranceTestBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CarInsuranceTestBot.Services;

/// <summary>
/// State machine dispatcher that routes incoming Telegram updates to the correct
/// handler based on the current conversation state. Uses OpenAI for intelligent
/// conversational capabilities and policy document generation.
/// </summary>
public class StateHandlerService : IStateHandlerService
{
    private readonly ITelegramBotClient _bot;
    private readonly IMindeeService _mindeeService;
    private readonly IOpenAiChatService _openAiChatService;
    private readonly ConcurrentDictionary<long, SessionData> _sessionStore;
    private readonly ILogger<StateHandlerService> _logger;

    public StateHandlerService(
        ITelegramBotClient bot,
        IMindeeService mindeeService,
        IOpenAiChatService openAiChatService,
        ConcurrentDictionary<long, SessionData> sessionStore,
        ILogger<StateHandlerService> logger)
    {
        _bot = bot;
        _mindeeService = mindeeService;
        _openAiChatService = openAiChatService;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>
    /// Main dispatcher: routes the update to the appropriate state handler.
    /// </summary>
    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        // Only process message updates
        if (update.Message == null)
            return;

        var chatId = update.Message.Chat.Id;

        // Get or create session
        var session = _sessionStore.AddOrUpdate(
            chatId,
            new SessionData { CreatedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.LastActivityAt = DateTime.UtcNow;
                return existing;
            });

        _logger.LogInformation(
            "Processing message for chat {ChatId} in state {State}",
            chatId, session.State);

        // Route based on current state
        switch (session.State)
        {
            case BotState.Start:
                await HandleStartAsync(chatId, update.Message, session, ct);
                break;

            case BotState.WaitingForPassportPhoto:
                await HandlePassportPhotoAsync(chatId, update.Message, session, ct);
                break;

            case BotState.WaitingForVehiclePhoto:
                await HandleVehiclePhotoAsync(chatId, update.Message, session, ct);
                break;

            case BotState.ConfirmingExtractedData:
                await HandleConfirmExtractedDataAsync(chatId, update.Message, session, ct);
                break;

            case BotState.PriceAgreement:
                await HandlePriceAgreementAsync(chatId, update.Message, session, ct);
                break;

            case BotState.PolicyIssuance:
                await HandlePolicyIssuanceAsync(chatId, update.Message, session, ct);
                break;

            default:
                _logger.LogWarning("Unknown state: {State}", session.State);
                break;
        }
    }

    /// <summary>
    /// Start state: generate greeting and prompt for passport photo using OpenAI.
    /// </summary>
    private async Task HandleStartAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        try
        {
            // Use OpenAI to generate a friendly greeting
            var systemContext =
                "You are a friendly car insurance bot assistant. " +
                "Welcome the user warmly and briefly explain that we need their passport " +
                "and vehicle photos to provide a quick insurance quote. Be concise and professional. " +
                "End with asking them to upload their passport photo first.";

            var userMessage = "Generate a greeting for a new user.";

            var greeting = await _openAiChatService.GenerateConversationalReplyAsync(
                systemContext,
                userMessage,
                ct);

            await _bot.SendTextMessageAsync(
                chatId,
                greeting,
                cancellationToken: ct);

            // ✅ UPDATE STATE (do not change this logic)
            session.State = BotState.WaitingForPassportPhoto;
            _sessionStore.TryUpdate(chatId, session, session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleStartAsync for chat {ChatId}", chatId);

            // Fallback message if OpenAI fails
            await _bot.SendTextMessageAsync(
                chatId,
                "👋 Welcome to the Car Insurance Bot! Please send a photo of your passport.",
                cancellationToken: ct);

            session.State = BotState.WaitingForPassportPhoto;
            _sessionStore.TryUpdate(chatId, session, session);
        }
    }

    /// <summary>
    /// Waiting for passport photo: download, extract via Mindee, store in session.
    /// </summary>
    private async Task HandlePassportPhotoAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        // Check if message contains a photo
        if (message.Photo == null || message.Photo.Length == 0)
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "❌ Please send a *photo* of your passport.",
                cancellationToken: ct);
            return;
        }

        try
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "📸 Processing your passport photo... Please wait.",
                cancellationToken: ct);

            // Get the highest resolution photo
            var photoFile = message.Photo.Last();

            // Download photo from Telegram — MUST use 'using' to dispose MemoryStream
            using (var fileStream = await DownloadPhotoAsync(photoFile.FileId, ct))
            {
                // Extract data via Mindee
                var extractedData = await _mindeeService.ExtractDocumentDataAsync(
                    fileStream,
                    "passport",
                    ct);

                // Store in session
                session.PassportPhotoFileId = photoFile.FileId;
                session.ExtractedPassportData = extractedData;
            } // ← MemoryStream is properly disposed here

            // ✅ UPDATE STATE (do not change this logic)
            session.State = BotState.WaitingForVehiclePhoto;
            _sessionStore.TryUpdate(chatId, session, session);

            // Prompt for vehicle photo
            var prompt =
                "✅ Passport data extracted successfully!\n\n" +
                session.ExtractedPassportData +
                "Now, please upload a photo of your *vehicle* (front/side view).";

            await _bot.SendTextMessageAsync(
                chatId,
                prompt,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing passport photo for chat {ChatId}", chatId);

            await _bot.SendTextMessageAsync(
                chatId,
                "❌ Failed to process passport photo. Please try again.",
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// Waiting for vehicle photo: download, extract via Mindee, store in session,
    /// then transition to confirmation state.
    /// </summary>
    private async Task HandleVehiclePhotoAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        // Check if message contains a photo
        if (message.Photo == null || message.Photo.Length == 0)
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "❌ Please send a *photo* of your vehicle.",
                cancellationToken: ct);
            return;
        }

        try
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "📸 Processing your vehicle photo... Please wait.",
                cancellationToken: ct);

            // Get the highest resolution photo
            var photoFile = message.Photo.Last();

            // Download photo from Telegram — MUST use 'using' to dispose MemoryStream
            using (var fileStream = await DownloadPhotoAsync(photoFile.FileId, ct))
            {
                // Extract data via Mindee
                var extractedData = await _mindeeService.ExtractDocumentDataAsync(
                    fileStream,
                    "vehicle",
                    ct);

                // Store in session
                session.VehiclePhotoFileId = photoFile.FileId;
                session.ExtractedVehicleData = extractedData;
            } // ← MemoryStream is properly disposed here

            // ✅ UPDATE STATE (do not change this logic)
            session.State = BotState.ConfirmingExtractedData;
            _sessionStore.TryUpdate(chatId, session, session);

            // Send combined extracted data for confirmation
            await SendConfirmationMessageAsync(chatId, session, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing vehicle photo for chat {ChatId}", chatId);

            await _bot.SendTextMessageAsync(
                chatId,
                "❌ Failed to process vehicle photo. Please try again.",
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// Send the combined extracted data and ask for confirmation.
    /// </summary>
    private async Task SendConfirmationMessageAsync(
        long chatId,
        SessionData session,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("✅ *Documents Processed Successfully!*\n");

        if (!string.IsNullOrWhiteSpace(session.ExtractedPassportData))
        {
            sb.AppendLine(session.ExtractedPassportData);
        }

        if (!string.IsNullOrWhiteSpace(session.ExtractedVehicleData))
        {
            sb.AppendLine(session.ExtractedVehicleData);
        }

        sb.AppendLine("\n❓ *Is this data correct?*");
        sb.AppendLine("Reply with *Yes* or *No*");

        await _bot.SendTextMessageAsync(
            chatId,
            sb.ToString(),
            cancellationToken: ct);
    }

    /// <summary>
    /// Confirming extracted data: user responds with Yes/No.
    /// </summary>
    private async Task HandleConfirmExtractedDataAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        var userResponse = message.Text?.Trim().ToLowerInvariant() ?? "";

        if (userResponse.Contains("yes") || userResponse.Contains("correct"))
        {
            // ✅ UPDATE STATE (do not change this logic)
            session.State = BotState.PriceAgreement;
            _sessionStore.TryUpdate(chatId, session, session);

            var priceMessage =
                "🎉 Great! We've prepared a quote for you.\n\n" +
                "💰 *Fixed Price: $100 USD*\n\n" +
                "This comprehensive car insurance policy includes:\n" +
                "✓ Collision coverage\n" +
                "✓ Liability protection\n" +
                "✓ Theft & vandalism\n" +
                "✓ Emergency roadside assistance\n\n" +
                "Do you accept this quote?\n" +
                "Reply with *Accept* or *Decline*";

            await _bot.SendTextMessageAsync(
                chatId,
                priceMessage,
                cancellationToken: ct);
        }
        else if (userResponse.Contains("no") || userResponse.Contains("incorrect") || userResponse.Contains("retry"))
        {
            // ✅ UPDATE STATE (do not change this logic)
            session.State = BotState.Start;
            _sessionStore.TryUpdate(chatId, session, session);

            var retryMessage =
                "🔄 Let's start over and try again.\n\n" +
                "Please upload a photo of your *passport*.";

            await _bot.SendTextMessageAsync(
                chatId,
                retryMessage,
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "❓ Please reply with *Yes* (data is correct) or *No* (retry).",
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// Price agreement state: user accepts or declines the $100 quote.
    /// If accepted, generate policy document using OpenAI.
    /// </summary>
    private async Task HandlePriceAgreementAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        var userResponse = message.Text?.Trim().ToLowerInvariant() ?? "";

        if (userResponse.Contains("accept") || userResponse.Contains("yes"))
        {
            try
            {
                // ✅ UPDATE STATE (do not change this logic)
                session.State = BotState.PolicyIssuance;
                _sessionStore.TryUpdate(chatId, session, session);

                // Generate policy document using OpenAI
                await _bot.SendTextMessageAsync(
                    chatId,
                    "🔄 Generating your policy document... Please wait.",
                    cancellationToken: ct);

                var policyDocument = await _openAiChatService.GeneratePolicyDocumentAsync(
                    session.ExtractedPassportData ?? "No passport data available",
                    session.ExtractedVehicleData ?? "No vehicle data available",
                    ct);

                // Send generated policy document
                var acceptance =
                    "🎊 *Your Policy Has Been Generated!*\n\n" +
                    policyDocument + "\n\n" +
                    "✉️ A confirmation email with your full policy document will be sent shortly.\n\n" +
                    "Thank you for choosing our insurance service! 🚗";

                await _bot.SendTextMessageAsync(
                    chatId,
                    acceptance,
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating policy for chat {ChatId}", chatId);

                // Fallback message if OpenAI fails
                var fallbackAcceptance =
                    "🎊 *Thank You!*\n\n" +
                    "Your car insurance policy has been successfully issued!\n\n" +
                    "📋 *Policy Details:*\n" +
                    "Policy Number: POL-2024-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper() + "\n" +
                    "Premium: $100 USD\n" +
                    "Coverage Period: 1 Year\n" +
                    "Effective Date: " + DateTime.Now.ToString("yyyy-MM-dd") + "\n\n" +
                    "✉️ A confirmation email with your full policy document will be sent shortly.\n\n" +
                    "Thank you for choosing our insurance service! 🚗";

                await _bot.SendTextMessageAsync(
                    chatId,
                    fallbackAcceptance,
                    cancellationToken: ct);
            }
        }
        else if (userResponse.Contains("decline") || userResponse.Contains("no"))
        {
            // User declined
            await _bot.SendTextMessageAsync(
                chatId,
                "❌ You have declined our offer. Thank you for your interest. If you change your mind, feel free to restart by typing /start.",
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendTextMessageAsync(
                chatId,
                "❓ Please reply with *Accept* (to confirm) or *Decline* (to reject).",
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// Policy issuance state: final state, inform user their policy is complete.
    /// </summary>
    private async Task HandlePolicyIssuanceAsync(
        long chatId,
        Message message,
        SessionData session,
        CancellationToken ct)
    {
        await _bot.SendTextMessageAsync(
            chatId,
            "Your policy is already issued! Check your email for the full policy document.",
            cancellationToken: ct);
    }

    /// <summary>
    /// Downloads a photo from Telegram and returns it as a MemoryStream.
    /// CALLER IS RESPONSIBLE FOR DISPOSING THE RETURNED STREAM!
    /// </summary>
    private async Task<MemoryStream> DownloadPhotoAsync(string fileId, CancellationToken ct)
    {
        try
        {
            // Get file info from Telegram
            var file = await _bot.GetFileAsync(fileId, ct);

            // Download the file into MemoryStream
            var memoryStream = new MemoryStream();
            await _bot.DownloadFileAsync(file.FilePath!, memoryStream, ct);

            // Reset position to beginning for reading
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file {FileId} from Telegram", fileId);
            throw;
        }
    }
}