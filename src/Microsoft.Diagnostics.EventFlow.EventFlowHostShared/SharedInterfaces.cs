using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.Extensions.Configuration;


namespace Microsoft.Diagnostics.EventFlow.EventFlowHostShared
{
    public interface IEventFlowHostRemote : IService
    {
        Task<string> StartSession(IConfiguration configuration);
        Task<string> ReceiveBatch(string token, IReadOnlyCollection<EventData> events);
    }
}