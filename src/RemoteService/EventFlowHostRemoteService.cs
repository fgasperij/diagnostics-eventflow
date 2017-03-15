using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.ServiceModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Diagnostics.EventFlow.HealthReporters;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.EventFlow.Configuration;
using System.Diagnostics;
using Validation;
using System.IO;

namespace Microsoft.Diagnostics.EventFlow.EventFlowHost
{
    public class SharedOutput
    {
        // It holds the number of application pipelines currently using the output of this SharedOutput.
        public int referenceCounter;
        public ActionBlock<EventData[]> output { get; set; }
        // The key in the EventFlowHostRemoteService.outputs dictionary that holds this SharedOutput.
        public JObject key { get; set; }
        public CancellationTokenSource cancellationTokenSource { get; set; }

        public SharedOutput(JObject diccKey, ActionBlock<EventData[]> outputBlock, CancellationTokenSource outputCancellationTokenSource)
        {
            referenceCounter = 1;
            output = outputBlock;
            key = diccKey;
            cancellationTokenSource = outputCancellationTokenSource;
        }

        public void IncrementReferenceCounter()
        {
            Interlocked.Increment(ref referenceCounter);
        }

        public void DecrementReferenceCounter()
        {
            Interlocked.Decrement(ref referenceCounter);
        }
    }
    public class EventFlowHostRemoteService : IEventFlowHostServiceContract
    {
        public static IHealthReporter healthReporter;
        public static int MaxNumberOfBatchesInProgress;
        public static int MaxConcurrency;

        private static ConcurrentDictionary<JObject, SharedOutput> outputs = new ConcurrentDictionary<JObject, SharedOutput>(new JTokenEqualityComparer());
        private static ConcurrentDictionary<Guid, CancellationTokenSource> cancellationTokenSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private static ConcurrentDictionary<Guid, BufferBlock<EventData[]>> applicationPipelinesHeads = new ConcurrentDictionary<Guid, BufferBlock<EventData[]>>();
        private static ConcurrentDictionary<Guid, List<Task>> filtersCompletionTasks = new ConcurrentDictionary<Guid, List<Task>>();
        private static ConcurrentDictionary<Guid, List<IDisposable>> linkDisposables = new ConcurrentDictionary<Guid, List<IDisposable>>();
        private static ConcurrentDictionary<Guid, List<SharedOutput>> appOutputs = new ConcurrentDictionary<Guid, List<SharedOutput>>();
        private static Object outputsLock = new object();

        public Guid StartSession(string serializedEventFlowAppConfig)
        {
            JObject deserializedEventFlowAppConfig = JsonConvert.DeserializeObject<JObject>(serializedEventFlowAppConfig);
            IConfigurationSection outputsSection = BuildOutputsConfiguration(serializedEventFlowAppConfig);

            Guid sessionToken = Guid.NewGuid();
            InitializeApplicationBookkeeping(sessionToken);

            IReadOnlyCollection<EventSink> sinks = CreateOutputs(outputsSection);
            CreateApplicationPipeline(sessionToken, sinks, deserializedEventFlowAppConfig);

            return sessionToken;
        }

        public void EndSession(Guid sessionToken)
        {
            // Complete blocks up to the applications output filters.
            EventFlowHostRemoteService.applicationPipelinesHeads[sessionToken].Complete();
            TimeSpan maxWaitTime = TimeSpan.FromMilliseconds(1000);
            Task.WaitAll(EventFlowHostRemoteService.filtersCompletionTasks[sessionToken].ToArray(), maxWaitTime);
            EventFlowHostRemoteService.cancellationTokenSources[sessionToken].Cancel();

            // Update SharedOutputs reference counters.
            List<SharedOutput> appOutputs = EventFlowHostRemoteService.appOutputs[sessionToken];
            List<Task> outputCompletions = new List<Task>();
            List<CancellationTokenSource> outputsCancellationTokenSources = new List<CancellationTokenSource>();
            List<SharedOutput> outputsToRemove = new List<SharedOutput>();
            SharedOutput value;
            foreach (SharedOutput sharedOutput in appOutputs)
            {
                bool outputNeedsToBeRemoved = false;
                lock (outputsLock)
                {
                    if (sharedOutput.referenceCounter == 1)
                    {
                        outputNeedsToBeRemoved = true;
                        EventFlowHostRemoteService.outputs.TryRemove(sharedOutput.key, out value);
                    }
                }
                if (outputNeedsToBeRemoved)
                {
                    sharedOutput.output.Complete();
                    outputCompletions.Add(sharedOutput.output.Completion);
                    outputsToRemove.Add(sharedOutput);
                    outputsCancellationTokenSources.Add(sharedOutput.cancellationTokenSource);
                }
                else
                {
                    sharedOutput.DecrementReferenceCounter();
                }
            }
            Task.WaitAll(outputCompletions.ToArray(), maxWaitTime);
            foreach (CancellationTokenSource outputCancellationTokenSource in outputsCancellationTokenSources)
            {
                outputCancellationTokenSource.Cancel();
            }

            foreach (IDisposable linkDisposable in EventFlowHostRemoteService.linkDisposables[sessionToken])
            {
                linkDisposable.Dispose();
            }
        }

