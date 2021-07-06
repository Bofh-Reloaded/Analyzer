using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using log4net;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using System.Text.Json;
using System.Collections.Generic;
using Message = AnalyzerCore.Models.Message;
using System.Linq;

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
            Telegram.Bot.Types.Message result = await _Bot.SendTextMessageAsync(
                            chatId: this._chatId,
                            text: text,
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Default
                            );
        }

        public async void SendStatsRecap(Message message)
        {
            log.Debug(JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true }));
            List<string> _m = new List<string>();
            _m.Add(message.Timestamp);
            foreach (var _a in message.Addresses)
            {
                _m.Add($"<b>\U0001F6A7[{_a.Address}]\U0001F6A7</b>");
                foreach (var _s in _a.BlockRanges)
                {
                    _m.Add($" B: {_s.BlockRange} T: {_s.TotalTransactionsPerBlockRange} S: {_s.SuccededTranstactionsPerBlockRange} WR: {_s.SuccessRate}");
                }
            }
            _m.Add($"\U0001F4CATotal TRX on last 500B: {message.TotalTrx}, Average TPS: {message.TPS}\U0001F4CA");
            Telegram.Bot.Types.Message result = await _Bot.SendTextMessageAsync(
                chatId: this._chatId,
                text: string.Join(Environment.NewLine, _m.ToArray()),
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Html
                );
        }
    }
}