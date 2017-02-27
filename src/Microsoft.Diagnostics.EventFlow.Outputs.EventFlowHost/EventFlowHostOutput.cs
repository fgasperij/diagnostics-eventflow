using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.EventFlowHost;
using System.ServiceModel;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutput : IOutput
    {
        private readonly IHealthReporter healthReporter;
        private IEventFlowHostServiceContract eventFlowHostChannel;
        private Guid token;

        public EventFlowHostOutput(IConfiguration config, IHealthReporter healthReporter)
        {
            this.healthReporter = healthReporter;

            try
            {
                string address = "net.pipe://localhost/eventflowhost/input";
                Console.WriteLine("I'm registering myself to EventFlowHost.");
                NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
                EndpointAddress ep = new EndpointAddress(address);
                this.eventFlowHostChannel = ChannelFactory<IEventFlowHostServiceContract>.CreateChannel(binding, ep);
                string jsonConfig = JsonConvert.SerializeObject(config);
                this.token = eventFlowHostChannel.StartSession(jsonConfig);
                Console.WriteLine("This is my token: {0}, I'm registered!", token);
            }
            catch
            {
                healthReporter.ReportProblem("Couldn't start a session with EventFlowHost.");
                throw;
            }
        }
        public Task SendEventsAsync(IReadOnlyCollection<EventData> events, long transmissionSequenceNumber, CancellationToken cancellationToken)
        {
            Console.WriteLine("Sending a batch of events to the Host.");
            IReadOnlyCollection<string> serializedEvents = events.Select(e => JsonConvert.SerializeObject(e)).ToList();
            this.eventFlowHostChannel.ReceiveBatch(token, JsonConvert.SerializeObject(serializedEvents));

            return Task.FromResult<object>(null);
        }
    }
}