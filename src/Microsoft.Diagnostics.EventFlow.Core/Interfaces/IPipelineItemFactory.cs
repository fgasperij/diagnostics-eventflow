﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using Microsoft.Extensions.Configuration;

namespace Microsoft.Diagnostics.EventFlow
{
    public interface IPipelineItemFactory<out ItemType>
    {
        ItemType CreateItem(IConfiguration configuration, IHealthReporter healthReporter);
    }

    public interface IEventFlowOutputItemFactory<out ItemType>
    {
        ItemType CreateItem(string configuration, IHealthReporter healthReporter);
    }
}
