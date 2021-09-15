﻿using System;
using System.Collections.Generic;
using System.Linq;
using AnalyzerCore.Services;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.String;
using Message = AnalyzerCore.Models.Message;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private static Serilog.Core.Logger _log;

        private readonly TelegramBotClient _bot;

        private readonly ChatId _chatId;

        public TelegramNotifier(string chatId, string botToken)
        {
            _chatId = chatId;
            _bot = new TelegramBotClient(botToken);
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"TelegramNotifier: {botToken}");
        }

        public async void SendMessage(string text)
        {
            try
            {
                _log.Debug(text);
                await _bot.SendTextMessageAsync(
                    _chatId,
                    text,
                    ParseMode.Html,
                    disableWebPagePreview: true
                );
            }
            catch (Exception r)
            {
                _log.Error(r.Message);
            }
        }

        public async void SendStatsRecap(Message message)
        {
            var m = new List<string> { message.Timestamp };
            foreach (var a in message.Addresses)
            {
                m.Add($"<b>\U0001F6A7[{a.Address}]\U0001F6A7</b>");
                switch (a.Address)
                {
                    case "0xa2ca4fb5abb7c2d9a61ca75ee28de89ab8d8c178":
                        m.Add($"<b>[token-king]</b>");
                        break;
                    case "0xaa7dcd270dd6734db602c3ea2e9749f74486619d":
                        m.Add($"<b>[cp-king]</b>");
                        break;
                    case "0x6eb0569afb79ec893c8212cbf4dbad74eea666aa":
                        m.Add($"<b>[cp-king-2]</b>");
                        break;
                }

                var totalTxInMaxBlockRange = a.BlockRanges.Where(b => b.BlockRange == 500);
                if (totalTxInMaxBlockRange.First().TotalTransactionsPerBlockRange == 0)
                {
                    m.Add("  No Activity from this address");
                    continue;
                }

                foreach (var s in a.BlockRanges)
                    try
                    {
                        if (string.Equals(a.Address, message.OurAddress, StringComparison.CurrentCultureIgnoreCase))
                        {
                            m.Add(
                                $" \U0001F4B8<b>B: {s.BlockRange.ToString()} T: {s.TotalTransactionsPerBlockRange.ToString()} S: {s.SuccededTranstactionsPerBlockRange.ToString()} WR: {s.SuccessRate}</b>");
                            var w = s.T0TrxSucceded.Count > 0 ? 100 * s.T0TrxSucceded.Count / s.T0Trx.Count : 0;
                            m.Add(
                                $"   -> Total T0 TRX: {s.T0Trx.Count.ToString()}, Succeeded: {s.T0TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                            w = s.T1TrxSucceded.Count > 0 ? 100 * s.T1TrxSucceded.Count / s.T1Trx.Count : 0;
                            m.Add(
                                $"   -> Total T1 TRX: {s.T1Trx.Count.ToString()}, Succeeded: {s.T1TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                            w = s.T2TrxSucceded.Count > 0 ? 100 * s.T2TrxSucceded.Count / s.T2Trx.Count : 0;
                            m.Add(
                                $"   -> Total T2 TRX: {s.T2Trx.Count.ToString()}, Succeeded: {s.T2TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                            w = s.ContPSucceded.Count > 0 ? 100 * s.ContPSucceded.Count / s.ContP.Count : 0;
                            m.Add(
                                $"   -> Total Cont TRX: {s.ContP.Count.ToString()}, Succeeded: {s.ContPSucceded.Count.ToString()}, WR: {w.ToString()}%");
                        }
                        else
                        {
                            if (s.TotalTransactionsPerBlockRange == 0) continue;
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