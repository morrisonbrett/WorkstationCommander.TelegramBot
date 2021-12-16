using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WorkstationCommander.TelegramBot;
using WorkstationCommander.TelegramBot.Properties;

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.

WinSys.LockEventSetup();

// List of BotCommands
var commands = new List<BotCommand>() {
    new BotCommand { Command = "help", Description = "View summary of what the bot can do" },
    new BotCommand { Command = "lock", Description = "Locks the workstation" },
    new BotCommand { Command = "status", Description = "View the workstation status" },
    new BotCommand { Command = "version", Description = "View current bot version" }
};

// Get this once at startup
var assemblyVersion = GetAssemblyVersion();

// Setup the configuration settings file
SetupConfiguration.Setup();

// Get the Bot key
var botKey = SetupConfiguration.botKey;

var botClient = new TelegramBotClient(botKey);

using var cts = new CancellationTokenSource();

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

// TODO Send a welcome message
// var welcomeMessage = await botClient.SendTextMessageAsync(chatId: botClient., text: $"{me.FirstName} connected", cancellationToken: cts.Token); 

Console.ReadLine();

// Send cancellation request to stop bot
cts.Cancel();

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

    Console.WriteLine($"Type: {update.Type} Received: '{messageText}' message in bot {chatId}.");

    var messageResponse = string.Empty;

    switch (messageText.ToLower())
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
            messageResponse = string.Format(Resources.Status, Environment.MachineName, GetLocalIpAddress(), GetPublicIpAddress(), WinSys.GetSystemUpTimeInfo(), WinSys.GetIdleTime(), WinSys.lockState ? "Locked" : "Unlocked");
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
        var sentMessage = await botClient.SendTextMessageAsync(chatId: chatId, text: messageResponse, cancellationToken: cancellationToken);
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

string GetAssemblyVersion()
{
    return Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version.ToString();
}

// https://stackoverflow.com/a/27376368/3782147
string GetLocalIpAddress()
{
    var localIP = string.Empty;
    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
    {
        socket.Connect("8.8.8.8", 65530);
        var endPoint = socket.LocalEndPoint as IPEndPoint;
        localIP = endPoint.Address.ToString();
    }
    return localIP;
}

string GetPublicIpAddress(string serviceUrl = "https://ipinfo.io/ip")
{
    return IPAddress.Parse(new WebClient().DownloadString(serviceUrl)).ToString();
}

#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8604 // Possible null reference argument.
