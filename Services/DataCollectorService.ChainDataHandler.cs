using System;
using System.Collections.Generic;

namespace AnalyzerCore.Services
{
    public partial class DataCollectorService
    {
        public class ChainDataHandler : IObservable<ChainData>
        {
            private readonly List<IObserver<ChainData>> _observers;

            public ChainDataHandler()
            {
                _observers = new List<IObserver<ChainData>>();
            }

            private ChainData ChainData { get; set; }

            public IDisposable Subscribe(IObserver<ChainData> observer)
            {
                // Check whether observer is already registered. If not, add it
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                    // Provide observer with existing data.
                    observer.OnNext(ChainData);
                }

                return new Unsubscriber<ChainData>(_observers, observer);
            }

            public void DataChange(ChainData newData)
            {
                ChainData = newData;
                foreach (var o in _observers)
                {
                    o.OnNext(ChainData);
                }
            }

            public void EndStream()
            {
                foreach (var observer in _observers)
                    observer.OnCompleted();

                _observers.Clear();
            }

            private class Unsubscriber<ChainData> : IDisposable
            {
                private readonly IObserver<ChainData> _observer;
                private readonly List<IObserver<ChainData>> _observers;

                internal Unsubscriber(
                    List<IObserver<ChainData>> observers,
                    IObserver<ChainData> observer)
                {
                    _observers = observers;
                    _observer = observer;
                }

                public void Dispose()
                {
                    if (_observers.Contains(_observer))
                        _observers.Remove(_observer);
                }
            }
        }
    }
}