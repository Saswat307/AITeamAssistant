namespace AITeamAssistant
{
    public interface IBotHost
    {
        Task StartAsync();

        Task StopAsync();
    }
}