﻿using OpenAI.Chat;

namespace API.Services.Interfaces
{
    public interface IOpenAIService
    {
        Task<string> Ask(string question);

        Task<string> Ask(List<ChatMessage> chatMessages);

        Task<string> DetectActionFromPrompt(string question, List<string> actionList);

        Task<string> GatherActionParametersFromConversation(List<ChatMessage> chatMessages, string format, string userPrompt);

    }
}
