// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.22.0

using API.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace EchoBot.Bots
{
    public class TextBot : ActivityHandler
    {
        private IOpenAIService openAIService;
        public TextBot(IOpenAIService openAIService)
        {
            this.openAIService = openAIService;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var question = turnContext.Activity.RemoveRecipientMention();
            var answer = this.openAIService.Ask(question);
            await turnContext.SendActivityAsync(MessageFactory.Text(answer, answer), cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
