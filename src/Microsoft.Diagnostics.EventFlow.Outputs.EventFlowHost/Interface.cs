using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Microsoft.Diagnostics.EventFlow.Outputs.EventFlowHost
{
    public interface IEventFlowHostRemote : IService
    {
        Task<string> StartSession(IConfiguration configuration);
        Task<string> ReceiveBatch(string token, IReadOnlyCollection<EventData> events);
    }
}
