using Microsoft.Diagnostics.EventFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventFlowClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var pipeline = DiagnosticPipelineFactory.CreatePipeline("C:\\Users\\t-fegasp\\projects\\diagnostics-eventflow\\src\\EventFlowClientApp\\eventFlowConfig.json");
            while (true)
            {
                //using (var pipeline = DiagnosticPipelineFactory.CreatePipeline("C:\\Users\\t-fegasp\\projects\\diagnostics-eventflow\\src\\EventFlowClientApp\\eventFlowConfig.json"))
                //{
                  System.Diagnostics.Trace.TraceWarning("EventFlow is working!");
                //}
                // Console.WriteLine("something something bla bla bla");
                System.Threading.Thread.Sleep(100);
            }
        }
    }
}
