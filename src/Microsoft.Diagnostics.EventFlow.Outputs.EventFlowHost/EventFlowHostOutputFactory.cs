﻿using Microsoft.Extensions.Configuration;
using Validation;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.EventFlow.Outputs
{
    public class EventFlowHostOutputFactory : IPipelineItemFactory<EventFlowHostOutput>
    {
        public EventFlowHostOutput CreateItem(IConfiguration outputsConfiguration, IHealthReporter healthReporter)
        {
            Requires.NotNull(healthReporter, nameof(healthReporter));
            Task<EventFlowHostOutput> outputTask = EventFlowHostOutput.CreateAsync(outputsConfiguration, healthReporter);
            EventFlowHostOutput output = outputTask.Result;
            return output;
        }
    }
}