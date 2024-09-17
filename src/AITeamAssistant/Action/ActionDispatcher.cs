
using AITeamAssistant.Service;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System.Text;

namespace AITeamAssistant.Action
{
    public class ActionDispatcher
    {
        public static string NoActionFound = "NO_ACTION";

        private string adoPat;

        private List<string> actions;
        private readonly Dictionary<string, IAction> _actionsMap;

        private readonly IOpenAIService _openAIService;

        private List<string> FetchActions()
        {
           /* var actionType = typeof(IAction);
            var actionNames = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => actionType.IsAssignableFrom(p) && !p.IsAbstract && !p.IsInterface)
                .Select(t => t.Name.Replace("Action", ""))
                .ToList();*/
            List<string> actionNames = new List<string>
            {
                "SendEmail",
                "CreateADOTask"
            };

            return actionNames;
        }

        public List<string> GetActionNames()
        {
            if (actions == null)
            {
                actions = FetchActions();
            }
            return actions;
        }
        
        public ActionDispatcher(IOpenAIService openAIService, IConfiguration configuration)
        {
            _openAIService = openAIService;


            // Register all available actions here
            _actionsMap = new Dictionary<string, IAction>
            {
                { "SendEmail", new SendEmailAction() },
                { "CreateADOTask", new CreateADOTaskAction(configuration) }
            };
        }

        public async Task<ActionResponse> DispatchActionAsync(string actionName,List<ChatMessage> chatMessages)
        {
            
            IAction detectedAction = null;
            if (_actionsMap.TryGetValue(actionName, out var action))
            {
                detectedAction = action;
            }
            List<Dictionary<string, object>> parameters = await GatherParametersForAction(detectedAction, chatMessages);

            List<ActionResponse> actionResponses = new List<ActionResponse>();
            foreach (var parameter in parameters)
            {
                var actionResponse = await detectedAction.ExecuteAsync(parameter);
                actionResponses.Add(actionResponse);
            }

            ActionResponse mergedActionResponse = MergeActionResponses(actionResponses);

            return mergedActionResponse;
        }
        private async Task<List<Dictionary<string, object>>> GatherParametersForAction(IAction detectedAction, List<ChatMessage> chatMessages)
        {
            List<ChatMessage> chatMessagesCopy = chatMessages.ToList();
            var paramterString =  await _openAIService.GatherActionParametersFromConversation(chatMessages, detectedAction.GetActionTemplate(), detectedAction.GetLLMPrompt());
            List<Dictionary<string, object>> parameters = ParseParameterString(paramterString);

            return parameters;
        }

        private List<Dictionary<string, object>> ParseParameterString(string parameterString)
        {
            List<Dictionary<string, object>> parameters = new List<Dictionary<string, object>>();

            try
            {
                var jsonString = parameterString.Replace("'", "\"");

                // Parse the JSON string
                JArray jsonArray = JArray.Parse(jsonString);

                foreach (JObject item in jsonArray)
                {
                    string title = item["title"].ToString();
                    string description = item["description"].ToString();
                    Dictionary<string, object> parsedData = new Dictionary<string, object>
                        {
                            { "title", title },
                            { "description", description }
                        };
                    parameters.Add(parsedData);
                }


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Dictionary<string, object> fakeParsedData = new Dictionary<string, object>
                        {
                            { "title", "Create Web API" },
                            { "description", "Create Web API" }
                        };
                parameters.Add(fakeParsedData);
            }
            
            return parameters;
        }

        public ActionResponse MergeActionResponses(List<ActionResponse> responses)
        {
            if (responses == null || responses.Count == 0)
                return new ActionResponse();

            // Prepare the AudioResponse
            int taskCount = responses.Count;
            string audioSummary = $"{taskCount} ADO task{(taskCount > 1 ? "s have" : " has")} been created. Check the chat window for details.";

            // Prepare the TextResponse
            StringBuilder textSummary = new StringBuilder();
            textSummary.AppendLine($"Below {taskCount} task{(taskCount > 1 ? "s have" : " has")} been created:");
            
            // Append details of each task
            for (int i = 0; i < responses.Count; i++)
            {
                textSummary.AppendLine();
                textSummary.AppendLine($"{i + 1} - {responses[i].TextResponse}");
            }

            // Return the combined ActionResponse
            return new ActionResponse
            {
                AudioResponse = audioSummary,
                TextResponse = textSummary.ToString()
            };
        }
    }

    
}
