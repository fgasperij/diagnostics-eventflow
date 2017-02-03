﻿using System;
using Moq;
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Diagnostics.EventFlow.Outputs;

namespace Microsoft.Diagnostics.EventFlow.Outputs.Tests
{
    public class EventFlowOutputTests
    {
        [Fact]
        public void ConstructorShouldCreateTheInstance()
        {
            // Setup
            Mock<IHealthReporter> healthReporterMock = new Mock<IHealthReporter>();
            var configurationMock = new Mock<IConfiguration>();

            // Execute
            EventFlowHostOutput sender = new EventFlowHostOutput(configurationMock, healthReporterMock.Object);

            // Verify
            Assert.NotNull(sender);
        }

        [Fact]
        public void ConstructorShouldRequireHealthReporter()
        {
            var configurationMock = new Mock<IConfiguration>();

            Exception ex = Assert.Throws<ArgumentNullException>(() =>
            {
                EventFlowHostOutput target = new EventFlowHostOutput(configurationMock, null);
            });

            Assert.Equal("Value cannot be null.\r\nParameter name: healthReporter", ex.Message);
        }
    }
}
