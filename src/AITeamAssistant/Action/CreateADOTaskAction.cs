using Microsoft.CognitiveServices.Speech;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Commerce;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AITeamAssistant.Action
{

    public class CreateADOTaskAction : IAction
    {
        string url = "https://dev.azure.com/tictactec";

        // Personal Access Token (PAT)
        string personalAccessToken;

        // Project name
        string project = "tictactech";

        public string ActionName => "CreateADOTask";
        private VssConnection vssConnection;
        private WorkItemTrackingHttpClient witClient;


        public CreateADOTaskAction(IConfiguration configuration)
        {
            personalAccessToken = configuration["ADO_PAT"];
            vssConnection = new VssConnection(new Uri(url), new VssBasicCredential(string.Empty, personalAccessToken));
            witClient = vssConnection.GetClient<WorkItemTrackingHttpClient>();
        }

        public async Task<ActionResponse> ExecuteAsync(Dictionary<string, object> parameters)
        {

            var taskTitle = parameters["title"].ToString();
            var taskDescription = parameters["description"].ToString();

            // Define the work item fields
            var patchDocument = new JsonPatchDocument();
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Title",
                Value = taskTitle
            });
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                Path = "/fields/System.Description",
                Value = taskDescription
            });

            // Create the work item
            WorkItem result = witClient.CreateWorkItemAsync(patchDocument, project, "Task").Result;

            var newUrl = ConvertUrl(result.Url);

            return new ActionResponse
            {
                AudioResponse = $"Task '{taskTitle}' created in Azure DevOps",
                TextResponse = $"Task '{taskTitle}' - {newUrl}"
            };
        }

        // Template for the action parameters
        public string GetActionTemplate()
        {
            return "[{ 'title': 'Task title', 'description': 'Task description' }]";
        }

        public string GetLLMPrompt()
        {
            return "Create one or more deliverable and format them in below template ";
        }

        private string ConvertUrl(string originalUrl)
        {
            // Step 1: Extract necessary parts from the original URL
            Uri uri = new Uri(originalUrl);

            // Extract the organization and project name from the URL
            string[] segments = uri.Segments;

            if (segments.Length < 8)
                throw new ArgumentException("The URL format is incorrect.");

            string organization = segments[2].TrimEnd('/');
            string workItemId = segments[7].TrimEnd('/');

            // Step 2: Format the new URL
            string newUrl = $"https://dev.azure.com/{organization}/{organization}/_workitems/edit/{workItemId}";

            return newUrl;
        }

       


    }


}
