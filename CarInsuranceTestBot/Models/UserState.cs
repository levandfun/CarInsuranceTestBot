namespace CarInsuranceTestBot.Models;

/// <summary>
/// Represents all possible states in the insurance bot conversation flow.
/// Each state drives which handler is invoked for the next user message.
/// </summary>
public enum BotState
{
    /// <summary>Initial state — bot sends greeting and prompts for passport photo.</summary>
    Start,

    /// <summary>Bot is waiting for the user to send a photo of their passport.</summary>
    WaitingForPassportPhoto,

    /// <summary>Bot is waiting for the user to send a photo of their vehicle.</summary>
    WaitingForVehiclePhoto,

    /// <summary>
    /// Extracted data (from Mindee) has been shown to the user.
    /// Waiting for confirmation ("Yes" / "No, retry").
    /// </summary>
    ConfirmingExtractedData,

    /// <summary>
    /// User has been presented with the fixed price of $100 USD.
    /// Waiting for acceptance ("Accept" / "Decline").
    /// </summary>
    PriceAgreement,

    /// <summary>Final state — policy is being issued and the flow is complete.</summary>
    PolicyIssuance
}