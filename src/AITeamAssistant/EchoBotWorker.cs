using AITeamAssistant.Client;
using Microsoft.Graph.Communications.Client;

namespace AITeamAssistant
{
    public class EchoBotWorker : BackgroundService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly ILogger<EchoBotWorker> _logger;
        private readonly IBotHost _botHost;
        private readonly CallClient callClient;
        public EchoBotWorker(CallClient callClient,IHostApplicationLifetime hostApplicationLifetime, ILogger<EchoBotWorker> logger, IBotHost botHost)
        {
            this.callClient = callClient;
            _hostApplicationLifetime = hostApplicationLifetime;
            _logger = logger;
            _botHost = botHost;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting Echo Bot at: {time}", DateTimeOffset.Now);
                // Start the bot host in a separate task to avoid blocking
                var botHostTask = Task.Run(() => _botHost.StartAsync(callClient), stoppingToken);

                // Check if the bot started successfully
                await botHostTask;
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Task was canceled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                Environment.Exit(1);
            }
            finally
            {
                _logger.LogInformation("Stopping Echo Bot at: {time}", DateTimeOffset.Now);
                await _botHost.StopAsync();
                _hostApplicationLifetime.StopApplication();
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Echo Bot gracefully...");
            try
            {
                await _botHost.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping bot: {Message}", ex.Message);
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
