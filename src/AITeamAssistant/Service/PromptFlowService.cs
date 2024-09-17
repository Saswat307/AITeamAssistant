using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace AITeamAssistant.Service
{
    public interface IPromptFlowService
    {
        public Task<string> GetResponseAsync(string chat_input, ChatHistory chat_history);
    }

    public class PromptFlowResponse
    {
        [JsonProperty("chat_output")]
        public string ChatOutput { get; set; }
    }

    public class ChatInteraction
    {
        [JsonProperty("inputs")]
        public ChatInputs Inputs { get; set; }

        [JsonProperty("outputs")]
        public ChatOutputs Outputs { get; set; }
    }

    public class ChatInputs
    {
        [JsonProperty("chat_input")]
        public string ChatInput { get; set; }
    }

    public class ChatOutputs
    {
        [JsonProperty("chat_output")]
        public string ChatOutput { get; set; }
    }

    public class ChatHistory
    {
        public List<ChatInteraction> Interactions { get; set; }
    }

    public class PromptInput
    {
        public string chat_input;
        public List<ChatInteraction> chat_history;

        public PromptInput(string chat_input, List<ChatInteraction> chat_history)
        {
            this.chat_input = chat_input;
            this.chat_history = chat_history;
        }
    }

    public class PromptFlowService : IPromptFlowService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PromptFlowService> _logger;

        public PromptFlowService(IHttpClientFactory httpClientFactory, ILogger<PromptFlowService> logger)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("AzurePromptFlowClient");
        }

        public async Task<string> GetResponseAsync(string chat_input, ChatHistory chat_history)
        {
            _logger.LogInformation("PromptFlow Chat Input " + chat_input);

            foreach(var interaction in chat_history.Interactions)
            {
                _logger.LogInformation("--------------------------------------");

                if (interaction.Inputs != null && interaction.Inputs.ChatInput != null)
                {
                    _logger.LogInformation("Input -  " + interaction.Inputs.ChatInput);
                }
                if(interaction.Outputs != null && interaction.Outputs.ChatOutput != null)
                {
                    _logger.LogInformation("Output -  " + interaction.Outputs.ChatOutput);
                }
                
            }
            _logger.LogInformation("--------------------------------------");

            var requestBody = new StringContent(JsonConvert.SerializeObject(new PromptInput( chat_input, chat_history.Interactions )), Encoding.UTF8, "application/json");
            requestBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _httpClient.PostAsync("/score", requestBody);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var promptFlowResponse = JsonConvert.DeserializeObject<PromptFlowResponse>(responseContent);
            return promptFlowResponse.ChatOutput;
        }
    }
}