        public void ReceiveBatch(Guid sessionToken, string serializedEvents)
        {            
            JArray jarrayOfEvents = (JArray)JsonConvert.DeserializeObject(serializedEvents);
            IEnumerable<EventData> events = jarrayOfEvents.Select(serializedEvent => JsonConvert.DeserializeObject<EventData>((string)serializedEvent));            
            if (!EventFlowHostRemoteService.applicationPipelinesHeads[sessionToken].Post(events.ToArray()))
            {
                EventFlowHostRemoteService.healthReporter.ReportWarning("An event was dropped from the diagnostic pipeline because there was not enough capacity", EventFlowContextIdentifiers.Throttling);
            }
        }

        private IConfigurationSection BuildOutputsConfiguration(string serializedConfig)
        {
            string tempConfigFilename = Path.GetTempFileName();
            File.WriteAllText(tempConfigFilename, serializedConfig);
            IConfiguration config = new ConfigurationBuilder().AddJsonFile(tempConfigFilename).Build();
            File.Delete(tempConfigFilename);
            return config.GetSection("outputs");
        }

        private void InitializeApplicationBookkeeping(Guid sessionToken)
        {
            filtersCompletionTasks[sessionToken] = new List<Task>();
            linkDisposables[sessionToken] = new List<IDisposable>();
            appOutputs[sessionToken] = new List<SharedOutput>();
        }

        private IReadOnlyCollection<EventSink> CreateOutputs(IConfigurationSection outputsSection)
        {
            IDictionary<string, string> outputFactories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            outputFactories["ApplicationInsights"] = "Microsoft.Diagnostics.EventFlow.Outputs.ApplicationInsightsOutputFactory, Microsoft.Diagnostics.EventFlow.Outputs.ApplicationInsights, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            outputFactories["StdOutput"] = "Microsoft.Diagnostics.EventFlow.Outputs.StdOutputFactory, Microsoft.Diagnostics.EventFlow.Outputs.StdOutput, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            outputFactories["EventHub"] = "Microsoft.Diagnostics.EventFlow.Outputs.EventHubOutputFactory, Microsoft.Diagnostics.EventFlow.Outputs.EventHub, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            outputFactories["ElasticSearch"] = "Microsoft.Diagnostics.EventFlow.Outputs.ElasticSearchOutputFactory, Microsoft.Diagnostics.EventFlow.Outputs.ElasticSearch, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            outputFactories["OmsOutput"] = "Microsoft.Diagnostics.EventFlow.Outputs.OmsOutputFactory, Microsoft.Diagnostics.EventFlow.Outputs.Oms, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            IDictionary<string, string> filterFactories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            filterFactories["metadata"] = "Microsoft.Diagnostics.EventFlow.Filters.EventMetadataFilterFactory, Microsoft.Diagnostics.EventFlow.Core, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
            filterFactories["drop"] = "Microsoft.Diagnostics.EventFlow.Filters.DropFilterFactory, Microsoft.Diagnostics.EventFlow.Core, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

            List<ItemWithChildren<IOutput, IFilter>> outputCreationResult = EventFlowHostRemoteService.ProcessSection<IOutput, IFilter>(
                outputsSection,
                healthReporter,
                outputFactories,
                filterFactories,
                childSectionName: "filters");
            IReadOnlyCollection<EventSink> sinks = outputCreationResult.Select(outputResult =>
                    new EventSink(outputResult.Item, outputResult.Children)
                ).ToList();

            return sinks;
        }

