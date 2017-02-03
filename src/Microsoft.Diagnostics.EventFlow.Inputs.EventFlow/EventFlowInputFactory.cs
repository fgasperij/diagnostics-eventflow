// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Diagnostics.EventFlow.Inputs
{
    public class EventFlowInputFactory : IPipelineItemFactory<EventFlowInput>
    {
        public EventFlowInput CreateItem(IConfiguration configuration, IHealthReporter healthReporter)
        {
            Validation.Requires.NotNull(configuration, nameof(configuration));
            Validation.Requires.NotNull(healthReporter, nameof(healthReporter));

            return new EventFlowInput(configuration, healthReporter);
        }
    }
}
