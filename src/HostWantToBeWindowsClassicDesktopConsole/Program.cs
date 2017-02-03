using Microsoft.Diagnostics.EventFlow.HealthReporters;
using Microsoft.Extensions.Configuration;
using System;
using System.ServiceModel;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost
{
    class Program
    {
        static void Main(string[] args)
        {
            // remote service configuration
            EventFlowHostRemoteService.MaxNumberOfBatchesInProgress = 20;
            EventFlowHostRemoteService.MaxConcurrency = 5;
            EventFlowHostRemoteService.healthReporter = new CsvHealthReporter(new ConfigurationBuilder().AddInMemoryCollection().Build());
            // ServiceHost set up
            string address = "net.pipe://localhost/eventflowhost/input";
            ServiceHost serviceHost = new ServiceHost(typeof(EventFlowHostRemoteService));
            NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            serviceHost.AddServiceEndpoint(typeof(IEventFlowHostServiceContract), binding, address);
            serviceHost.Open();

            Console.WriteLine("EventFlowHostRemoteService running.");

            while (true)
            {
                System.Threading.Thread.Sleep(10);
            }

            serviceHost.Close();
        }
    }
}
