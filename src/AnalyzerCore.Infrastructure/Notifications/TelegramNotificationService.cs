using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AnalyzerCore.Infrastructure.Notifications
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string message, CancellationToken cancellationToken = default);
        Task SendPoolNotificationAsync(Pool poolInfo, CancellationToken cancellationToken = default);
        Task SendErrorNotificationAsync(Exception ex, string context, CancellationToken cancellationToken = default);
    }

    public class TelegramNotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TelegramNotificationService> _logger;
        private readonly string _botToken;
        private readonly string _chatId;
        private const string BaseUrl = "https://api.telegram.org/bot";

        public TelegramNotificationService(
            HttpClient httpClient,
            ILogger<TelegramNotificationService> logger,
            string botToken,
            string chatId)
        {
            _httpClient = httpClient;
            _logger = logger;
            _botToken = botToken ?? throw new ArgumentNullException(nameof(botToken));
            _chatId = chatId ?? throw new ArgumentNullException(nameof(chatId));
        }

        public async Task SendNotificationAsync(string message, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{BaseUrl}{_botToken}/sendMessage";
                var content = new
                {
                    chat_id = _chatId,
                    text = message,
                    parse_mode = "HTML"
                };

                var json = JsonSerializer.Serialize(content);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, stringContent, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("Notification sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification: {Message}", message);
                throw;
            }
        }

        public async Task SendPoolNotificationAsync(Pool poolInfo, CancellationToken cancellationToken = default)
        {
            var message = new StringBuilder();
            message.AppendLine("🔄 <b>New Pool Detected</b>");
            message.AppendLine();
            message.AppendLine($"📍 <b>Address:</b> <code>{poolInfo.Address}</code>");
            message.AppendLine($"🏭 <b>Factory:</b> <code>{poolInfo.Factory}</code>");
            message.AppendLine();
            message.AppendLine("🪙 <b>Tokens:</b>");
            message.AppendLine($"• {poolInfo.Token0} ({Web3.Convert.FromWei(poolInfo.Reserve0):N2})");
            message.AppendLine($"• {poolInfo.Token1} ({Web3.Convert.FromWei(poolInfo.Reserve1):N2})");
            message.AppendLine();
            message.AppendLine($"⚡ <b>Type:</b> {poolInfo.Type}");

            await SendNotificationAsync(message.ToString(), cancellationToken);
        }

        public async Task SendErrorNotificationAsync(
            Exception ex,
            string context,
            CancellationToken cancellationToken = default)
        {
            var message = new StringBuilder();
            message.AppendLine("❌ <b>Error Detected</b>");
            message.AppendLine();
            message.AppendLine($"📝 <b>Context:</b> {context}");
            message.AppendLine($"⚠️ <b>Error:</b> {ex.Message}");
            
            if (ex.StackTrace != null)
            {
                message.AppendLine();
                message.AppendLine("<b>Stack Trace:</b>");
                message.AppendLine($"<code>{ex.StackTrace}</code>");
            }

            await SendNotificationAsync(message.ToString(), cancellationToken);
        }

        private record Pool
        {
            public string Address { get; init; }
            public string Factory { get; init; }
            public string Token0 { get; init; }
            public string Token1 { get; init; }
            public decimal Reserve0 { get; init; }
            public decimal Reserve1 { get; init; }
            public string Type { get; init; }
        }
    }
}