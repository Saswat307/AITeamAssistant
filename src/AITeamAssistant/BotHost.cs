// ***********************************************************************
// Assembly         : EchoBot
// Author           : bcage29
// Created          : 10-27-2023
//
// Last Modified By : bcage29
// Last Modified On : 10-27-2023
// ***********************************************************************
// <copyright file="BotHost.cs" company="Microsoft">
//     Copyright ©  2023
// </copyright>
// <summary></summary>
// ***********************************************************************
using AdaptiveCards;
using AITeamAssistant.Bot;
using AITeamAssistant.Models;
using AITeamAssistant.Service;
using AITeamAssistant.Util;
using Azure.Identity;
using DotNetEnv.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Common.Telemetry;
using System.Net.Http.Headers;

namespace AITeamAssistant
{
    /// <summary>
    /// Bot Web Application
    /// </summary>
    public class BotHost : TeamsActivityHandler,IBotHost
    {
        private readonly ILogger<BotHost> _logger;
        private WebApplication? _app;
        private BotState _conversationState;
        /// <summary>
        /// Bot Host constructor
        /// </summary>
        /// <param name="logger"></param>
        public BotHost(ConversationState conversationState, ILogger<BotHost> logger)
        {
            _conversationState = conversationState;
            _logger = logger;
        }

        /// <summary>
        /// Starting the Bot and Web App
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            _logger.LogInformation("Starting the AI Team Assistant");
            // Set up the bot web application
            var builder = WebApplication.CreateBuilder();

            if (builder.Environment.IsDevelopment())
            {
                // load the .env file environment variables
                builder.Configuration.AddDotNetEnv();
            }

            // Add Environment Variables
            builder.Configuration.AddEnvironmentVariables(prefix: "AppSettings__");

            // Add services to the container.
            builder.Services.AddControllers();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var section = builder.Configuration.GetSection("AppSettings");
            var appSettings = section.Get<AppSettings>();

            builder.Services
                .AddOptions<AppSettings>()
                .BindConfiguration(nameof(AppSettings))
                .ValidateDataAnnotations()
                .ValidateOnStart();


            var promptFlowModel = builder.Configuration["PromptFlow:ModelName"];
            var promptFlowSecret = builder.Configuration["PromptFlow:Secret"];
            var promptFlowEndpoint = builder.Configuration["PromptFlow:Endpoint"];

            builder.Services.AddHttpClient("AzurePromptFlowClient", client =>
            {
                client.BaseAddress = new Uri(uriString: promptFlowEndpoint);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", promptFlowSecret);
                client.DefaultRequestHeaders.Add("azureml-model-deployment", promptFlowModel);
            });

            builder.Services.AddSingleton<GraphServiceClient>( (_) =>
            {
                var scopes = new[] { "User.Read" };

                // Multi-tenant apps can use "common",
                // single-tenant apps must use the tenant ID from the Azure portal
                var tenantId = "common";

                // Value from app registration
                var clientId = appSettings.AadAppId;

                // using Azure.Identity;
                var options = new DeviceCodeCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    ClientId = clientId,
                    TenantId = tenantId,
                    // Callback function that receives the user prompt
                    // Prompt contains the generated device code that user must
                    // enter during the auth process in the browser
                    DeviceCodeCallback = (code, cancellation) =>
                    {
                        Console.WriteLine(code.Message);
                        return Task.FromResult(0);
                    },
                };

                // https://learn.microsoft.com/dotnet/api/azure.identity.devicecodecredential
                var deviceCodeCredential = new DeviceCodeCredential(options);

                return new GraphServiceClient(deviceCodeCredential, scopes);
            });

            // Create the Bot Framework Authentication to be used with the Bot Adapter.
            builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

            builder.Services.AddSingleton<IPromptFlowService, PromptFlowService>();

            builder.Services.AddSingleton<IMeetingService, MeetingService>();

            // Create the Bot Adapter with error handling enabled.
            builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            builder.Services.AddTransient<IBot, TextBot>();

            builder.Services.AddSingleton<IGraphLogger, GraphLogger>(_ => new GraphLogger("EchoBotWorker", redirectToTrace: true));
            builder.Services.AddSingleton<IBotMediaLogger, BotMediaLogger>();
            builder.Logging.AddApplicationInsights();
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            builder.Logging.AddEventLog(config => config.SourceName = "Echo Bot Service");

