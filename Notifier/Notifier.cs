using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Threading.Tasks;
using log4net;
using System.Reflection;

namespace AnalyzerCore.Notifier
{
    public class TelegramNotifier
    {
        private TelegramBotClient _Bot = new TelegramBotClient(
            token: "1780013642:AAH2nN3rFtRFLQzh4dHd1gjNTdwGWFHrYL8"
            );
        private readonly ChatId _chatId = new ChatId(identifier: -560874043);
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public TelegramNotifier()
        {
        }

        public TelegramNotifier(string chatId)
        {
            this._chatId = chatId;
        }

        public async void SendMessage(string text)
        {
            log.Debug(text);
            Message result = await _Bot.SendTextMessageAsync(
                            chatId: this._chatId,
                            text: text,
                            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
                            );
        }
    }
}
