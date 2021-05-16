using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models.BscScanModels;
using AnalyzerCore.Notifier;
using log4net;
using Range = AnalyzerCore.Models.Range;

namespace AnalyzerCore.Services
{
    public class GasAnalyzerService : BackgroundService
    {
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        private readonly int taskDelayMs = 120000;
        private string ourAddress = "0x153e170524cfad4261743ce8bd8053e15d6d1f15";
        private TelegramNotifier telegramNotifier = new TelegramNotifier();

        public Dictionary<string, List<Result>> SharedData = new Dictionary<string, List<Result>>();
        public Dictionary<string, List<Result>> State = new Dictionary<string, List<Result>>();

        public GasAnalyzerService(Dictionary<string, List<Result>> data)
        {
            log.Info("GasAnalyzerService starting...");
            this.SharedData = data;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var trx = new List<Result>();
                try
                {
                    trx = SharedData[ourAddress];
                }
                catch (System.Collections.Generic.KeyNotFoundException)
                {
                    log.Info("Dictionary not yet filled by the analyzer service, waiting...");
                    await Task.Delay(2000);
                    continue;
                }
                List<string> tgMsgs = new List<string>();

                tgMsgs.Add($"*Result of Gas Analysis*");
                List<Range> ranges = new List<Range>();
                ranges.Add(new Range
                {
                    rangeName = "x<=5.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() <= 5.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "5.000005>x<7.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 5.000005 && x.getGasPrice() < 7.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "7.000005>x<10.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 7.000005 && x.getGasPrice() < 10.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "10.000005>x<15.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 10.000005 && x.getGasPrice() < 15.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "15.000005>x<25.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 15.000005 && x.getGasPrice() < 25.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "25.000005>x<35.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 25.000005 && x.getGasPrice() < 35.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "35.000005>x<45.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 35.000005 && x.getGasPrice() < 45.000005).ToList()
                });
                ranges.Add(new Range
                {
                    rangeName = "45.000005>x<60.000005",
                    trxInRange = trx.Where(x => x.getGasPrice() > 45.000005 && x.getGasPrice() < 60.000005).ToList()
                });
                foreach (var range in ranges)
                {
                    try
                    {
                        long sr = 100 * range.trxInRange.Where(x => x.txreceipt_status == "1").Count() / range.trxInRange.Count();
                        tgMsgs.Add($"Range: {range.rangeName}, avgGas: {range.trxInRange.Select(x => long.Parse(x.gasPrice)).ToList().Sum() / range.trxInRange.Count()}, TRX: {range.trxInRange.Where(x => x.txreceipt_status == "1").Count()}/{range.trxInRange.Count()}, SR: {sr}%");
                    }
                    catch (System.DivideByZeroException)
                    {
                        log.Debug($"No trx in gas Range: {range.rangeName}");
                    }
                }
                State = SharedData;

                string finalMsg = string.Join(Environment.NewLine, tgMsgs.ToArray());
                telegramNotifier.SendMessage(finalMsg);

                await Task.Delay(taskDelayMs, stoppingToken);
            }
        }

        private static List<Result> GetFailedTrxWithHiGas(List<Result> transactions, long gasTrigger = 40000)
        {
            var toNotify = transactions
                .Where(tr => int.Parse(tr.txreceipt_status) == 0)
                .Where(tr => long.Parse(tr.gasUsed) >= gasTrigger).ToList();
            return toNotify;
        }
    }
}
