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

        /// <summary>
        /// Creates an instance of the PingerFunction with the given logger factory.
        /// </summary>
        /// <param name="loggerFactory">The logger factory to use for logging from this function.</param>
        public PingerFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PingerFunction>();
        }

        /// <summary>
        /// This function is called every 30 seconds and checks the following endpoints:
        ///     - google.com:80
        ///     - google.com:443
        /// </summary>
        /// <param name="myTimer">Information about the timer that triggered this function.</param>
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
        
        /// <summary>
        /// Checks all endpoints that are configured in the <see cref="_endpointsToCheck"/> dictionary.
        /// </summary>
        /// <remarks>
        /// The method creates a list of tasks, where each task is either a call to <see cref="CheckPortAsync(string, int)"/>
        /// for all configured ports of an endpoint, or a call to <see cref="PingHostAsync(string)"/> for the host itself.
        /// The method then waits for all tasks to complete using <see cref="Task.WhenAll(IEnumerable{Task})"/>.
        /// </remarks>
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
        
        /// <summary>
        /// Checks whether the specified TCP port on the given host is reachable.
        /// </summary>
        /// <param name="hostname">The hostname or IP address of the server to check.</param>
        /// <param name="port">The TCP port to check.</param>
        /// <returns><see langword="true"/> if the port is reachable, <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// The method attempts to connect to the specified port using a <see cref="TcpClient"/>,
        /// and waits for 5 seconds for the connection to complete. If the connection completes
        /// successfully, the method logs a message at the information level and returns <see langword="true"/>.
        /// If the connection times out, the method logs a message at the warning level and returns <see langword="false"/>.
        /// If the connection is faulted (i.e. an exception is thrown), the method logs a message at the error level
        /// and returns <see langword="false"/>.
        /// </remarks>
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
        
        /// <summary>
        /// Sends a ping to the specified hostname and waits for 5 seconds for the response.
        /// </summary>
        /// <param name="hostname">The hostname to ping.</param>
        /// <returns><see langword="true"/> if the ping was successful, <see langword="false"/> otherwise.</returns>
        /// <remarks>
        /// The method attempts to ping the specified hostname using a <see cref="Ping"/>,
        /// and waits for 5 seconds for the response. If the response is received successfully,
        /// the method logs a message at the information level and returns <see langword="true"/>.
        /// If the response times out, the method logs a message at the warning level and returns <see langword="false"/>.
        /// If the response is faulted (i.e. an exception is thrown), the method logs a message at the error level
        /// and returns <see langword="false"/>.
        /// </remarks>
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

        /// <summary>
        /// Checks whether the specified endpoint is reachable, and if not, sends a Teams notification.
        /// </summary>
        /// <param name="endpoint">The hostname or IP address of the server to check.</param>
        /// <remarks>
        /// The method attempts to ping the specified endpoint using a <see cref="Ping"/>,
        /// and waits for 5 seconds for the response. If the response is received successfully,
        /// the method logs a message at the information level and returns <see langword="true"/>.
        /// If the response times out or is faulted (i.e. an exception is thrown), the method logs a message at the warning level
        /// and returns <see langword="false"/>. If the response is not successful, the method also sends a Teams notification
        /// with the specified message.
        /// </remarks>
        private async Task CheckEndpointStatusAsync(string endpoint)
        {
            if (!await PingHostAsync(endpoint))
            {
                await SendTeamsNotificationAsync(endpoint, "Ping failed");
            }
        }
        
        /// <summary>
        /// Sends a Teams notification for the specified <paramref name="endpoint"/>,
        /// with the specified <paramref name="message"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The method is not yet implemented.
        /// </para>
        /// <para>
        /// The method should send a message to the Teams channel associated with the
        /// specified <paramref name="endpoint"/>, with the specified <paramref name="message"/>.
        /// </para>
        /// </remarks>
        /// <param name="endpoint">The hostname or IP address of the server that is the subject of the notification.</param>
        /// <param name="message">The message to be included in the notification.</param>
        private async Task SendTeamsNotificationAsync(string endpoint, string message)
        {
            // NOT YET IMPLEMENTED
        }

    }
}