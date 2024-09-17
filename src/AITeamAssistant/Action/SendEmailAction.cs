using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AITeamAssistant.Action
{
    public class SendEmailAction : IAction
    {
        public string ActionName => "SendEmail";

        public async Task<ActionResponse> ExecuteAsync(Dictionary<string, object> parameters)
        {
            string subject;
            if (!parameters.TryGetValue("subject", out var subjectValue) || string.IsNullOrEmpty(subjectValue?.ToString()))
            {
                subject = "Default Mail Subject"; // Provide a default value
            }
            else
            {
                subject = subjectValue.ToString();
            }


//            var message = parameters["message"].ToString();
            // Send email logic

            return new ActionResponse
            {
                AudioResponse = $"Email sent -  {subject}",
                TextResponse = $"Email sent -  {subject}"
            };
            
        }

        // Template that OpenAI can use to fill in the details
        public string GetActionTemplate()
        {
            return "{ 'subject' : 'Your subject here' , 'message': 'Your message here' }";
        }

        public string GetLLMPrompt()
        {
            throw new NotImplementedException();
        }
    }
}
