namespace AnalyzerCore.Notifier
{
    public interface ITelegramNotifier
    {
        public void SendMessage(string text);
    }
}