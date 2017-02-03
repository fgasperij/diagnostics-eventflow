using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.ServiceModel;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class EventFlowHostRemoteService : IEventFlowHostServiceContract
    {
        public static IHealthReporter healthReporter;
        // TODO: should input satisfy an interface? Couldn't get it to be an EventFlowInput due to references problems.
        public static IObserver<EventData> input;
        private Dictionary<Guid, List<string>> outputTypesByToken;
        public Guid StartSession(IConfiguration configuration)
        {
            Guid token = Guid.NewGuid();
            List<IConfigurationSection> configurationSections = configuration.GetChildren().ToList();
            List<string> outputTypes = new List<string>();
            foreach (IConfigurationSection configurationSection in configurationSections)
            {
                outputTypes.Add(configurationSection["Type"]);
            }
            outputTypesByToken.Add(token, outputTypes);
            return token;
        }
        public void ReceiveBatch(Guid token, IReadOnlyCollection<EventData> events)
        {
            if (EventFlowHostRemoteService.input != null)
            {
                // TODO: how can we take advantage of the fact that they are already batched?
                foreach (EventData anEvent in events)
                {
                    // TODO: should use `nameof(EventFlowInput)` instead of the string.
                    anEvent.AddPayloadProperty("Corresponding outputs", this.outputTypesByToken[token], healthReporter, "EventFlowInput");
                    EventFlowHostRemoteService.input.OnNext(anEvent);
                }
                
            }
        }
    }
}
