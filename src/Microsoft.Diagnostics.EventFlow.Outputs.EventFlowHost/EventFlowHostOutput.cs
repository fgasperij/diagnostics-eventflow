using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.EventFlowHost;
using System.ServiceModel;
using System.Linq;
using Newtonsoft.Json;
using Validation;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutput : IOutput, IDisposable
    {
        private readonly IHealthReporter healthReporter;
        private IEventFlowHostServiceContract eventFlowHostChannel;
        private Guid token;

        public EventFlowHostOutput(string config, IHealthReporter healthReporter)
        {
            Requires.NotNull(config, nameof(config));
            Requires.NotNull(healthReporter, nameof(healthReporter));

            this.healthReporter = healthReporter;

            try
            {
                string address = "net.pipe://localhost/eventflowhost/input";
                NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
                EndpointAddress ep = new EndpointAddress(address);
                this.eventFlowHostChannel = ChannelFactory<IEventFlowHostServiceContract>.CreateChannel(binding, ep);
                token = eventFlowHostChannel.StartSession(config);
            }
            catch (Exception e)
            {
                string errorMessage = $"{nameof(EventFlowHostOutput)} could not start a session with EventFlowHost: {e.ToString()}";
                healthReporter.ReportProblem(errorMessage);

                throw new Exception(errorMessage);
            }
        }
        public Task SendEventsAsync(IReadOnlyCollection<EventData> events, long transmissionSequenceNumber, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<string> serializedEvents = events.Select(e => JsonConvert.SerializeObject(e)).ToList();
            try
            {
                this.eventFlowHostChannel.ReceiveBatch(token, JsonConvert.SerializeObject(serializedEvents));
            }
            catch (Exception e)
            {
                string errorMessage = $"{nameof(EventFlowHostOutput)} could not send a batch of {events.Count} event to EventFlowHost: {e.ToString()}";
                healthReporter.ReportProblem(errorMessage);
            }

            return Task.FromResult<object>(null);
        }

        public void Dispose()
        {
            try
            {
                this.eventFlowHostChannel.EndSession(token);
            }
            catch (Exception e)
            {
                string errorMessage = $"{nameof(EventFlowHostOutput)} could not end its session with EventFlowHost: {e.ToString()}";
                healthReporter.ReportProblem(errorMessage);
            }
        }
    }
}