using System;
using System.Collections.Generic;
using System.ServiceModel;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost
{
    [ServiceContract(Namespace = "eventflow/eventflowhost")]
    public interface IEventFlowHostServiceContract
    {   
        [OperationContract]
        Guid StartSession(string configuration);
        [OperationContract]
        void ReceiveBatch(Guid token, string events);
    }
}
