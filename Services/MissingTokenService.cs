using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AnalyzerCore.Models;
using log4net;
using Microsoft.Extensions.Configuration;
using Nethereum.RPC.Eth.DTOs;

public interface IJob
{
}

namespace AnalyzerCore.Services
{
    public class TransactionObservable : IObservable<Transaction>
    {
        private List<IObserver<Transaction>> observers;

        public TransactionObservable()
        {
            observers = new List<IObserver<Transaction>>();
        }

        public IDisposable Subscribe(IObserver<Transaction> observer)
        {
            if (! observers.Contains(observer))
            {
                observers.Add(observer);
            }
            return new Unsubscriber(observers, observer);
        }

        private class Unsubscriber : IDisposable
        {
            private List<IObserver<Transaction>> _observers;
            private IObserver<Transaction> _observer;

            public Unsubscriber(List<IObserver<Transaction>> observers,
                 IObserver<Transaction> observer)
            {
                this._observers = observers;
                this._observer = observer;
            }

            public void Dispose()
            {
                if (_observer != null && _observers.Contains(_observer))
                    _observers.Remove(_observer);
            }
        }
    }

    public class MissingTokenJob : BackgroundService
    {
        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );

        public MissingTokenJob()
        {
            log.Info("Starting MissingTokenJob Service");
            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, reloadOnChange: true)
                .Build();

            var section = configuration.Get<TokenListConfig>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
