// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.22.0

using AITeamAssistant.Action;
using API.Services;
using API.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Graph.Communications.Common;
using OpenAI.Chat;

namespace EchoBot.Bots
{
    public class TextBot : ActivityHandler
    {
        private IOpenAIService _openAIService;
        private ActionDispatcher actionDispatcher;

        public TextBot(IOpenAIService openAIService, ActionDispatcher actionDispatcher)
        {
            this._openAIService = openAIService;
            this.actionDispatcher = actionDispatcher;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var questionPrompt = turnContext.Activity.RemoveRecipientMention();
            //var answer = await this.openAIService.Ask(question);

            var action = await _openAIService.DetectActionFromPrompt(questionPrompt, this.actionDispatcher.GetActionNames());

            var response = string.Empty;
            if (ActionDispatcher.NoActionFound.EqualsIgnoreCase(action))
            {
                response = await this._openAIService.Ask(questionPrompt);
            }
            else
            {
                List<ChatMessage> chatMessages = new List<ChatMessage>()
                {
                    new UserChatMessage(questionPrompt),
                };
                var actionResponse = await actionDispatcher.DispatchActionAsync(action, chatMessages);
                response = actionResponse.TextResponse;
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(response, response), cancellationToken);
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
