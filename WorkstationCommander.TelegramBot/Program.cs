using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WorkstationCommander.TelegramBot;
using WorkstationCommander.TelegramBot.Properties;

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

string? botChatId = null;
TelegramBotClient? botClient = null;
using var cts = new CancellationTokenSource();

WinSys.LockEventSetup();
WinSys.LockMessageFunc = LockMessageFuncAsync;

// List of BotCommands
var commands = new List<BotCommand>() {
    new BotCommand { Command = "help", Description = "View summary of what the bot can do" },
    new BotCommand { Command = "lock", Description = "Lock the workstation" },
    new BotCommand { Command = "status", Description = "View the workstation status" },
    new BotCommand { Command = "version", Description = "View current bot version" }
};

// Get this once at startup
var assemblyVersion = WinSys.GetAssemblyVersion();

// Setup the configuration settings file
SetupConfiguration.Setup();

// Get the Bot key
var botKey = SetupConfiguration.botKey;

// Try and get a ChatId
botChatId = SetupConfiguration.botChatId;

botClient = new TelegramBotClient(botKey);

// Inject the bot with these command options
await botClient.SetMyCommands(commands, null, null, cts.Token);

// Set up message handler using the new event-based approach
botClient.OnMessage += async (message, type) =>
{
    // Only process text messages
    if (message.Type != MessageType.Text)
        return;

    var chatId = message.Chat.Id;
    var messageText = message.Text;

    if (string.IsNullOrEmpty(botChatId))
    {
        // Store it in appSettings
        botChatId = chatId.ToString();
        SetupConfiguration.AddOrUpdateAppSetting("TelegramBotChatId", botChatId);
        Console.WriteLine($"Persisting ChatId: {botChatId} to appsettings.json");
    }

    Console.WriteLine($"Received: '{messageText}' message in chat {chatId}.");

    var messageResponse = string.Empty;

    switch (messageText.ToLower().Split(' ').First())
    {
        case "/help":
            {
                messageResponse = Resources.Help;
                break;
            }

        case "/lock":
            {
                WinSys.LockWorkStation();
                messageResponse = string.Format(Resources.Locking, Environment.MachineName);
                break;
            }

        case "/status":
            {
                messageResponse = string.Format(Resources.Status, Environment.MachineName, WinSys.GetLocalIpAddress(), WinSys.GetPublicIpAddress(), WinSys.GetSystemUpTimeInfo(), WinSys.GetIdleTime(), WinSys.lockState ? "Locked" : "Unlocked");
                break;
            }

        case "/version":
            {
                messageResponse = string.Format(Resources.Version, assemblyVersion);
                break;
            }

        default:
            {
                break;
            }
    }

    if (messageResponse.Length > 0)
    {
        await botClient.SendMessage(chatId: chatId, text: messageResponse, cancellationToken: cts.Token);
        Console.WriteLine($"Sent Message: {messageResponse}");
    }
};

// Set up error handler
botClient.OnError += async (exception, source) =>
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine($"HandleErrorAsync() Error: {ErrorMessage}");
    await Task.CompletedTask;
};

var me = await botClient.GetMe();

Console.WriteLine($"Start listening for @{me.Username}");

// If we have this ID, we can send notifications. Send a welcome message.
if (!string.IsNullOrEmpty(botChatId))
{
    var welcomeMessage = await SendMessage(botChatId, $"Connected Workstation.Commander to {me.Username}", cancellationToken: cts.Token);
}

// This keeps it running
new ManualResetEvent(false).WaitOne();

// Send cancellation request to stop bot
cts.Cancel();

// Callback function invoked by WinSys when the workstation is locked or unlocked
async Task<bool> LockMessageFuncAsync(bool lockState)
{
    if (string.IsNullOrEmpty(botChatId))
        return false;

    _ = await SendMessage(botChatId, lockState ? string.Format(Resources.Locked, Environment.MachineName) : string.Format(Resources.Unlocked, Environment.MachineName), cancellationToken: cts.Token);

    return true;
}

async Task<Message> SendMessage(ChatId chatId, string text, CancellationToken cancellationToken)
{
    return await botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
}

#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.