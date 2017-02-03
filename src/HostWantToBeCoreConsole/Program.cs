using System;
using System.ServiceModel;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost
{
    class Program
    {
        static void Main(string[] args)
        {            
            string address = "net.pipe://localhost/eventflowhost/input";
            ServiceHost serviceHost = new ServiceHost(typeof(EventFlowHostRemoteService));
            NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            serviceHost.AddServiceEndpoint(typeof(IEventFlowHostServiceContract), binding, address);
            serviceHost.Open();

            Console.WriteLine("EventFlowHostRemoteService running.");

            using (var pipeline = DiagnosticPipelineFactory.CreatePipeline("C:\\Users\\t-fegasp\\projects\\event-flow-host\\src\\Microsoft.Diagnostics.EventFlow.Host\\eventFlowConfig.json"))
            {
                Console.ReadLine();
            }
        }
    }
}
