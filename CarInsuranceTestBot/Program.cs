using CarInsuranceTestBot;
using CarInsuranceTestBot.Models;
using CarInsuranceTestBot.Services;
using System.Collections.Concurrent;
using Telegram.Bot;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var token = ctx.Configuration["TelegramBot:Token"]
            ?? throw new InvalidOperationException(
                "TelegramBot:Token is missing from configuration.");

        // ── Telegram client ──────────────────────────────────────────────────
        services.AddSingleton<ITelegramBotClient>(
            new TelegramBotClient(token));

        // ── Session store — singleton so Worker and StateHandlerService share it
        services.AddSingleton<ConcurrentDictionary<long, SessionData>>();

        // ── OpenAI Chat Service — for conversational AI and policy generation ──
        services.AddSingleton<IOpenAiChatService, OpenAiChatService>();

        // ── OCR Service (Mindee or your OCR implementation) ────────────────────
        services.AddSingleton<IMindeeService, MindeeService>();

        // ── State machine dispatcher ─────────────────────────────────────────
        services.AddSingleton<IStateHandlerService, StateHandlerService>();

        // ── Worker ───────────────────────────────────────────────────────────
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();