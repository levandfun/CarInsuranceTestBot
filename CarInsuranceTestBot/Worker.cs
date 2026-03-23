using CarInsuranceTestBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


namespace CarInsuranceTestBot;

/// <summary>
/// The hosted Worker Service. Starts long-polling in ExecuteAsync,
/// delegates all update handling to IStateHandlerService.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IStateHandlerService _stateHandler;
    private readonly ILogger<Worker> _logger;

    public Worker(
        ITelegramBotClient bot,
        IStateHandlerService stateHandler,
        ILogger<Worker> logger)
    {
        _bot = bot;
        _stateHandler = stateHandler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _bot.GetMeAsync(stoppingToken);
        _logger.LogInformation("Bot started: @{Username} (id={Id})", me.Username, me.Id);

        var receiverOptions = new ReceiverOptions
        {
            // Only process Message updates; extend as needed (CallbackQuery, etc.)
            AllowedUpdates = [UpdateType.Message],

            // Drop pending updates that accumulated while the bot was offline
            ThrowPendingUpdates = true
        };

        // StartReceiving is non-blocking and internally manages the polling loop.
        // It respects the cancellation token — stops cleanly on host shutdown.
        await _bot.ReceiveAsync(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            receiverOptions,
            stoppingToken);

        // Keep ExecuteAsync alive until the host requests cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            await _stateHandler.HandleUpdateAsync(update, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception while processing update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(
        ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        // Log the polling-level error (network issues, Telegram API errors, etc.)
        // The library will automatically retry after a delay
        _logger.LogError(exception, "Telegram polling error");
        return Task.CompletedTask;
    }
}