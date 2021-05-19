using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models.BscScanModels;
using AnalyzerCore.Notifier;
using log4net;

namespace AnalyzerCore.Services
{
    public class AnalyzeTheBastard : BackgroundService
    {
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        private readonly int taskDelayMs = 120000;
        private string ourAddress = "0x153e170524cfad4261743ce8bd8053e15d6d1f15";
        private TelegramNotifier telegramNotifier = new TelegramNotifier();
        private List<string> trxHashAlerted = new List<string>();

        public Dictionary<string, List<Result>> SharedData = new Dictionary<string, List<Result>>();
        public Dictionary<string, List<Result>> State = new Dictionary<string, List<Result>>();

        public AnalyzeTheBastard(Dictionary<string, List<Result>> data)
        {
            log.Info("FailedTrxService starting...");
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
                } catch (System.Collections.Generic.KeyNotFoundException)
                {
                    log.Info("Dictionary not yet filled by the analyzer service, waiting...");
                    await Task.Delay(2000);
                    continue;
                }

                var t = GetFailedTrxWithHiGas(transactions: trx);
                var trxToNotify = t.Where(t => !trxHashAlerted.Contains(t.hash)).ToList();
                if (trxToNotify.Count() == 0)
                {
                    log.Info($"Analyzed: {trx.Count()} trx, no failures with gas > 40000");
                }
                foreach (var tn in trxToNotify)
                {
                    telegramNotifier.SendMessage($"Tx failed: https://bscscan.com/tx/{tn.hash} with gasUsed: {tn.gasUsed}");
                    trxHashAlerted.Add(tn.hash);
                }
                State = SharedData;

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
