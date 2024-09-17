using Microsoft.Graph.Models;

namespace AITeamAssistant.Bot
{
    public interface IMeetingService
    {
        public Task<OnlineMeeting?> GetMeetingInfoAsync(string meetingId);
    }
}
