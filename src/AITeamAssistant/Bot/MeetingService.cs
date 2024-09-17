using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AITeamAssistant.Bot
{
    public class MeetingService : IMeetingService
    {
        private readonly GraphServiceClient _graphClient;

        public MeetingService(GraphServiceClient graphClient)
        {
            _graphClient = graphClient;
        }

        public async Task<OnlineMeeting?> GetMeetingInfoAsync(string meetingId)
        {
            try
            {
                // Retrieve the online meeting details using the meeting ID
                var onlineMeeting = await _graphClient.Me.OnlineMeetings[meetingId]
                    .GetAsync();
                return onlineMeeting;
            }
            catch (ServiceException ex)
            {
                // Handle errors (e.g., meeting not found)
                Console.WriteLine($"Error retrieving meeting info: {ex.Message}");
                throw;
            }
        }
    }
}
