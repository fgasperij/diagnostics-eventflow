using Microsoft.Extensions.Configuration;
using Validation;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutputFactory : IPipelineItemFactory<EventFlowHostOutput>
    {
        public EventFlowHostOutput CreateItem(IConfiguration config, IHealthReporter healthReporter)
        {
            Requires.NotNull(healthReporter, nameof(healthReporter));
            EventFlowHostOutput output = new EventFlowHostOutput(config, healthReporter);

            return output;
        }
    }
}