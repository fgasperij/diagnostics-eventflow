using Microsoft.Diagnostics.EventFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string configPath = "C:\\Users\\t-fegasp\\projects\\diagnostics-eventflow\\ClientApp\\eventFlowConfig.json";
            using (var pipeline = DiagnosticPipelineFactory.CreatePipeline(configPath))
            {
                int iterations = 0;
                while (true)
                {
                    System.Diagnostics.Trace.TraceWarning($"Working-{++iterations}");
                    System.Threading.Sleep(100);
                }

            }
        }
    }
}