            builder.Services.AddSingleton<IBotService, BotService>();
            builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
            
            // Bot Settings Setup
            var botInternalHostingProtocol = "https";
            if (appSettings.UseLocalDevSettings)
            {
                // if running locally with ngrok
                // the call signalling and notification will use the same internal and external ports
                // because you cannot receive requests on the same tunnel with different ports

                // calls come in over 443 (external) and route to the internally hosted port: BotCallingInternalPort
                botInternalHostingProtocol = "http";

                builder.Services.PostConfigure<AppSettings>(options =>
                {
                    options.BotInstanceExternalPort = 443;
                    options.BotInternalPort = appSettings.BotCallingInternalPort;

                });
            } else
            {
                //appSettings.MediaDnsName = appSettings.ServiceDnsName;
                builder.Services.PostConfigure<AppSettings>(options =>
                {
                    options.MediaDnsName = appSettings.ServiceDnsName;
                });
            }

            // localhost
            var baseDomain = "+";

            // http for local development
            // https for running on VM
            var callListeningUris = new HashSet<string>
            {
                $"{botInternalHostingProtocol}://{baseDomain}:{appSettings.BotCallingInternalPort}/",
                $"{botInternalHostingProtocol}://{baseDomain}:{appSettings.BotInternalPort}/"
            };

