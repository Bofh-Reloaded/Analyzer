using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using log4net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Message = AnalyzerCore.Models.Message;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ChatId _chatId = new ChatId(identifier: -560874043);

        private readonly TelegramBotClient _Bot = new TelegramBotClient(
            "1780013642:AAH2nN3rFtRFLQzh4dHd1gjNTdwGWFHrYL8"
        );

        public TelegramNotifier()
        {
        }

        public TelegramNotifier(string chatId)
        {
            _chatId = chatId;
        }

        public async void SendMessage(string text)
        {
            log.Debug(text);
            var result = await _Bot.SendTextMessageAsync(
                _chatId,
                text,
                ParseMode.Markdown
            );
        }

        public async void SendStatsRecap(Message message)
        {
            log.Debug(JsonSerializer.Serialize(message, new JsonSerializerOptions {WriteIndented = true}));
            var _m = new List<string>();
            _m.Add(message.Timestamp);
            foreach (var _a in message.Addresses)
            {
                _m.Add($"<b>\U0001F6A7[{_a.Address}]\U0001F6A7</b>");
                foreach (var _s in _a.BlockRanges)
                    if (_a.Address.ToLower() == message.ourAddress.ToLower())
                    {
                        _m.Add(
                            $" \U0001F4B8<b>B: {_s.BlockRange} T: {_s.TotalTransactionsPerBlockRange} S: {_s.SuccededTranstactionsPerBlockRange} WR: {_s.SuccessRate}</b>");
                        if (_s.T0TrxSucceded.Count > 0)
                            _m.Add(
                                $"   -> Total T0 TRX: {_s.T0Trx.Count}, Succeded: {_s.T0TrxSucceded.Count}, WR: {100 * _s.T0TrxSucceded.Count / _s.T0Trx.Count}%");
                        if (_s.T1TrxSucceded.Count > 0)
                            _m.Add(
                                $"   -> Total T1 TRX: {_s.T1Trx.Count}, Succeded: {_s.T1TrxSucceded.Count}, WR: {100 * _s.T1TrxSucceded.Count / _s.T1Trx.Count}%");
                        if (_s.T2TrxSucceded.Count > 0)
                            _m.Add(
                                $"   -> Total T2 TRX: {_s.T2Trx.Count}, Succeded: {_s.T2TrxSucceded.Count}, WR: {100 * _s.T2TrxSucceded.Count / _s.T2Trx.Count}%");
                        if (_s.ContPSucceded.Count > 0)
                            _m.Add(
                                $"   -> Total Cont TRX: {_s.ContP.Count}, Succeded: {_s.ContPSucceded.Count}, WR: {100 * _s.ContPSucceded.Count / _s.ContP.Count}%");
                    }
                    else
                    {
                        _m.Add(
                            $" B: {_s.BlockRange} T: {_s.TotalTransactionsPerBlockRange} S: {_s.SuccededTranstactionsPerBlockRange} WR: {_s.SuccessRate}");
                    }
            }

            _m.Add($"\U0001F4CATotal TRX on last 500B: {message.TotalTrx}, Average TPS: {message.TPS}\U0001F4CA");
            var _ = await _Bot.SendTextMessageAsync(
                _chatId,
                string.Join(Environment.NewLine, _m.ToArray()),
                ParseMode.Html
            );
        }
    }
}