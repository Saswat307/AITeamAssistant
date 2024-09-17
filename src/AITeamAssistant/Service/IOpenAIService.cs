using OpenAI.Chat;

namespace AITeamAssistant.Service
{
    public interface IOpenAIService
    {
        string Ask(string question);

        string Ask(List<ChatMessage> chatMessages);

    }
}
