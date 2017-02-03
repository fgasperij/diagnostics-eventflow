// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.EventFlowHost;


namespace Microsoft.Diagnostics.EventFlow.Inputs
{
    public class EventFlowInput : IObservable<EventData>, IObserver<EventData>
    {
        private EventFlowSubject<EventData> subject;
        private readonly IHealthReporter healthReporter;

        public EventFlowInput(IConfiguration configuration, IHealthReporter healthReporter)
        {
            Validation.Requires.NotNull(configuration, nameof(configuration));
            Validation.Requires.NotNull(healthReporter, nameof(healthReporter));

            this.healthReporter = healthReporter;
            // EventFlowHostRemoteService.input = this;
            this.subject = new EventFlowSubject<EventData>();
            this.healthReporter.ReportHealthy("EventFlowInput initialized.");
        }
        public IDisposable Subscribe(IObserver<EventData> observer)
        {
            return this.subject.Subscribe(observer);
        }
        public void OnCompleted()
        {
            this.subject.OnCompleted();
        }
        public void OnError(Exception e)
        {
            this.subject.OnError(e);
        }
        public void OnNext(EventData value)
        {
            this.subject.OnNext(value);
        }
    }
}
