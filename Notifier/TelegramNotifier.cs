using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AnalyzerCore.DbLayer;
using AnalyzerCore.Models;
using Newtonsoft.Json;
using Polly;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.String;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Message = AnalyzerCore.Models.Message;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private static Serilog.Core.Logger _log;

        private readonly TelegramBotClient _bot;

        private readonly ChatId _chatId;

        private readonly List<string> _inMemorySeenToken = new();

        private Dictionary<string, int> _tokenTransactionsCount = new();
        private readonly AnalyzerConfig _config;

        private const string TASK_TASK_TMP_FILE_NAME = "tokensCont.json";

        public TelegramNotifier(string chatId, string botToken, AnalyzerConfig config)
        {
            _chatId = chatId;
            _bot = new TelegramBotClient(botToken);
            _config = config;
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            LogContext.PushProperty("SourceContext", $"TelegramNotifier: {botToken}");
            LoadTokenDictionaryWithTxCount();
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

        private void LoadTokenDictionaryWithTxCount()
        {
            try
            {
                var f = System.IO.File.ReadAllText(TASK_TASK_TMP_FILE_NAME);
                _tokenTransactionsCount = JsonSerializer.Deserialize<Dictionary<string, int>>(f);
            }
            catch (Exception)
            {
                _log.Error("file {Filename} not found, will be created later", TASK_TASK_TMP_FILE_NAME);
            }
        }

        public async Task UpdateMissingTokensAsync(string baseUri, string version)
        {
            await using var context = new TokenDbContext();
            // taking tokens notified but not yet deleted
            var query = context.Tokens.Where(t => t.Notified == true && t.Deleted == false);
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
                if (transactionHash == null) continue;
                var msg = Join(
                    Environment.NewLine,
                    $"<b>{t.TokenSymbol} [<a href='{baseUri}token/{t.TokenAddress}'>{t.TokenAddress}</a>]:</b>",
                    $"{Concat(Enumerable.Repeat(star, t.TxCount))}",
                    $"  token address: {t.TokenAddress}",
                    $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                    $"  totalTxCount: {t.TxCount.ToString()}",
                    $"  lastTxSeen: <a href='{baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                    $"  from: <a href='{baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                    $"  to: <a href='{baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                    $"  pools: [{Environment.NewLine}{Join(Environment.NewLine, pools.Select(p => $"    <a href='{baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                    $"  exchanges: [{Environment.NewLine}{Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                    $"  version: {version}"
                );
                var policy = Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(10, retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
                if (_tokenTransactionsCount.ContainsKey(t.TokenAddress) &&
                    _tokenTransactionsCount[t.TokenAddress] == t.TxCount)
                {
                    continue;
                }

                _log.Warning("update token: {tokenSymbol}", t.TokenSymbol);

                _tokenTransactionsCount[t.TokenAddress] = t.TxCount;
                var jsonString = JsonSerializer.Serialize(_tokenTransactionsCount,
                    new JsonSerializerOptions() { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(TASK_TASK_TMP_FILE_NAME, jsonString);

                try
                {
                    await policy.ExecuteAsync(async () => await _bot.EditMessageTextAsync(
                        _chatId,
                        t.TelegramMsgId,
                        msg,
                        parseMode: ParseMode.Html,
                        disableWebPagePreview: true)
                    );
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
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
                    var msg = Join(
                        Environment.NewLine,
                        $"<b>{t.TokenSymbol} [<a href='{baseUri}token/{t.TokenAddress}'>{t.TokenAddress}</a>]:</b>",
                        $"{Concat(Enumerable.Repeat(star, t.TxCount))}",
                        $"  token address: {t.TokenAddress}",
                        $"  totalSupplyChanged: {t.IsDeflationary.ToString()}",
                        $"  totalTxCount: {t.TxCount.ToString()}",
                        $"  lastTxSeen: <a href='{baseUri}tx/{transactionHash}'>{transactionHash[..10]}...{transactionHash[^10..]}</a>",
                        $"  from: <a href='{baseUri}{t.From}'>{t.From[..10]}...{t.From[^10..]}</a>",
                        $"  to: <a href='{baseUri}{t.To}'>{t.To[..10]}...{t.To[^10..]}</a>",
                        $"  pools: [{Environment.NewLine}{Join(Environment.NewLine, pools.Select(p => $"    <a href='{baseUri}address/{p.Address.ToString()}'>{p.Address.ToString()[..10]}...{p.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
                        $"  exchanges: [{Environment.NewLine}{Join(Environment.NewLine, t.Exchanges.Select(e => $"    <a href='{baseUri}address/{e.Address.ToString()}'>{e.Address.ToString()[..10]}...{e.Address.ToString()[^10..]}</a>"))}{Environment.NewLine}  ]",
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

        public async Task<Telegram.Bot.Types.Message> EditMessageAsync(int msgId, string text)
        {
            var resp = await _bot.EditMessageTextAsync(_chatId, msgId, text);
            return resp;
        }

        private async Task<Telegram.Bot.Types.Message> SendMessageWithReturnAsync(string text)
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

        public async void SendOurStatsRecap(Message message)
        {
            _log.Information("SendOurStatsRecap");
            var m = new List<string> { message.Timestamp };
            foreach (var a in message.Addresses)
            {
                _log.Debug("Processing our address: {Wallet}", a.Address);
                m.Add($"<b>\U0001F6A7[{a.Address}]\U0001F6A7</b>");
                var totalTxInMaxBlockRange = a.BlockRanges.Where(b => b.BlockRange == 500);
                if (totalTxInMaxBlockRange.First().TotalTransactionsPerBlockRange == 0)
                {
                    m.Add("  No Activity from this address");
                    continue;
                }

                foreach (var s in a.BlockRanges)
                {
                    try
                    {
                        m.Add(
                            $" \U0001F4B8<b>B: {s.BlockRange.ToString()} T: {s.TotalTransactionsPerBlockRange.ToString()} S: {s.SuccededTranstactionsPerBlockRange.ToString()} WR: {s.SuccessRate}</b>");
                        var w = Math.Round(
                            (decimal)s.T0TrxSucceded.Count > 0
                                ? 100 * (decimal)s.T0TrxSucceded.Count / (long)s.T0Trx.Count
                                : 0
                            , 2);
                        m.Add(
                            $"   -> Total T0 TRX: {s.T0Trx.Count.ToString()}, Succeeded: {s.T0TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                        w = Math.Round(
                            (decimal)s.T1TrxSucceded.Count > 0
                                ? 100 * (decimal)s.T1TrxSucceded.Count / (long)s.T1Trx.Count
                                : 0
                            , 2);
                        m.Add(
                            $"   -> Total T1 TRX: {s.T1Trx.Count.ToString()}, Succeeded: {s.T1TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                        w = Math.Round(
                            (decimal)s.T2TrxSucceded.Count > 0
                                ? 100 * (decimal)s.T2TrxSucceded.Count / (long)s.T2Trx.Count
                                : 0
                            , 2);
                        m.Add(
                            $"   -> Total T2 TRX: {s.T2Trx.Count.ToString()}, Succeeded: {s.T2TrxSucceded.Count.ToString()}, WR: {w.ToString()}%");
                        w = Math.Round(
                            (decimal)s.ContPSucceded.Count > 0
                                ? 100 * (decimal)s.ContPSucceded.Count / (long)s.ContP.Count
                                : 0
                            , 2);
                        m.Add(
                            $"   -> Total Cont TRX: {s.ContP.Count.ToString()}, Succeeded: {s.ContPSucceded.Count.ToString()}, WR: {w.ToString()}%");
                    }
                    catch (DivideByZeroException e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                }
            }
            
            var _ = await _bot.SendTextMessageAsync(
                _chatId,
                Join(Environment.NewLine, m.ToArray()),
                ParseMode.Html
            );
        }

        public async void SendCompetitorsStatsRecap(Message message)
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
                        if (s.TotalTransactionsPerBlockRange == 0) continue;
                        m.Add(
                            $" B: {s.BlockRange.ToString()} T: {s.TotalTransactionsPerBlockRange.ToString()} S: {s.SuccededTranstactionsPerBlockRange.ToString()} WR: {s.SuccessRate}");
                    }
                    catch (DivideByZeroException e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
            }

            var _ = await _bot.SendTextMessageAsync(
                _chatId,
                Join(Environment.NewLine, m.ToArray()),
                ParseMode.Html
            );
        }
    }
}