using AITeamAssistant;
using AITeamAssistant.Bot;
using AITeamAssistant.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using System.Net.Http.Headers;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Echo Bot Service";
    })
    .ConfigureServices((builder, services) =>
    {
    LoggerProviderOptions.RegisterProviderOptions<
        EventLogSettings, EventLogLoggerProvider>(services);

        // Creating the storage.
        var storage = new MemoryStorage();

        // Create the Conversation state passing in the storage layer.
        var conversationState = new ConversationState(storage);
        services.AddSingleton(conversationState);

        services.AddSingleton<IBotHost, BotHost>();

        services.AddHostedService<EchoBotWorker>();
    })
    .Build();

await host.RunAsync();
