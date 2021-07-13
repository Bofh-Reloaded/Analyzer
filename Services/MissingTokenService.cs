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
        private readonly List<IObserver<Transaction>> _observers;

        public TransactionObservable()
        {
            _observers = new List<IObserver<Transaction>>();
        }

        public IDisposable Subscribe(IObserver<Transaction> observer)
        {
            if (!_observers.Contains(observer)) _observers.Add(observer);
            return new Unsubscriber(_observers, observer);
        }

        private class Unsubscriber : IDisposable
        {
            private readonly IObserver<Transaction> _observer;
            private readonly List<IObserver<Transaction>> _observers;

            public Unsubscriber(List<IObserver<Transaction>> observers,
                IObserver<Transaction> observer)
            {
                _observers = observers;
                _observer = observer;
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
        private readonly ILog _log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType
        );

        public MissingTokenJob()
        {
            _log.Info("Starting MissingTokenJob Service");
            // Load configuration regarding tokens
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("tokens.json", false, true)
                .Build();

            var section = configuration.Get<TokenListConfig>();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested) await Task.Delay(10000, stoppingToken);
        }
    }
}