        private TransformBlock<EventData[], EventData[]> BuildSinkFilters(EventSink sink, CancellationToken applicationCancellationToken)
        {
            FilterAction filterTransform = new FilterAction(
                       sink.Filters,
                       applicationCancellationToken,
                       MaxNumberOfBatchesInProgress,
                       MaxConcurrency,
                       healthReporter);
            return  filterTransform.GetFilterBlock();
        }

        private JObject GetOutputKey(JObject deserializedEventFlowConfig, EventSink sink)
        {
            string outputTypeName = sink.Output.GetType().Name.ToString();
            string outputConfigType = outputTypeName.Replace("Output", "");
            JArray outputsConfig = (JArray)deserializedEventFlowConfig["outputs"];
            JObject jsonOutputConfig = (JObject)outputsConfig.Where(output => (string)output["type"] == outputConfigType).First();

            return jsonOutputConfig;
        }

        private void CreateApplicationPipeline(Guid sessionToken, IReadOnlyCollection<EventSink> sinks, JObject deserializedEventFlowConfig)
        {
            ISourceBlock<EventData[]> sinkSource;
            var propagateCompletion = new DataflowLinkOptions() { PropagateCompletion = true };
            // The application pipeline up to the filters will share the same CancellationToken.
            EventFlowHostRemoteService.cancellationTokenSources[sessionToken] = new CancellationTokenSource();
            CancellationToken applicationCancellationToken = EventFlowHostRemoteService.cancellationTokenSources[sessionToken].Token;
            
            // Application pipeline head.
            var appInputBuffer = new BufferBlock<EventData[]>(
                new DataflowBlockOptions()
                {
                    BoundedCapacity = 1000, // current default for DiagnosticPipelineConfiguration
                    CancellationToken = applicationCancellationToken
                });
            EventFlowHostRemoteService.applicationPipelinesHeads[sessionToken] = appInputBuffer;
            sinkSource = appInputBuffer;

            // The application broadcaster broadcasts the events to all the output filters or directly to the outputs
            // if they don't have a filter set up.
            var appBroadcaster = new BroadcastBlock<EventData[]>(
                    (events) => events?.Select((e) => e.DeepClone()).ToArray(),
                    new DataflowBlockOptions()
                    {
                        BoundedCapacity = MaxNumberOfBatchesInProgress,
                        CancellationToken = applicationCancellationToken
                    });
            IDisposable appInputBufferToBroadcasterLink = sinkSource.LinkTo(appBroadcaster, propagateCompletion);
            EventFlowHostRemoteService.linkDisposables[sessionToken].Add(appInputBufferToBroadcasterLink);
            sinkSource = appBroadcaster;

            foreach (var sink in sinks)
            {
                ISourceBlock<EventData[]> outputSource = sinkSource;
                if (sink.Filters != null && sink.Filters.Count > 0)
                {
                    TransformBlock<EventData[], EventData[]> filterBlock = BuildSinkFilters(sink, applicationCancellationToken);
                    IDisposable broadcasterToFilterLink = sinkSource.LinkTo(filterBlock, propagateCompletion);
                    EventFlowHostRemoteService.linkDisposables[sessionToken].Add(broadcasterToFilterLink);
                    EventFlowHostRemoteService.filtersCompletionTasks[sessionToken].Add(filterBlock.Completion);
                    outputSource = filterBlock;
                }

                JObject outputKey = GetOutputKey(deserializedEventFlowConfig, sink);
                ActionBlock<EventData[]> outputBlock;
                lock (outputsLock)
                {
                    if (EventFlowHostRemoteService.outputs.ContainsKey(outputKey))
                    {
                        outputBlock = EventFlowHostRemoteService.outputs[outputKey].output;
                        EventFlowHostRemoteService.outputs[outputKey].IncrementReferenceCounter();
                    }
                    else
                    {
                        CancellationTokenSource outputCancellationTokenSource = new CancellationTokenSource();
                        OutputAction outputAction = new OutputAction(
                            sink.Output,
                            outputCancellationTokenSource.Token,
                            MaxNumberOfBatchesInProgress,
                            MaxConcurrency,
                            healthReporter);
                        outputBlock = outputAction.GetOutputBlock();
                        EventFlowHostRemoteService.outputs[outputKey] = new SharedOutput(outputKey, outputBlock, outputCancellationTokenSource);
                    }
                }

                IDisposable applicationToSharedOutputLink = outputSource.LinkTo(outputBlock);
                EventFlowHostRemoteService.linkDisposables[sessionToken].Add(applicationToSharedOutputLink);
                EventFlowHostRemoteService.appOutputs[sessionToken].Add(EventFlowHostRemoteService.outputs[outputKey]);
            }
        }

