using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Exceptions;

namespace AnalyzerCore.Services
{
    public class NewTokenService : BackgroundService
    {
        private readonly string _wssUri;
        private readonly Logger _log;

        public static string NewPairEvent { get; } =
            "0x0d3648bd0f6ba80134a33ba9275ac585d9d315f0ad8355cddefde31afa28d0e9";

        public NewTokenService()
        {
            _wssUri = "wss://bsc-ws-node.nariox.org:443";
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithExceptionDetails()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] " +
                                    "[ThreadId {ThreadId}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            _log.Information("Starting Service");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.Information($"Starting cycle, wss: {_wssUri}");

            using (var client = new StreamingWebSocketClient(_wssUri))
            {
                var filterPairCreated = Event<PairCreatedEventDTO>.GetEventABI().CreateFilterInput();

                // create the subscription
                // (it won't start receiving data until Subscribe is called)
                var subscription = new EthNewBlockHeadersObservableSubscription(client);

                // attach a handler for when the subscription is first created (optional)
                // this will occur once after Subscribe has been called
                subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
                    Console.WriteLine("Block Header subscription Id: " + subscriptionId));

                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(transactionHash =>
                {
                    _log.Information(transactionHash.ToString());
                });
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