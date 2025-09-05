using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pinger
{
    public class PingerFunction
    {
        private readonly ILogger _logger;

        private readonly Dictionary<string, int[]> _endpointsToCheck = new()
        {
            { "google.com", new[] {80, 443} }
        };

        public PingerFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PingerFunction>();
        }

        [Function("PingerFunction")]
        public async Task Run([TimerTrigger("*/30 * * * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            await CheckAllEndpoints();

            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
            }
        }

        private async Task CheckAllEndpoints()
        {
            var tasks = new List<Task>();

            foreach (var endpoint in _endpointsToCheck)
            {
                foreach (var port in endpoint.Value)
                {
                    tasks.Add(CheckPortAsync(endpoint.Key, port));
                }

                tasks.Add(PingHostAsync(endpoint.Key));
            }

            await Task.WhenAll(tasks);
        }

        private async Task<bool> CheckPortAsync(string hostname, int port)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(hostname, port);
                var timeoutTask = Task.Delay(5000);

                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning($"Timeout connecting to {hostname}:{port}");
                    return false;
                }

                if (connectTask.IsFaulted)
                {
                    _logger.LogError($"Failed to connect to {hostname}:{port} - {connectTask.Exception?.GetBaseException().Message}");
                    return false;
                }

                _logger.LogInformation($"{hostname}:{port} is reachable");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking {hostname}:{port} - {ex.Message}");
                return false;
            }
        }
        
        private async Task<bool> PingHostAsync(string hostname)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(hostname, 5000);

                if (reply.Status == IPStatus.Success)
                {
                    _logger.LogInformation($"{hostname} ping successful ({reply.RoundtripTime}ms)");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Ping to {hostname} failed: {reply.Status}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error pinging {hostname} - {ex.Message}");
                return false;
            }
        }

        private async Task CheckEndpointStatusAsync(string endpoint)
        {
            if (!await PingHostAsync(endpoint))
            {
                await SendTeamsNotificationAsync(endpoint, "Ping failed");
            }
        }

        private async Task SendTeamsNotificationAsync(string endpoint, string message)
        {
            // NOT YET IMPLEMENTED
        }

    }
}