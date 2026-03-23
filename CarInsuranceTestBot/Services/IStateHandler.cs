using Telegram.Bot.Types;

namespace CarInsuranceTestBot.Services;

/// <summary>
/// Contract for the service that dispatches an incoming Telegram Update
/// to the correct state handler based on the session's current BotState.
/// </summary>
public interface IStateHandlerService
{
    Task HandleUpdateAsync(Update update, CancellationToken ct);
}