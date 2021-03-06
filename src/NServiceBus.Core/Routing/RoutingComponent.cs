namespace NServiceBus
{
    using System.Collections.Generic;
    using Config;
    using Features;
    using Pipeline;
    using Routing;
    using Routing.MessageDrivenSubscriptions;
    using Settings;
    using Transport;

    class RoutingComponent
    {
        public const string EnforceBestPracticesSettingsKey = "NServiceBus.Routing.EnforceBestPractices";

        public RoutingComponent(UnicastRoutingTable unicastRoutingTable, DistributionPolicy distributionPolicy, EndpointInstances endpointInstances, Publishers publishers)
        {
            UnicastRoutingTable = unicastRoutingTable;
            DistributionPolicy = distributionPolicy;
            EndpointInstances = endpointInstances;
            Publishers = publishers;
        }

        public UnicastRoutingTable UnicastRoutingTable { get; }

        public DistributionPolicy DistributionPolicy { get; }

        public EndpointInstances EndpointInstances { get; }

        public Publishers Publishers { get; }

        public bool EnforceBestPractices { get; private set; }

        public void Initialize(ReadOnlySettings settings, TransportInfrastructure transportInfrastructure, PipelineSettings pipelineSettings)
        {
            var unicastBusConfig = settings.GetConfigSection<UnicastBusConfig>();
            var conventions = settings.Get<Conventions>();
            var configuredUnicastRoutes = settings.GetOrDefault<ConfiguredUnicastRoutes>();
            var distributorAddress = settings.GetOrDefault<string>("LegacyDistributor.Address");

            List<DistributionStrategy> distributionStrategies;
            if (settings.TryGet(out distributionStrategies))
            {
                foreach (var distributionStrategy in distributionStrategies)
                {
                    DistributionPolicy.SetDistributionStrategy(distributionStrategy);
                }
            }

            unicastBusConfig?.MessageEndpointMappings.Apply(Publishers, UnicastRoutingTable, transportInfrastructure.MakeCanonicalForm, conventions);
            configuredUnicastRoutes?.Apply(UnicastRoutingTable, conventions);

            pipelineSettings.Register(b =>
            {
                var router = new UnicastSendRouter(settings.GetOrDefault<string>("BaseInputQueueName"), settings.EndpointName(), settings.InstanceSpecificQueue(), distributorAddress, DistributionPolicy, UnicastRoutingTable, EndpointInstances, i => transportInfrastructure.ToTransportAddress(LogicalAddress.CreateRemoteAddress(i)));
                return new UnicastSendRouterConnector(router);
            }, "Determines how the message being sent should be routed");

            pipelineSettings.Register(new UnicastReplyRouterConnector(), "Determines how replies should be routed");

            EnforceBestPractices = ShouldEnforceBestPractices(settings);
            if (EnforceBestPractices)
            {
                EnableBestPracticeEnforcement(conventions, pipelineSettings);
            }
        }

        static bool ShouldEnforceBestPractices(ReadOnlySettings settings)
        {
            bool enforceBestPractices;
            if (settings.TryGet(EnforceBestPracticesSettingsKey, out enforceBestPractices))
            {
                return enforceBestPractices;
            }

            // enable best practice enforcement by default
            return true;
        }

        static void EnableBestPracticeEnforcement(Conventions conventions, PipelineSettings pipeline)
        {
            var validations = new Validations(conventions);

            pipeline.Register(
                "EnforceSendBestPractices",
                new EnforceSendBestPracticesBehavior(validations),
                "Enforces send messaging best practices");

            pipeline.Register(
                "EnforceReplyBestPractices",
                new EnforceReplyBestPracticesBehavior(validations),
                "Enforces reply messaging best practices");

            pipeline.Register(
                "EnforcePublishBestPractices",
                new EnforcePublishBestPracticesBehavior(validations),
                "Enforces publish messaging best practices");

            pipeline.Register(
                "EnforceSubscribeBestPractices",
                new EnforceSubscribeBestPracticesBehavior(validations),
                "Enforces subscribe messaging best practices");

            pipeline.Register(
                "EnforceUnsubscribeBestPractices",
                new EnforceUnsubscribeBestPracticesBehavior(validations),
                "Enforces unsubscribe messaging best practices");
        }
    }
}