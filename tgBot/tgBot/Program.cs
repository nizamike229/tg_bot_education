using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using tgBot;
using Telegram.Bot;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        BotBusinessLogic.ConfigureServices(services);
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("7412325055:AAEmva3flztRdn0iXKkSg__qJGEpnjoiC98"));
        services.AddHostedService<TelegramBotHostedService>();
        services.AddLogging();
    });

await builder.RunConsoleAsync();
