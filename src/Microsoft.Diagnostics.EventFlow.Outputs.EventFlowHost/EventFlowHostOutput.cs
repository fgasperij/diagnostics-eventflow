using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.Diagnostics.EventFlow.Outputs.EventFlowHost;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutput : IOutput
    {
        private readonly IHealthReporter healthReporter;
        private IEventFlowHostRemote eventFlowHost;
        private string token;

        public static Task<EventFlowHostOutput> CreateAsync(IConfiguration outputsConfiguration, IHealthReporter healthReporter)
        {
            var ret = new EventFlowHostOutput(healthReporter);
            return ret.InitializeAsync(outputsConfiguration, healthReporter);
        }
        private async Task<EventFlowHostOutput> InitializeAsync(IConfiguration outputsConfiguration, IHealthReporter healthReporter)
        {
            try
            {
                IEventFlowHostRemote eventFlowHost = ServiceProxy.Create<IEventFlowHostRemote>(new Uri("fabric:/EventFlowHostApp/EventFlowHostService"));
                this.token = await eventFlowHost.StartSession(outputsConfiguration);
            }
            catch
            {
                healthReporter.ReportProblem("Couldn't start a session with EventFlowHost.");
                throw;
            }

            return this;
        }
        private EventFlowHostOutput(IHealthReporter healthReporter)
        {
            this.healthReporter = healthReporter;
        }
        public Task SendEventsAsync(IReadOnlyCollection<EventData> events, long transmissionSequenceNumber, CancellationToken cancellationToken)
        {
            eventFlowHost.ReceiveBatch(token, events);

            return Task.FromResult<object>(null);
        }
    }
}