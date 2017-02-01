using Validation;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutputFactory : IEventFlowOutputItemFactory<EventFlowHostOutput>
    {
        public EventFlowHostOutput CreateItem(string config, IHealthReporter healthReporter)
        {
            Requires.NotNull(healthReporter, nameof(healthReporter));
            EventFlowHostOutput output = new EventFlowHostOutput(config, healthReporter);

            return output;
        }
    }
}