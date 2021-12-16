using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// https://thecodeblogger.com/2021/05/04/how-to-use-appsettings-json-config-file-with-net-console-applications/
namespace WorkstationCommander.TelegramBot
{
    public static class SetupConfiguration
    {
        public static string botKey = string.Empty;

        public static void Setup()
        {
            var host = CreateDefaultBuilder().Build();

            // Invoke Worker
            using IServiceScope serviceScope = host.Services.CreateScope();
            IServiceProvider provider = serviceScope.ServiceProvider;
            var workerInstance = provider.GetRequiredService<Worker>();
            botKey = workerInstance.GetBotKey();
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

            public string GetBotKey()
            {
                return configuration["TelegramBotKey"];
            }
        }
    }
}
