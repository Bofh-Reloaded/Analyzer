using System;
using Telegram.Bot;
using Telegram.Bot.Types;


namespace AnalyzerCore.Notifier
{
    public interface ITelegramNotifier
    {
        public void SendMessage(string text);
    }
}
