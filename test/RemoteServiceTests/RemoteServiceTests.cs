using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Diagnostics.EventFlow.EventFlowHost;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost.RemoteService.Tests
{
    public class RemoteServiceTests
    {
        [Fact]
        public void ShouldStartASession()
        {
            string config = @"
            {
                ""inputs"": [
                    {
                        ""type"": ""Trace""
                    }
                ],
                ""outputs"": [
                    {
                        ""type"": ""StdOutput""
                    }
                ],
                ""schemaVersion"": ""2016-08-11""
            }";
            EventFlowHostRemoteService service = new EventFlowHostRemoteService();
            var ex = Record.Exception(() => service.StartSession(config) );
            Assert.Null(ex);
            
        }
    }
}
