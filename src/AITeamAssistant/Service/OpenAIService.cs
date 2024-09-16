using API.Services.Interfaces;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;


namespace API.Services
{
    public class OpenAIService : IOpenAIService
    {
        private string SystemMessage = "You are TeamMate AI, an employee assistant bot named Max. " +
    "In meetings, your responses will be used as audio output, so keep your answers brief and clear. " +
    "Respond concisely, ideally under 250 words. If the question is unclear or you don’t know the answer, politely say you don’t know. " +
    "Focus on straightforward and helpful responses, avoiding unnecessary details to ensure compatibility with audio channels.";

        private readonly IConfiguration _configuration;
        private ChatClient chatClient;
        private ILogger _logger;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger)
        {
            _logger = logger;
            _configuration = configuration;
            string keyFromEnvironment = _configuration["AZURE_OPENAI_API_KEY"];
            string uriFromEnvironment = _configuration["AZURE_OPENAI_ENDPOINT"];
            string modelFromEnvironment = _configuration["AZURE_OPENAI_MODEL"];

            
            AzureOpenAIClient azureClient = new(
                new Uri(uriFromEnvironment),
                new AzureKeyCredential(keyFromEnvironment));
            chatClient = azureClient.GetChatClient(modelFromEnvironment);
        }

        public string Ask(string question)
        {
            _logger.LogInformation("Question " + question);
            List<OpenAI.Chat.ChatMessage> chatMessages = new List<OpenAI.Chat.ChatMessage>()
            {
                new SystemChatMessage(SystemMessage),
                new UserChatMessage(question),
            };

            ChatCompletion completion = chatClient.CompleteChat(chatMessages);
            _logger.LogInformation("Answer " + completion.Content[0].Text);
            return completion.Content[0].Text;
        }

        public string Ask(List<OpenAI.Chat.ChatMessage> chatMessages)
        {

            OpenAI.Chat.ChatMessage systemMessage = chatMessages[0];
            if (systemMessage is not SystemChatMessage)
            {
                _logger.LogInformation("Adding System Message");
                chatMessages.Insert(0, new SystemChatMessage(SystemMessage));
            }
           
            foreach (var chatMessage in chatMessages)
            {
                if(chatMessage.Content.Count > 0)
                {

                    // Check if the message is from a user
                    if (chatMessage is UserChatMessage userChatMessage)
                    {
                        _logger.LogInformation("User Message: " + userChatMessage.Content[0]);
                    }
                    // Check if the message is from the system
                    else if (chatMessage is SystemChatMessage systemChatMessage)
                    {
                        _logger.LogInformation("System Message: " + systemChatMessage.Content[0]);
                    }
                    // You can add additional roles if necessary (e.g., assistant messages)
                    else if (chatMessage is AssistantChatMessage assistantChatMessage)
                    {
                        _logger.LogInformation("Assistant Message: " + assistantChatMessage.Content[0]);
                    }
                }
            }

            ChatCompletion completion = chatClient.CompleteChat(chatMessages);

            _logger.LogInformation("Chat Answer " + completion.Content[0].Text);
            return completion.Content[0].Text;
        }
    }
}
