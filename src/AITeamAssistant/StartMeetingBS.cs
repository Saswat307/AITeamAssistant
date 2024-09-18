using AITeamAssistant.Bot;
using AITeamAssistant.Client;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Client;

namespace AITeamAssistant
{
    public class StartMeetingBS : BackgroundService
    {
        GraphServiceClient graphServiceClient;
        CallClient callClient;
        ICommunicationsClient communicationsClient;
        ILogger<StartMeetingBS> _logger;
        IBotService botService;

        public StartMeetingBS(CallClient callClient,ICommunicationsClient communicationsClient,IBotService botService, GraphServiceClient graphServiceClient, ILogger<StartMeetingBS> logger)
        {
            _logger = logger;
            this.communicationsClient = communicationsClient;
            this.callClient = callClient;
            this.botService = botService;
            this.graphServiceClient = graphServiceClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Echo Bot running at: {time}", DateTimeOffset.Now);
            /* await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
             try
             {
                 await JoinMeeting();
             }catch(Exception ex)
             {
                 _logger.LogError(ex.Message);
             }*/
            await Task.CompletedTask;
        }

        private async Task JoinMeeting()
        {
            var meetingDetails = new Models.JoinCallBody()
            {
                JoinUrl = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_MDVlY2EyNmItMjA4OS00OTM5LTkzY2UtYTgxMWZlODBhMTFj%40thread.v2/0?context=%7b%22Tid%22%3a%22e89eec77-91df-4a70-91bb-5845d24e20d3%22%2c%22Oid%22%3a%223986f9c8-161c-4493-9bb8-15f0dbc44cc5%22%7d",
                DisplayName = "Maya AI"
            };
            await callClient.JoinCall(meetingDetails);
            //await communicationsClient.Calls().AddAsync(joinParams, scenarioId).ConfigureAwait(false);
            /* await botService.JoinCallAsync();*/
        }

      /*  private async Task CheckMeetingsAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Checking the meetings which bot needs to join...");

                IGraphClient graphClient = communicationsClient.GraphClient;
                IGraphRequest request = new GraphRequest(new Uri("https://graph.microsoft.com/v1.0/communications/onlineMeetings/424764638759"), RequestType.Get);

                var meetings = await graphServiceClient.
                Console.WriteLine(meetings);

                // IGraphResponse res = await graphClient.SendAsync(request, stoppingToken);
                // MeetingId: 424 764 638 759
                //var meetingCount = await graphServiceClient.Me.OnlineMeetings.Count.GetAsync(cancellationToken: stoppingToken);
                // Uncomment and implement the meeting checking logic here
                *//* var graphClient = GetGraphClient();

                 var events = await graphClient.Me.Events
                     .Request()
                     .Filter("start/dateTime ge '" + DateTime.UtcNow.ToString("o") + "'")
                     .GetAsync();

                 foreach (var meeting in events.CurrentPage)
                 {
                     if (meeting.Start.DateTime <= DateTime.UtcNow.AddMinutes(5) && meeting.Status == "NotStarted")
                     {
                         // Logic to join the meeting
                         await JoinMeetingAsync(meeting);
                     }
                 }*//*

                // Simulate work
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking meetings: {Message}", ex.Message);
            }
        }*/
    }
}
