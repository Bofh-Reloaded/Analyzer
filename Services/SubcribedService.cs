using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Notifier;
using log4net;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using Newtonsoft.Json;

namespace AnalyzerCore.Services
{
    public class SubscribedService : BackgroundService
    {
        // Initialize Logger
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        // String Value representing the chain name
        private string _chainName;

        // Define the TelegramNotifier Instance
        private TelegramNotifier _telegramNotifier;

        public SubscribedService(TelegramNotifier telegramNotifier, string chainName)
        {
            _telegramNotifier = telegramNotifier ?? throw new ArgumentNullException(nameof(telegramNotifier));
            _chainName = chainName ?? throw new ArgumentNullException(nameof(chainName));
            _log.Info("SubscribedService started");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var client =
                new StreamingWebSocketClient(
                    "wss://rpc-mainnet.maticvigil.com/ws/v1/2c629d29f345a2ecd738659d8619902338316b72"))
            {
                // create the subscription
                // it won't start receiving data until Subscribe is called on it
                var subscription = new EthNewPendingTransactionObservableSubscription(client);

                // attach a handler subscription created event (optional)
                // this will only occur once when Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    _log.Info($"Pending transactions subscription Id: {subscriptionId}"));

                // attach a handler for each pending transaction
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(transactionHash =>
                {
                    var web3 = new Web3("http://162.55.94.149:8545");
                    var t = web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
                    t.Wait(stoppingToken);
                    if (t.Result == null) return;
                    _log.Info($"new pending trx from addr: {t.Result.From}, txHash: {t.Result.TransactionHash}");
                    if (string.Equals(t.Result.From, "0xEc009815B473459cB8da30F6CF9C6e91C286D7Fe",
                        StringComparison.CurrentCultureIgnoreCase))
                        _log.Info($"US: {JsonConvert.SerializeObject(t.Result, Formatting.Indented)}");
                });

                var subscribed = true;

                //handle unsubscription
                //optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    _log.Info($"Pending transactions unsubscribe result: {response.ToString()}");
                });

                //open the websocket connection
                await client.StartAsync();

                // start listening for pending transactions
                // this will only block long enough to register the subscription with the client
                // it won't block whilst waiting for transactions
                // transactions will be delivered to our handlers on another thread
                await subscription.SubscribeAsync();

                // run for minute
                // transactions should appear on another thread
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

                // unsubscribe
                await subscription.UnsubscribeAsync();

                // wait for unsubscribe 
                while (subscribed) await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}