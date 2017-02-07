using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.Outputs.EventFlowHost;
using System.ServiceModel;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutput : IOutput
    {
        private readonly IHealthReporter healthReporter;
        private IEventFlowHostServiceContract eventFlowHostChannel;
        private Guid token;

        public EventFlowHostOutput(IConfiguration outputsConfiguration, IHealthReporter healthReporter)
        {
            this.healthReporter = healthReporter;

            try
            {
                string address = "net.pipe://localhost/eventflowhost/input";

                NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
                EndpointAddress ep = new EndpointAddress(address);
                this.eventFlowHostChannel = ChannelFactory<IEventFlowHostServiceContract>.CreateChannel(binding, ep);

                this.token = eventFlowHostChannel.StartSession(outputsConfiguration);
            }
            catch
            {
                healthReporter.ReportProblem("Couldn't start a session with EventFlowHost.");
                throw;
            }
        }
        public Task SendEventsAsync(IReadOnlyCollection<EventData> events, long transmissionSequenceNumber, CancellationToken cancellationToken)
        {
            this.eventFlowHostChannel.ReceiveBatch(token, events);

            return Task.FromResult<object>(null);
        }
    }
}