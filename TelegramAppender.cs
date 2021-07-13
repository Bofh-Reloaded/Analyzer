using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Core;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace AnalyzerCore
{
    public class TelegramAppender : AppenderSkeleton
    {
        private static TelegramBotClient _bot;

        private readonly List<Task> _tasks = new List<Task>();

        public TelegramAppender(string token, string chatId, ParseMode parseMode)
        {
            Token = token;
            ChatId = chatId;
            ParseMode = parseMode;
        }

        private string Token { get; }

        private string ChatId { get; }

        private ParseMode ParseMode { get; }

        protected override void Append(LoggingEvent e)
        {
            if (string.IsNullOrEmpty(Token))
                throw new ConfigurationErrorsException(
                    "Please set the Token under TelegramAppender configuration section: <Token>...</Token>");

            if (string.IsNullOrEmpty(ChatId))
                throw new ConfigurationErrorsException(
                    "Please set the ChatId under TelegramAppender configuration section: <ChatId>...</ChatId>");
            _bot ??= new TelegramBotClient(Token);
            var message = Layout == null ? e.RenderedMessage : RenderLoggingEvent(e);
            _tasks.Add(_bot.SendTextMessageAsync(ChatId, message, ParseMode));
        }

        protected override void OnClose()
        {
            Task.WaitAll(_tasks.Where(x => !x.IsCompleted).ToArray());
        }
    }
}