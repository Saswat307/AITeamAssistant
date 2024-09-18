using AITeamAssistant;
using AITeamAssistant.Authentication;
using AITeamAssistant.Bot;
using AITeamAssistant.Client;
using AITeamAssistant.Constants;
using AITeamAssistant.Service;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Calls;
using Microsoft.Graph.Communications.Client;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Graph.Communications.Resources;
using Microsoft.Skype.Bots.Media;
using System.Net;
using System.Net.Http.Headers;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "Maya AI Service";
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

        services.AddOptions<AppSettings>()
                .BindConfiguration(nameof(AppSettings));

        var section = builder.Configuration.GetSection("AppSettings");
        var appSettings = section.Get<AppSettings>();
        var name = "AITeamAssistant";

        var promptFlowModel = builder.Configuration["PromptFlow:ModelName"];
        var promptFlowSecret = builder.Configuration["PromptFlow:Secret"];
        var promptFlowEndpoint = builder.Configuration["PromptFlow:Endpoint"];

        services.AddHttpClient("AzurePromptFlowClient", client =>
        {
            client.BaseAddress = new Uri(uriString: promptFlowEndpoint);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", promptFlowSecret);
            client.DefaultRequestHeaders.Add("azureml-model-deployment", promptFlowModel);
        });

        services.AddSingleton<IBotMediaLogger, BotMediaLogger>();
        services.AddSingleton<IGraphLogger, GraphLogger>(_ => new GraphLogger(name, redirectToTrace: true));
        services.AddSingleton<IPromptFlowService, PromptFlowService>();
        services.AddSingleton<IOpenAIService, OpenAIService>();
        services.AddSingleton<IBotHost, BotHost>();
        services.AddSingleton<IBotService, BotService>();

        services.AddSingleton<ICommunicationsClient>((buil) =>
         {
         var _graphLogger = buil.GetRequiredService<IGraphLogger>();
         var _mediaPlatformLogger = buil.GetRequiredService<IBotMediaLogger>();

         var communicationBuilder = new CommunicationsClientBuilder(
             name,
             appSettings.AadAppId,
             _graphLogger);

         var authProvider = new AuthenticationProvider(
             name,
             appSettings.AadAppId,
             appSettings.AadAppSecret,
             _graphLogger
             );

         var mediaPlatformSettings = new MediaPlatformSettings()
         {
             MediaPlatformInstanceSettings = new MediaPlatformInstanceSettings()
             {
                 CertificateThumbprint = appSettings.CertificateThumbprint,
                 InstanceInternalPort = appSettings.MediaInternalPort,
                 InstancePublicIPAddress = IPAddress.Any,
                 InstancePublicPort = appSettings.MediaInstanceExternalPort,
                 ServiceFqdn = appSettings.MediaDnsName
             },
             ApplicationId = appSettings.AadAppId,
             MediaPlatformLogger = _mediaPlatformLogger
         };

         var notificationUrl = new Uri($"https://{appSettings.ServiceDnsName}:{appSettings.BotInstanceExternalPort}/{HttpRouteConstants.CallSignalingRoutePrefix}/{HttpRouteConstants.OnNotificationRequestRoute}");
         communicationBuilder.SetAuthenticationProvider(authProvider);
         communicationBuilder.SetNotificationUrl(notificationUrl);
         communicationBuilder.SetServiceBaseUrl(new Uri(AppConstants.PlaceCallEndpointUrl));

         var client = communicationBuilder.Build();
             return client;
         });

        services.AddSingleton<CallClient>();
        services.AddSingleton<GraphServiceClient>((_) =>
        {
            var scopes = new[] { "https://graph.microsoft.com/.default", "User.Read" };

            // Values from app registration
            var clientId = appSettings.AadAppId;
            var tenantId = appSettings.TenantId;
            var clientSecret = appSettings.AadAppSecret;

            // using Azure.Identity;
            var options = new ClientSecretCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            };


            TokenCredential clientCertCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            return new GraphServiceClient(clientCertCredential, scopes);
        });

        services.AddHostedService<EchoBotWorker>();
        services.AddHostedService<StartMeetingBS>();
    })
    .Build();

CollectionEventHandler<ICallCollection, ICall> OnIncomingCall()
{
    throw new NotImplementedException();
}

await host.RunAsync();