            builder.WebHost.UseUrls(callListeningUris.ToArray());

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.ConfigureHttpsDefaults(listenOptions =>
                {
                    listenOptions.ServerCertificate = Utilities.GetCertificateFromStore(appSettings.CertificateThumbprint);
                });
            });

            _app = builder.Build();

            using (var scope = _app.Services.CreateScope())
            {
                var bot = scope.ServiceProvider.GetRequiredService<IBotService>();
                bot.Initialize();
            }

            // Configure the HTTP request pipeline.
            if (_app.Environment.IsDevelopment())
            {
                // https://localhost:<port>/swagger
                _app.UseSwagger();
                _app.UseSwaggerUI();
            }

            _app.UseAuthorization();

            _app.MapControllers();

            await _app.RunAsync();
        }

        /// <summary>
        /// Stop the bot web application
        /// </summary>
        /// <returns></returns>
        public async Task StopAsync()
        {
            if (_app != null)
            {
                using (var scope = _app.Services.CreateScope())
                {
                    var bot = scope.ServiceProvider.GetRequiredService<IBotService>();
                    // terminate all calls and dispose of the call client
                    await bot.Shutdown();
                }

                // stop the bot web application
                await _app.StopAsync();
            }
        }

        /// <summary>
        /// Activity Handler for Meeting Participant join event
        /// </summary>
        /// <param name="meeting"></param>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task OnTeamsMeetingParticipantsJoinAsync(MeetingParticipantsEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Attachment(createAdaptiveCardInvokeResponseAsync(meeting.Members[0].User.Name, " has joined the meeting.")), cancellationToken);
            return;
        }

        /// <summary>
        /// Activity Handler for Meeting start event
        /// </summary>
        /// <param name="meeting"></param>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task OnTeamsMeetingStartAsync(MeetingStartEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            // Save any state changes that might have occurred during the turn.
            var conversationStateAccessors = _conversationState.CreateProperty<MeetingData>(nameof(MeetingData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new MeetingData());
            conversationData.StartTime = meeting.StartTime;
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Attachment(GetAdaptiveCardForMeetingStart(meeting)));
        }


        /// <summary>
        /// Sample Adaptive card for Meeting Start event.
        /// </summary>
        private Attachment GetAdaptiveCardForMeetingStart(MeetingStartEventDetails meeting)
        {
            AdaptiveCard card = new AdaptiveCard(new AdaptiveSchemaVersion("1.2"))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = meeting.Title  + "- started",
                        Weight = AdaptiveTextWeight.Bolder,
                        Spacing = AdaptiveSpacing.Medium,
                    },
                    new AdaptiveColumnSet
                    {
                        Columns = new List<AdaptiveColumn>
                        {
                            new AdaptiveColumn
                            {
                                Width = AdaptiveColumnWidth.Auto,
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock
                                    {
                                        Text = "Start Time : ",
                                        Wrap = true,
                                    },
                                },
                            },
                            new AdaptiveColumn
                            {
                                Width = AdaptiveColumnWidth.Auto,
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock
                                    {
                                        Text = Convert.ToString(meeting.StartTime.ToLocalTime()),
                                        Wrap = true,
                                    },
                                },
                            },
                        },
                    },
                },
                Actions = new List<AdaptiveAction>
                {
                    new AdaptiveOpenUrlAction
                    {
                        Title = "Join meeting",
                        Url = meeting.JoinUrl,
                    },
                },
            };

            return new Attachment()
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card,
            };
        }

        /// <summary>
        /// Activity Handler for Meeting end event.
        /// </summary>
        /// <param name="meeting"></param>
        /// <param name="turnContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task OnTeamsMeetingEndAsync(MeetingEndEventDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            var conversationStateAccessors = _conversationState.CreateProperty<MeetingData>(nameof(MeetingData));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new MeetingData());
            await turnContext.SendActivityAsync(MessageFactory.Attachment(GetAdaptiveCardForMeetingEnd(meeting, conversationData)));
        }

        /// <summary>
        /// Sample Adaptive card for Meeting participant events.
        /// </summary>
        private Attachment createAdaptiveCardInvokeResponseAsync(string userName, string action)
        {
            AdaptiveCard card = new(new AdaptiveSchemaVersion("1.4"))
            {
                Body = new List<AdaptiveElement>
                {
                    new AdaptiveRichTextBlock
                    {
                        Inlines = new List<AdaptiveInline>
                        {
                            new AdaptiveTextRun
                            {
                                Text = userName,
                                Weight = AdaptiveTextWeight.Bolder,
                                Size = AdaptiveTextSize.Default,
                            },
                            new AdaptiveTextRun
                            {
                                Text = action,
                                Weight = AdaptiveTextWeight.Default,
                                Size = AdaptiveTextSize.Default,
                            }
                        },
                    Spacing = AdaptiveSpacing.Medium,
                    }
                }
            };

            return new Attachment()
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card,
            };
        }

    /// <summary>
    /// Sample Adaptive card for Meeting End event.
    /// </summary>
    private Attachment GetAdaptiveCardForMeetingEnd(MeetingEndEventDetails meeting, MeetingData conversationData)
    {

        TimeSpan meetingDuration = meeting.EndTime - conversationData.StartTime;
        var meetingDurationText = meetingDuration.Minutes < 1 ?
              Convert.ToInt32(meetingDuration.Seconds) + "s"
            : Convert.ToInt32(meetingDuration.Minutes) + "min " + Convert.ToInt32(meetingDuration.Seconds) + "s";

        AdaptiveCard card = new AdaptiveCard(new AdaptiveSchemaVersion("1.2"))
        {
            Body = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = meeting.Title  + "- ended",
                        Weight = AdaptiveTextWeight.Bolder,
                        Spacing = AdaptiveSpacing.Medium,
                    },
                     new AdaptiveColumnSet
                    {
                        Columns = new List<AdaptiveColumn>
                        {
                            new AdaptiveColumn
                            {
                                Width = AdaptiveColumnWidth.Auto,
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock
                                    {
                                        Text = "End Time : ",
                                        Wrap = true,
                                    },
                                    new AdaptiveTextBlock
                                    {
                                        Text = "Total duration : ",
                                        Wrap = true,
                                    },
                                },
                            },
                            new AdaptiveColumn
                            {
                                Width = AdaptiveColumnWidth.Auto,
                                Items = new List<AdaptiveElement>
                                {
                                    new AdaptiveTextBlock
                                    {
                                        Text = Convert.ToString(meeting.EndTime.ToLocalTime()),
                                        Wrap = true,
                                    },
                                    new AdaptiveTextBlock
                                    {
                                        Text = meetingDurationText,
                                        Wrap = true,
                                    },
                                },
                            },
                        },
                    },
                }
        };

        return new Attachment()
        {
            ContentType = AdaptiveCard.ContentType,
            Content = card,
        };
    }
    }
}
