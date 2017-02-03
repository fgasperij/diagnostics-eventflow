using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ServiceModel;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Diagnostics.EventFlow.Outputs.EventFlowHost
{
    interface IEventFlowHostServiceContract
    {   
        [OperationContract]
        Guid StartSession(IConfiguration configuration);
        [OperationContract]
        void ReceiveBatch(Guid token, IReadOnlyCollection<EventData> events);
    }
}