        private static List<ItemWithChildren<PipelineItemType, PipelineItemChildType>> ProcessSection<PipelineItemType, PipelineItemChildType>(
            IConfigurationSection configurationSection,
            IHealthReporter healthReporter,
            IDictionary<string, string> itemFactories,
            IDictionary<string, string> childFactories,
            string childSectionName)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(configurationSection.Key));
            Debug.Assert(healthReporter != null);
            Debug.Assert(itemFactories != null);
            Debug.Assert((string.IsNullOrEmpty(childSectionName) && childFactories == null) || (!string.IsNullOrEmpty(childSectionName) && childFactories != null));

            List<ItemWithChildren<PipelineItemType, PipelineItemChildType>> createdItems = new List<ItemWithChildren<PipelineItemType, PipelineItemChildType>>();

            if (configurationSection == null)
            {
                return createdItems;
            }

            List<IConfigurationSection> itemConfigurationFragments = configurationSection.GetChildren().ToList();

            foreach (var itemFragment in itemConfigurationFragments)
            {
                ItemConfiguration itemConfiguration = new ItemConfiguration();
                try
                {
                    itemFragment.Bind(itemConfiguration);
                }
                catch
                {
                    // It would be ideal to print the whole fragment, but we didn't find a way to serialize the configuration. So we give the configuration path instead.
                    var errMsg = $"{nameof(DiagnosticPipelineFactory)}: invalid configuration fragment '{itemFragment.Path}'";
                    healthReporter.ReportProblem(errMsg);
                    throw new Exception(errMsg);
                }

                string itemFactoryTypeName;
                if (itemConfiguration.Type == "EventFlow")
                {
                    continue;
                }
                if (!itemFactories.TryGetValue(itemConfiguration.Type, out itemFactoryTypeName))
                {
                    var errMsg = $"{nameof(DiagnosticPipelineFactory)}: unknown type '{itemConfiguration.Type}' in configuration section '{configurationSection.Path}'";
                    healthReporter.ReportProblem(errMsg);
                    throw new Exception(errMsg);
                }

                IPipelineItemFactory<PipelineItemType> factory;
                PipelineItemType item = default(PipelineItemType);
                try
                {
                    var itemFactoryType = Type.GetType(itemFactoryTypeName, throwOnError: true);
                    factory = Activator.CreateInstance(itemFactoryType) as IPipelineItemFactory<PipelineItemType>;
                    item = factory.CreateItem(itemFragment, healthReporter);
                }
                catch (Exception e)
                {
                    string errMsg = $"{nameof(DiagnosticPipelineFactory)}: item of type '{itemConfiguration.Type}' could not be created";
                    if (e != null)
                    {
                        errMsg += Environment.NewLine + e.ToString();
                    }
                    healthReporter.ReportProblem(errMsg);
                    throw new Exception(errMsg);
                }

                // The factory will do its own error reporting, so if it returns null, no further error reporting is necessary.
                if (item == null)
                {
                    continue;
                }

                List<ItemWithChildren<PipelineItemChildType, object>> children = null;
                if (!string.IsNullOrEmpty(childSectionName))
                {
                    IConfigurationSection childrenSection = itemFragment.GetSection(childSectionName);
                    children = ProcessSection<PipelineItemChildType, object>(
                        childrenSection,
                        healthReporter,
                        childFactories,
                        childFactories: null,       // Only one level of nexting is supported
                        childSectionName: null);

                    createdItems.Add(new ItemWithChildren<PipelineItemType, PipelineItemChildType>(item, children.Select(c => c.Item).ToList()));
                }
                else
                {
                    createdItems.Add(new ItemWithChildren<PipelineItemType, PipelineItemChildType>(item, null));
                }
            }

