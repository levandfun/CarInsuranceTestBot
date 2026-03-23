using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarInsuranceTestBot.Models;
/// <summary>
/// Holds the full mutable session for a single Telegram chat.
/// One instance lives in the ConcurrentDictionary per chatId.
/// </summary>
public class SessionData
{
    /// <summary>Current state in the conversation state machine.</summary>
    public BotState State { get; set; } = BotState.Start;

    /// <summary>Telegram file_id of the passport photo, set after upload.</summary>
    public string? PassportPhotoFileId { get; set; }

    /// <summary>Telegram file_id of the vehicle photo, set after upload.</summary>
    public string? VehiclePhotoFileId { get; set; }

    /// <summary>
    /// Raw text extracted from the passport photo.
    /// Populated by Mindee — left null until TODO is implemented.
    /// </summary>
    public string? ExtractedPassportData { get; set; }

    /// <summary>
    /// Raw text extracted from the vehicle photo.
    /// Populated by Mindee  — left null until TODO is implemented.
    /// </summary>
    public string? ExtractedVehicleData { get; set; }

    /// <summary>Timestamp of session creation — useful for TTL eviction.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp of last activity — update on every message.</summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}