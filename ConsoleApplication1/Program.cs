using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.EventFlow;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var pipeline = DiagnosticPipelineFactory.CreatePipeline("C:\\Users\\t-fegasp\\projects\\diagnostics-eventflow\\ConsoleApplication1\\eventFlowConfig.json"))
            {
                foreach (int i in Enumerable.Range(0, 10000000))
                {
                    System.Diagnostics.Trace.TraceWarning($"Event {i}");
                }
                Console.ReadLine();
            }
        }
    }
}
