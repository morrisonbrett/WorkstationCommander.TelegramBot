using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// https://thecodeblogger.com/2021/05/04/how-to-use-appsettings-json-config-file-with-net-console-applications/
namespace WorkstationCommander.TelegramBot
{
    public static class SetupConfiguration
    {
        public static string botKey = string.Empty;
        public static string botChatId = string.Empty;

        public static void Setup()
        {
            var host = CreateDefaultBuilder().Build();

            // Invoke Worker
            using IServiceScope serviceScope = host.Services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;
            var workerInstance = provider.GetRequiredService<Worker>();
            botKey = workerInstance.BotKey;
            botChatId = workerInstance.BotChatId;
        }

        private static IHostBuilder CreateDefaultBuilder()
        {
            return Host.CreateDefaultBuilder().ConfigureAppConfiguration(app => {
                    app.AddJsonFile("appsettings.json");
                }).ConfigureServices(services => {
                    services.AddSingleton<Worker>();
                });
        }

        internal class Worker
        {
            private readonly IConfiguration configuration;

            public Worker(IConfiguration configuration)
            {
                this.configuration = configuration;
            }

            public string BotKey { get { return configuration["TelegramBotKey"]; } }
            public string BotChatId { get { return configuration["TelegramBotChatId"]; } }
        }

        // https://stackoverflow.com/a/60832823/3782147
        public static void AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                SetValueRecursively(sectionPathKey, jsonObj, value);

                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, output);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings | {0}", ex.Message);
            }
        }

        private static void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            // split the string at the first ':' character
            var remainingSections = sectionPathKey.Split(":", 2);

            var currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                // continue with the procress, moving down the tree
                var nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                // we've got to the end of the tree, set the value
                jsonObj[currentSection] = value;
            }
        }
    }
}
