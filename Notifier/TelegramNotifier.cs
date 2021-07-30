using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using log4net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.String;
using Message = AnalyzerCore.Models.Message;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);

        private readonly TelegramBotClient _bot = new TelegramBotClient(
            "1780013642:AAH2nN3rFtRFLQzh4dHd1gjNTdwGWFHrYL8"
        );

        private readonly ChatId _chatId = new ChatId(identifier: -560874043);

        public TelegramNotifier()
        {
        }

        public TelegramNotifier(string chatId)
        {
            _chatId = chatId;
        }

        public async void SendMessage(string text)
        {
            Log.Debug(text);
            await _bot.SendTextMessageAsync(
                _chatId,
                text,
                ParseMode.Html
            );
        }

        public async void SendStatsRecap(Message message)
        {
            var m = new List<string> {message.Timestamp};
            foreach (var a in message.Addresses)
            {
                m.Add($"<b>\U0001F6A7[{a.Address}]\U0001F6A7</b>");
                foreach (var s in a.BlockRanges)
                    try
                    {
                        if (string.Equals(a.Address, message.OurAddress, StringComparison.CurrentCultureIgnoreCase))
                        {
                            m.Add(
                                $" \U0001F4B8<b>B: {s.BlockRange.ToString()} T: {s.TotalTransactionsPerBlockRange.ToString()} S: {s.SuccededTranstactionsPerBlockRange.ToString()} WR: {s.SuccessRate}</b>");
                            if (s.T0TrxSucceded.Count > 0)
                                m.Add(
                                    $"   -> Total T0 TRX: {s.T0Trx.Count.ToString()}, Succeded: {s.T0TrxSucceded.Count.ToString()}, WR: {100 * s.T0TrxSucceded.Count / s.T0Trx.Count}%");
                            if (s.T1TrxSucceded.Count > 0)
                                m.Add(
                                    $"   -> Total T1 TRX: {s.T1Trx.Count.ToString()}, Succeded: {s.T1TrxSucceded.Count.ToString()}, WR: {100 * s.T1TrxSucceded.Count / s.T1Trx.Count}%");
                            if (s.T2TrxSucceded.Count > 0)
                                m.Add(
                                    $"   -> Total T2 TRX: {s.T2Trx.Count.ToString()}, Succeded: {s.T2TrxSucceded.Count.ToString()}, WR: {100 * s.T2TrxSucceded.Count / s.T2Trx.Count}%");
                            if (s.ContPSucceded.Count > 0)
                                m.Add(
                                    $"   -> Total Cont TRX: {s.ContP.Count.ToString()}, Succeded: {s.ContPSucceded.Count.ToString()}, WR: {100 * s.ContPSucceded.Count / s.ContP.Count}%");
                        }
                        else
                        {
                            m.Add(
                                $" B: {s.BlockRange} T: {s.TotalTransactionsPerBlockRange} S: {s.SuccededTranstactionsPerBlockRange} WR: {s.SuccessRate}");
                        }
                    }
                    catch (DivideByZeroException e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
            }

            m.Add($"\U0001F4CATotal TRX on last 500B: {message.TotalTrx}, Average TPS: {message.Tps}\U0001F4CA");
            var _ = await _bot.SendTextMessageAsync(
                _chatId,
                Join(Environment.NewLine, m.ToArray()),
                ParseMode.Html
            );
        }
    }
}