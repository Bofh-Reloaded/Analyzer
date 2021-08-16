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
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

                    // unsubscribe
                    await subscription.UnsubscribeAsync();

                    // allow time to unsubscribe
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}