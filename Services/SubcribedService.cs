using AnalyzerCore.Notifier;
using log4net;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Subscriptions;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Web3;
using Newtonsoft.Json;
using System;
using System.Numerics;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AnalyzerCore.Services
{
    public class SubscribedService : BackgroundService
    {
        // Initialize Logger
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );

        // Define the TelegramNotifier Instance
        private TelegramNotifier telegramNotifier;

        // String Value representing the chain name
        private string chainName;

        public SubscribedService()
        {
            log.Info("SubscribedService started");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var client = new StreamingWebSocketClient("wss://rpc-mainnet.maticvigil.com/ws/v1/2c629d29f345a2ecd738659d8619902338316b72"))
            {
                // create the subscription
                // it won't start receiving data until Subscribe is called on it
                var subscription = new EthNewPendingTransactionObservableSubscription(client);

                // attach a handler subscription created event (optional)
                // this will only occur once when Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    log.Info("Pending transactions subscription Id: " + subscriptionId));

                // attach a handler for each pending transaction
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(transactionHash =>
                {
                    Web3 web3 = new Web3(url: "http://162.55.94.149:8545");
                    Task<Transaction> t = web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
                    t.Wait();
                    if (t.Result != null)
                    {
                        log.Info($"new pending trx from addr: {t.Result.From}, txhash: {t.Result.TransactionHash}");
                        if (t.Result.From.ToLower() == "0xEc009815B473459cB8da30F6CF9C6e91C286D7Fe".ToLower())
                        {
                            log.Info($"US: {JsonConvert.SerializeObject(t.Result, Formatting.Indented)}");
                        }
                    }

                });

                bool subscribed = true;

                //handle unsubscription
                //optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    log.Info("Pending transactions unsubscribe result: " + response);
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
                await Task.Delay(TimeSpan.FromMinutes(1));

                // unsubscribe
                await subscription.UnsubscribeAsync();

                // wait for unsubscribe 
                while (subscribed)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}