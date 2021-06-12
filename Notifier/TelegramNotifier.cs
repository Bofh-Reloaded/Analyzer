using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using log4net;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private TelegramBotClient _Bot = new TelegramBotClient(
            token: "1780013642:AAH2nN3rFtRFLQzh4dHd1gjNTdwGWFHrYL8"
            );
        private readonly ChatId _chatId = new ChatId(identifier: -560874043);
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public TelegramNotifier()
        {
        }

        public TelegramNotifier(string chatId)
        {
            this._chatId = chatId;
        }

        public async void SendMessage(string text)
        {
            log.Debug(text);
            Message result = await _Bot.SendTextMessageAsync(
                            chatId: this._chatId,
                            text: text,
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Default
                            );
        }
    }

    /*public class TelegramBotService : IHostedService, IDisposable
    {
        private TelegramBotClient _Bot = new TelegramBotClient(
            token: "1780013642:AAH2nN3rFtRFLQzh4dHd1gjNTdwGWFHrYL8"
            );
        private readonly ChatId _chatId = new ChatId(identifier: -560874043);
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public TelegramBotService(ILog logger)
        {
            _log = logger;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _log.Info("Telegram Bot Service Started.");

            //_timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _log.Info("Timed Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }*/
}