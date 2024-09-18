// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.22.0

using AITeamAssistant.Service;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace AITeamAssistant.Bot
{
    public class TextBot : ActivityHandler
    {
        private readonly IOpenAIService openAIService;
        private readonly IPromptFlowService promptFlowService;

        public TextBot(IOpenAIService openAIService, IPromptFlowService promptFlowService)
        {
            this.openAIService = openAIService;
            this.promptFlowService = promptFlowService;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var question = turnContext.Activity.RemoveRecipientMention();
            //var answer = await this.openAIService.Ask(question);
            var answer = await promptFlowService.GetResponseAsync(question, new ChatHistory() { Interactions = new List<ChatInteraction>() }); // @TODO Get the Context.
            await turnContext.SendActivityAsync(MessageFactory.Text(answer, answer), cancellationToken);
        }

       /* public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == "meeting")
            {
                var meetingInfo = await meetingService.GetMeetingInfoAsync(turnContext.Activity.ChannelId); // @TODO;
                *//*if (meetingInfo != null)
                    await JoinMeetingAsync(meetingInfo.JoinWebUrl);*//*
            }

            await base.OnTurnAsync(turnContext, cancellationToken);
        }
*/

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
