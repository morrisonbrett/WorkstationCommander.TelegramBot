using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
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

// StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};

// Inject the bot with these command options
var commandStatus = botClient.SetMyCommandsAsync(commands, null, null, cts.Token);

botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken: cts.Token);

var me = await botClient.GetMeAsync();

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
    return await botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
}

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    // Only process Message updates: https://core.telegram.org/bots/api#message
    if (update.Type != UpdateType.Message)
        return;

    // Only process text messages
    if (update.Message!.Type != MessageType.Text)
        return;

    var chatId = update.Message.Chat.Id;
    var messageText = update.Message.Text;

    if (string.IsNullOrEmpty(botChatId))
    {
        // Store it in appSettings
        botChatId = chatId.ToString();
        SetupConfiguration.AddOrUpdateAppSetting("TelegramBotChatId", botChatId);
        Console.WriteLine($"Persisting ChatId: {botChatId} to appsettings.json");
    }

    Console.WriteLine($"Type: {update.Type} Received: '{messageText}' message in bot {chatId}.");

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
            // TODO Properly implement version updating on build. Also, check against version on GitHub to determine if current version is outdated.
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
        var sentMessage = await SendMessage(chatId: chatId, text: messageResponse, cancellationToken: cancellationToken);
        Console.WriteLine($"Sent Message: {messageResponse}");
    }
}

Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}", _ => exception.ToString()
    };

    Console.WriteLine($"HandleErrorAsync() Error: {ErrorMessage}");

    return Task.CompletedTask;
}

#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
