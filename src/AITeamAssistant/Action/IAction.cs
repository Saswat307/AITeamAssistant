using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AITeamAssistant.Action
{
    internal interface IAction
    {
        string ActionName { get; }
        Task<ActionResponse> ExecuteAsync(Dictionary<string, object> parameters);
        string GetActionTemplate(); 
        string GetLLMPrompt();
    }
}
