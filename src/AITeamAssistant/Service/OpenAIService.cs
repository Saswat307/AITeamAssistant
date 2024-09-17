using AITeamAssistant.Action;
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

        private string ActionDetectSystemPromptTemplate =
            @$"You are an intelligent assistant. Your job is to analyze the input prompt and determine if the user is asking to perform any action. " +
            "Available actions are: [{0}]. " +
            "If the prompt asks for one of these actions, return the action name only. " +
            "If the prompt does not ask for any of the actions, return 'NO_ACTION'.";

        private string ADOActionPromptTemplate =
            "You are an AI assistant, and your task is to create one or more actionable Azure DevOps (ADO) work items. " +
            "Each work item should be clear, concise, and specific, detailing the task to be performed." +
            " Ensure that each work item includes a title and a brief description, and the output should be in the following format" +
            "{0}";

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

        public async Task<string> Ask(string question)
        {
            _logger.LogInformation("Question " + question);
            List<ChatMessage> chatMessages = new List<ChatMessage>()
            {
                new SystemChatMessage(SystemMessage),
                new UserChatMessage(question),
            };

            ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages);
            _logger.LogInformation("Answer " + completion.Content[0].Text);
            return completion.Content[0].Text;
        }

        public async Task<string> Ask(List<ChatMessage> chatMessages)
        {

            OpenAI.Chat.ChatMessage systemMessage = chatMessages[0];
            if (systemMessage is not SystemChatMessage)
            {
                _logger.LogInformation("Adding System Message");
                chatMessages.Insert(0, new SystemChatMessage(SystemMessage));
            }

            foreach (var chatMessage in chatMessages)
            {
                if (chatMessage.Content.Count > 0)
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

            ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages);

            _logger.LogInformation("Chat Answer " + completion.Content[0].Text);
            return completion.Content[0].Text;
        }

        public async Task<string> DetectActionFromPrompt(string inputPrompt, List<string> implementedActions)
        {
            _logger.LogInformation("Input Prompt to Detect Action - " + inputPrompt);
            // Get the list of actions dynamically

            var implemptedActionNames = string.Join(", ", implementedActions);

            string actionDetectionPrompt = string.Format(ActionDetectSystemPromptTemplate, implemptedActionNames);

            List<ChatMessage> chatMessages = new List<ChatMessage>()
            {
                new SystemChatMessage(actionDetectionPrompt),
                new UserChatMessage(inputPrompt)
            };

            ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages);
            _logger.LogInformation("Detected Action -" + completion.Content[0].Text);
            string action = completion.Content[0].Text.Trim();

            if(implementedActions.Contains(action))
            {
                return action;
            }

            return ActionDispatcher.NoActionFound;
        }

        public async Task<string> GatherActionParametersFromConversation(List<ChatMessage> chatMessages, string format, string userPrompt)
        {

            string actionParameterPrompt = string.Format(ADOActionPromptTemplate, format);

            ChatMessage systemMessage = chatMessages[0];
            if (systemMessage is SystemChatMessage)
            {
                _logger.LogInformation("Removing if any existing System Message");
                chatMessages.Insert(0, new SystemChatMessage(actionParameterPrompt));
            }
            chatMessages.Insert(0, new SystemChatMessage(actionParameterPrompt));
            ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages);
            string parameterString = completion.Content[0].Text.Trim();

            return parameterString;
        }
    }
}
