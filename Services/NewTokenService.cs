using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;

namespace AnalyzerCore.Services
{
    public class NewTokenService : BackgroundService
    {
        private const string NewPairEvent = "0x0d3648bd0f6ba80134a33ba9275ac585d9d315f0ad8355cddefde31afa28d0e9";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // ** SEE THE TransferEventDTO class below **

                using (var client = new StreamingWebSocketClient("wss://rinkeby.infura.io/ws"))
                {
                    // create the subscription
                    // (it won't start receiving data until Subscribe is called)
                    var subscription = new EthNewBlockHeadersObservableSubscription(client);

                    // attach a handler for when the subscription is first created (optional)
                    // this will occur once after Subscribe has been called
                    subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                        Console.WriteLine("Block Header subscription Id: " + subscriptionId));

                    DateTime? lastBlockNotification = null;
                    double secondsSinceLastBlock = 0;

                    // attach a handler for each block
                    // put your logic here
                    subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
                    {
                        secondsSinceLastBlock = (lastBlockNotification == null)
                            ? 0
                            : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
                        lastBlockNotification = DateTime.Now;
                        var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
                        Console.WriteLine(
                            $"New Block. Number: {block.Number.Value}, Timestamp UTC: {JsonConvert.SerializeObject(utcTimestamp)}, Seconds since last block received: {secondsSinceLastBlock} ");
                    });

                    bool subscribed = true;

                    // handle unsubscription
                    // optional - but may be important depending on your use case
                    subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                    {
                        subscribed = false;
                        Console.WriteLine("Block Header unsubscribe result: " + response);
                    });

                    // open the websocket connection
                    await client.StartAsync();

                    // start the subscription
                    // this will only block long enough to register the subscription with the client
                    // once running - it won't block whilst waiting for blocks
                    // blocks will be delivered to our handler on another thread
                    await subscription.SubscribeAsync();

                    // run for a minute before unsubscribing
                    await Task.Delay(TimeSpan.FromMinutes(1));

                    // unsubscribe
                    await subscription.UnsubscribeAsync();

                    //allow time to unsubscribe
                    while (subscribed) await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
    }
}


        public static async Task NewBlockHeader_With_Subscription()
        {
            using (var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws"))
            {
                // create a subscription 
                // it won't do anything just yet though
                var subscription = new EthNewBlockHeadersSubscription(client);

                // attach our handler for new block header data
                subscription.SubscriptionDataResponse += (object sender, StreamingEventArgs<Block> e) =>
                {
                    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)e.Response.Timestamp.Value);
                    Console.WriteLine(
                        $"New Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
                };

                // open the web socket connection
                await client.StartAsync();

                // subscribe to new block headers
                // blocks will be received on another thread
                // therefore this doesn't block the current thread
                await subscription.SubscribeAsync();

                //allow some time before we close the connection and end the subscription
                await Task.Delay(TimeSpan.FromMinutes(1));

                // the connection closing will end the subscription
            }
        }

        public static async Task NewPendingTransactions()
        {
            using (var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws"))
            {
                // create the subscription
                // it won't start receiving data until Subscribe is called on it
                var subscription = new EthNewPendingTransactionObservableSubscription(client);

                // attach a handler subscription created event (optional)
                // this will only occur once when Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    Console.WriteLine("Pending transactions subscription Id: " + subscriptionId));

                // attach a handler for each pending transaction
                // put your logic here
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(transactionHash =>
                {
                    Console.WriteLine("New Pending TransactionHash: " + transactionHash);
                });

                bool subscribed = true;

                //handle unsubscription
                //optional - but may be important depending on your use case
                subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
                {
                    subscribed = false;
                    Console.WriteLine("Pending transactions unsubscribe result: " + response);
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

        public static async Task GetLogs_Observable_Subscription()
        {
            using (var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws"))
            {
                // create the subscription
                // nothing will happen just yet though
                var subscription = new EthLogsObservableSubscription(client);

                // attach our handler for each log
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
                    Console.WriteLine("Log Address:" + log.Address));

                // create the web socket connection
                await client.StartAsync();

                // begin receiving subscription data
                // data will be received on another thread
                await subscription.SubscribeAsync();

                // allow to run for a minute
                await Task.Delay(TimeSpan.FromMinutes(1));

                // unsubscribe
                await subscription.UnsubscribeAsync();

                // allow some time to unsubscribe
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        public static async Task GetLogsTokenTransfer_Observable_Subscription()
        {
            // ** SEE THE TransferEventDTO class below **

            using (var client = new StreamingWebSocketClient("wss://mainnet.infura.io/ws"))
            {
                // create a log filter specific to Transfers
                // this filter will match any Transfer (matching the signature) regardless of address
                var filterTransfers = Event<TransferEventDTO>.GetEventABI().CreateFilterInput();

                // create the subscription
                // it won't do anything yet
                var subscription = new EthLogsObservableSubscription(client);

                // attach a handler for Transfer event logs
                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(log =>
                {
                    try
                    {
                        // decode the log into a typed event log
                        var decoded = Event<TransferEventDTO>.DecodeEvent(log);
                        if (decoded != null)
                        {
                            Console.WriteLine("Contract address: " + log.Address + " Log Transfer from:" +
                                              decoded.Event.From);
                        }
                        else
                        {
                            // the log may be an event which does not match the event
                            // the name of the function may be the same
                            // but the indexed event parameters may differ which prevents decoding
                            Console.WriteLine("Found not standard transfer log");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Log Address: " + log.Address + " is not a standard transfer log:",
                            ex.Message);
                    }
                });

                // open the web socket connection
                await client.StartAsync();

                // begin receiving subscription data
                // data will be received on a background thread
                await subscription.SubscribeAsync(filterTransfers);

                // run for a while
                await Task.Delay(TimeSpan.FromMinutes(1));

                // unsubscribe
                await subscription.UnsubscribeAsync();

                // allow time to unsubscribe
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        // This class describes the Transfer event
        // It allows untyped logs to be decoded into typed representations
        // This allows the event parameters to be decoded
        // It also provides a basis for creating filters which are used to retrieve matching logs 
        // It can be created by hand but often this class is code generated 
        // It is marked as partial to allow you to extend it without breaking everytime the code is regenerated
        // .nethereum-events-gettingstarted/
        public partial class TransferEventDTO : TransferEventDTOBase
        {
        }

        [Event("Transfer")]
        public class TransferEventDTOBase : IEventDTO
        {
            [Parameter("address", "_from", 1, true)]
            public virtual string From { get; set; }

            [Parameter("address", "_to", 2, true)] public virtual string To { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public virtual BigInteger Value { get; set; }
        }
    }
}