            return createdItems;
        }

        private class ItemWithChildren<ItemType, ChildType>
        {
            public ItemWithChildren(ItemType item, IReadOnlyCollection<ChildType> children)
            {
                Debug.Assert(item != null);
                Item = item;
                Children = children;
            }

            public ItemType Item;
            public IReadOnlyCollection<ChildType> Children;
        }

        private class FilterAction
        {
            private IReadOnlyCollection<IFilter> filters;
            private ParallelOptions parallelOptions;
            private ExecutionDataflowBlockOptions executionDataflowBlockOptions;
            private IHealthReporter healthReporter;

            // The stacks in the pool are used as temporary containers for filtered events.
            // Pooling them avoids creation of a new stack every time the filter action is invoked.
            private ConcurrentBag<ConcurrentStack<EventData>> stackPool;

            public FilterAction(
                IReadOnlyCollection<IFilter> filters,
                CancellationToken cancellationToken,
                int boundedCapacity,
                int maxDegreeOfParallelism,
                IHealthReporter healthReporter)
            {
                Requires.NotNull(filters, nameof(filters));
                Requires.Range(boundedCapacity > 0, nameof(boundedCapacity));
                Requires.Range(maxDegreeOfParallelism > 0, nameof(maxDegreeOfParallelism));
                Requires.NotNull(healthReporter, nameof(healthReporter));

                this.filters = filters;
                this.healthReporter = healthReporter;

                this.parallelOptions = new ParallelOptions();
                parallelOptions.CancellationToken = cancellationToken;

                this.executionDataflowBlockOptions = new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = boundedCapacity,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    SingleProducerConstrained = false,
                    CancellationToken = cancellationToken
                };
                this.stackPool = new ConcurrentBag<ConcurrentStack<EventData>>();
            }

            public EventData[] FilterAndDecorate(EventData[] eventsToFilter)
            {
                if (eventsToFilter == null)
                {
                    return null;
                }

                ConcurrentStack<EventData> eventsToKeep;
                if (!this.stackPool.TryTake(out eventsToKeep))
                {
                    eventsToKeep = new ConcurrentStack<EventData>();
                }

                Parallel.ForEach(eventsToFilter, this.parallelOptions, eventData =>
                {
                    try
                    {
                        if (this.filters.All(f => f.Evaluate(eventData) == FilterResult.KeepEvent))
                        {
                            eventsToKeep.Push(eventData);
                        }
                    }
                    catch (Exception e)
                    {
                        this.healthReporter.ReportWarning(
                            nameof(DiagnosticPipeline) + ": a filter has thrown an exception" + Environment.NewLine + e.ToString(),
                            EventFlowContextIdentifiers.Filtering);
                    }
                });

                EventData[] eventsFiltered = eventsToKeep.ToArray();
                eventsToKeep.Clear();
                this.stackPool.Add(eventsToKeep);
                int eventsRemoved = eventsToFilter.Length - eventsFiltered.Length;
                return eventsFiltered;
            }

            public TransformBlock<EventData[], EventData[]> GetFilterBlock()
            {
                return new TransformBlock<EventData[], EventData[]>(
                    (Func<EventData[], EventData[]>)this.FilterAndDecorate,
                    this.executionDataflowBlockOptions);
            }
        }

        private class OutputAction
        {
            private long transmissionSequenceNumber = 0;
            private IOutput output;
            private CancellationToken cancellationToken;
            private ExecutionDataflowBlockOptions executionDataflowBlockOptions;
            private IHealthReporter healthReporter;

            public OutputAction(
                IOutput output,
                CancellationToken cancellationToken,
                int boundedCapacity,
                int maxDegreeOfParallelism,
                IHealthReporter healthReporter)
            {
                Requires.NotNull(output, nameof(output));
                Requires.NotNull(healthReporter, nameof(healthReporter));

                this.output = output;
                this.cancellationToken = cancellationToken;
                this.healthReporter = healthReporter;

                this.executionDataflowBlockOptions = new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = boundedCapacity,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    SingleProducerConstrained = false,
                    CancellationToken = cancellationToken
                };
            }

            public async Task SendDataAsync(EventData[] events)
            {
                if (events.Length > 0)
                {
                    try
                    {
                        await this.output.SendEventsAsync(events, Interlocked.Increment(ref transmissionSequenceNumber), this.cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        this.healthReporter.ReportWarning(
                            nameof(DiagnosticPipeline) + ": an output has thrown an exception while sending data" + Environment.NewLine + e.ToString(),
                            EventFlowContextIdentifiers.Output);
                    }
                }
            }

            public ActionBlock<EventData[]> GetOutputBlock()
            {
                return new ActionBlock<EventData[]>(this.SendDataAsync, this.executionDataflowBlockOptions);
            }
        }
    }
}
