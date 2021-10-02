using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnalyzerCore.DbLayer;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
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

        private readonly List<string> _inMemorySeenToken = new List<string>();

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
                _log.Debug("{Text}", text);
                await _bot.SendTextMessageAsync(
                    _chatId,
                    text,
                    ParseMode.Html,
                    disableWebPagePreview: true
                );
            }
            catch (Exception r)
            {
                _log.Error("{Message}", r.Message);
            }
        }

        public async Task DeleteMessageAsync(int messageId)
        {
            try
            {
                _log.Debug("Deleting messageId: {MessageId} inside chat: {ChatId}", messageId, _chatId);
                await _bot.DeleteMessageAsync(_chatId, messageId);
            }
            catch (Exception e)
            {
                _log.Error("{Message}", e.Message);
            }
        }

        public async Task UpdateMissingTokensAsync(string baseUri, string version)
        {
            await using var context = new TokenDbContext();
            // taking tokens notified but not yet deleted
            var query = context.Tokens.Where(t => t.Notified == true && t.Deleted == false && t.TxCount > 10);
            var tokenToBeUpdated = query.ToList();
            _log.Debug("going to update {tokenNumber}", tokenToBeUpdated.Count.ToString());
            foreach (var t in tokenToBeUpdated)
            {
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Exchanges)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Pools)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.TransactionHashes)
                    .LoadAsync();
                const string star = "\U00002B50";
                var transactionHash = t.TransactionHashes.FirstOrDefault()?.Hash;
                var pools = t.Pools.ToList();
                if (transactionHash != null)
                {
                    var msg = string.Join(
                        Environment.NewLine,
                        $"<b>{t.TokenSymbol} [<a href='{baseUri}token/{t.TokenAddress}'>{t.TokenAddress}</a>]:</b>",
                        $"{string.Concat(Enumerable.Repeat(star, t.TxCount))}",
                        $"  token address: {t.TokenAddress}",
                        $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                        $"  totalTxCount: {t.TxCount.ToString()}",
                        $"  lastTxSeen: <a href='{baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                        $"  from: <a href='{baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                        $"  to: <a href='{baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                        $"  pools: [{Environment.NewLine}{string.Join(Environment.NewLine, pools.Select(p => $"    <a href='{baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  exchanges: [{Environment.NewLine}{string.Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  version: {version}"
                    );
                    try
                    {
                        await _bot.EditMessageTextAsync(
                            _chatId,
                            t.TelegramMsgId,
                            msg,
                            ParseMode.Html,
                            disableWebPagePreview: true);
                    }
                    catch (Telegram.Bot.Exceptions.MessageIsNotModifiedException)
                    {
                        _log.Debug("no updates for token {token}", t.TokenId);
                    }
                }
            }
        }

        public async Task NotifyMissingTokens(string baseUri, string version)
        {
            await using var context = new TokenDbContext();
            var query = context.Tokens.Where(t => t.Notified == false);
            var tokenToNotify = query.ToList();
            _log.Debug("MissingTokens: {MissingTokens}", tokenToNotify.ToList().Count.ToString());
            if (tokenToNotify.ToList().Count <= 0)
            {
                _log.Information("No Missing token found this time");
                return;
            }

            foreach (var t in tokenToNotify)
            {
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Exchanges)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.Pools)
                    .LoadAsync();
                await context.Entry(t)
                    .Collection(tokenEntity => tokenEntity.TransactionHashes)
                    .LoadAsync();
                const string star = "\U00002B50";
                var transactionHash = t.TransactionHashes.FirstOrDefault()?.Hash;
                var pools = t.Pools.ToList();
                if (transactionHash != null)
                {
                    var msg = string.Join(
                        Environment.NewLine,
                        $"<b>{t.TokenSymbol} [<a href='{baseUri}token/{t.TokenAddress}'>{t.TokenAddress}</a>]:</b>",
                        $"{string.Concat(Enumerable.Repeat(star, t.TxCount))}",
                        $"  token address: {t.TokenAddress}",
                        $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                        $"  totalTxCount: {t.TxCount.ToString()}",
                        $"  lastTxSeen: <a href='{baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                        $"  from: <a href='{baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                        $"  to: <a href='{baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                        $"  pools: [{Environment.NewLine}{string.Join(Environment.NewLine, pools.Select(p => $"    <a href='{baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  exchanges: [{Environment.NewLine}{string.Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  version: {version}"
                    );
                    _log.Information("{MsgSerialized}", JsonConvert.SerializeObject(msg, Formatting.Indented));
                    if (_inMemorySeenToken.Contains(t.TokenAddress.ToLower())) return;
                    _inMemorySeenToken.Add(t.TokenAddress.ToLower());
                    var notifierResponse = await SendMessageWithReturnAsync(msg);

                    t.Notified = true;
                    if (notifierResponse != null) t.TelegramMsgId = notifierResponse.MessageId;
                }

                await context.SaveChangesAsync();
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        public async Task<Telegram.Bot.Types.Message> SendMessageWithReturnAsync(string text)
        {
            try
            {
                _log.Debug("{Text}", text);
                var resp = await _bot.SendTextMessageAsync(
                    _chatId,
                    text,
                    ParseMode.Html,
                    disableWebPagePreview: true
                );
                return resp;
            }
            catch (Exception r)
            {
                _log.Error("{Message}", r.Message);
                return null;
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
                    case "0xf188b2a4cec428a479ab87e61d2e3bbf17f7c32a":
                        m.Add("<b>[new-competitor]</b>");
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
                                $" B: {s.BlockRange.ToString()} T: {s.TotalTransactionsPerBlockRange.ToString()} S: {s.SuccededTranstactionsPerBlockRange.ToString()} WR: {s.SuccessRate}");
                        }
                    }
                    catch (DivideByZeroException e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
            }

            m.Add(
                $"\U0001F4CATotal TRX on last 500B: {message.TotalTrx.ToString()}, Average TPS: {message.Tps}\U0001F4CA");
            var _ = await _bot.SendTextMessageAsync(
                _chatId,
                Join(Environment.NewLine, m.ToArray()),
                ParseMode.Html
            );
        }
    }
}