#if NET46
/*
using System;
using Moq;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.Outputs;
using System.ServiceModel;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Tests
{
    public class EventFlowOutputTests
    {
        [Fact]
        public void ConstructorShouldCreateTheInstance()
        {
            // Setup
            Mock<IHealthReporter> healthReporterMock = new Mock<IHealthReporter>();
            Mock<IConfiguration> configurationMock = new Mock<IConfiguration>();

            string address = "net.pipe://localhost/eventflowhost/input";
            ServiceHost serviceHost = new ServiceHost(typeof(EventFlowHostRemoteService));
            NetNamedPipeBinding binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.None);
            serviceHost.AddServiceEndpoint(typeof(IEventFlowHostServiceContract), binding, address);
            serviceHost.Open();

            // Execute
            EventFlowHostOutput sender = new EventFlowHostOutput(configurationMock.Object, healthReporterMock.Object);

            // Verify
            Assert.NotNull(sender);
        }

        [Fact]
        public void ConstructorShouldRequireHealthReporter()
        {
            Mock<IConfiguration> configurationMock = new Mock<IConfiguration>();

           
            Exception ex = Assert.Throws<ArgumentNullException>(() =>
            {
                EventFlowHostOutput target = new EventFlowHostOutput(configurationMock.Object, null);
            });
           

            Assert.Equal("Value cannot be null.\r\nParameter name: healthReporter", ex.Message);
        }
    }
}
*/
#endif