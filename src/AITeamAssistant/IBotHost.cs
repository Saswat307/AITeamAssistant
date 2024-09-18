using Microsoft.Graph.Communications.Client;
using Microsoft.Graph;
using AITeamAssistant.Client;

namespace AITeamAssistant
{
    public interface IBotHost
    {
        Task StartAsync(CallClient callClient);

        Task StopAsync();
    }
}