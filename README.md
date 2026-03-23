# 🚗 Car Insurance Telegram Bot

A robust Telegram bot built with **.NET 8** that automates the car insurance quotation and policy issuance process. The bot extracts data from user-uploaded documents and generates a dummy policy using AI.

## 🏗️ Architecture & Patterns
- **State Machine Pattern:** Used to strictly control the conversational flow (Start ➔ Passport ➔ Vehicle ➔ Price ➔ Policy). This ensures predictable user journeys and state persistence.
- **Adapter Pattern:** Implemented for the OCR service. Abstracted the document extraction logic into `IMindeeService`, currently powered by **OpenAI Vision API** for high-accuracy data extraction from images.
- **Dependency Injection:** Fully decoupled services (`IStateHandlerService`, `IOpenAiChatService`, `IMindeeService`) for maintainability and testability.

## ✨ Features
1. **Document OCR:** Users upload photos of their Passport and Vehicle Registration. The bot uses OpenAI Vision (`gpt-4o-mini`) to extract structured data (Names, VIN, Dates).
2. **Dynamic AI Conversations:** Replaced static, hardcoded responses with OpenAI Chat Completions for natural, context-aware user interactions.
3. **Automated Policy Generation:** Upon price agreement, the bot generates a professional Markdown-formatted dummy insurance policy containing the extracted user and vehicle data.

## 🚀 How to Run Locally

1. Clone the repository.
2. Create a copy of `appsettings.example.json` and rename it to `appsettings.json`.
3. Add your API keys:
   - `TelegramBot:Token` - Get it from [@BotFather](https://t.me/BotFather)
   - `OpenAI:ApiKey` - Get it from [OpenAI Developer Platform](https://platform.openai.com/)
4. Run the project (F5 in Visual Studio or `dotnet run` via CLI).
5. Send `/start` to your bot in Telegram!

## 🛠️ Tech Stack
- C# / .NET 8 (Worker Service)
- Telegram.Bot API
- OpenAI API (Vision & Chat Completions)
- ConcurrentDictionary (In-memory session storage)
