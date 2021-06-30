using log4net;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
/*
namespace AnalyzerCore.Services
{
    class TransactionRetrieverObservableService : BackgroundService : IObservable<SomeEvent>
    {
        public HttpClient client = new HttpClient();

        private readonly ILog log = LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType
            );
        private BscScan bscScanApiClient = new BscScan();
        int taskDelayMs = 120000; 

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {

                await Task.Delay(taskDelayMs, stoppingToken);
            }
        }
    }
    public class NotificationProvider : IObservable<SomeEvent>
{
    // Maintain a list of observers
    private List<IObserver<SomeEvent>> _observers;
    public NotificationProvider()
    {
        _observers = new List<IObserver<SomeEvent>>();
    }
    // Define Unsubscriber class
    private class Unsubscriber : IDisposable
    {
        private List<IObserver<SomeEvent>> _observers;
        private IObserver<SomeEvent> _observer;
        public Unsubscriber(List<IObserver<SomeEvent>> observers,
                            IObserver<SomeEvent> observer)
        {
            this._observers = observers;
            this._observer = observer;
        }
        public void Dispose()
        {
            if (!(_observer == null)) _observers.Remove(_observer);
        }
    }
    // Define Subscribe method
    public IDisposable Subscribe(IObserver<SomeEvent> observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
        return new Unsubscriber(_observers, observer);
    }
    // Notify observers when event occurs
    public void NotificationEvent(string description)
    {
        foreach (var observer in _observers)
        {
            observer.OnNext(new SomeEvent(description,
                            DateTime.Now));
        }
    }
}
}

*/