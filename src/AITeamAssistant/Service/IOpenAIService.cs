using OpenAI.Chat;

namespace API.Services.Interfaces
{
    public interface IOpenAIService
    {
        string Ask(string question);

        string Ask(List<ChatMessage> chatMessages);

    }
}
