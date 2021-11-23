using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Owin;
using Owin;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using App.Metrics;
using App.Metrics.Filters;
using App.Metrics.Formatters;
using App.Metrics.Infrastructure;
using App.Metrics.Internal.Infrastructure;
using App.Metrics.Reporting;
using Jaeger;
using Jaeger.Reporters;
using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Phobos.Prometheus.Integration.Sample.Extensions;
using OpenTracing;
using OpenTracing.Util;
using Phobos.Actor;
using Phobos.Actor.Configuration;
using Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet.Endpoints;
using Phobos.Prometheus.Integration.Sample.App.Metrics.Hosting;
using Phobos.Tracing.Scopes;


[assembly: OwinStartup(typeof(Phobos.Prometheus.Integration.Sample.Startup))]
namespace Phobos.Prometheus.Integration.Sample
{
    public partial class Startup
    {
        /// <summary>
        ///     Name of the <see cref="Environment" /> variable used to direct Phobos' Jaeger
        ///     output.
        ///     See https://github.com/jaegertracing/jaeger-client-csharp for details.
        /// </summary>
        public const string JaegerAgentHostEnvironmentVar = "JAEGER_AGENT_HOST";
        public const string JaegerEndpointEnvironmentVar = "JAEGER_ENDPOINT";
        public const string JaegerAgentPortEnvironmentVar = "JAEGER_AGENT_PORT";

        public const string InfluxDbHostEnvironmentVar = "INFLUXDB_HOST";
        public const string InfluxDbPortEnvironmentVar = "INFLUXDB_PORT";
        public const string InfluxDbDbEnvironmentVar = "INFLUXDB_DB";
        
        public void Configuration(IAppBuilder app)
        {
            // Register all services
            var services = new ServiceCollection();
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var resolver = new DefaultDependencyResolver(serviceProvider);
            DependencyResolver.SetResolver(resolver);

            // After all services are set up, start up all background services
            var backgroundServices = DependencyResolver.Current.GetServices(typeof(BackgroundService));
            var tasks = backgroundServices
                .Select(backgroundService
                    => ((BackgroundService)backgroundService).StartAsync()).ToList();
            Task.WhenAll(tasks).Wait();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddConfiguration();
            services.ConfigureSerilogLogging();

            // Register all classes that derives from IController or post-fixed with "Controller"
            var controllerTypes = typeof(Startup).Assembly.GetExportedTypes()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .Where(t => typeof(IController).IsAssignableFrom(t) ||
                            t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase));
            services.AddControllersAsServices(controllerTypes);

            // Inject OpenTracing GlobalTracer
            services.TryAddSingleton(GlobalTracer.Instance);
            
            // sets up InfluxDB + ASP.NET Core metrics
            ConfigureAppMetrics(services);

            // sets up Jaeger tracer
            ConfigureJaegerTracing(services);

            // this will manage Akka.NET lifecycle
            ConfigureAkka(services);
        }

        public static void ConfigureAppMetrics(IServiceCollection services)
        {
            var host = Environment.GetEnvironmentVariable(InfluxDbHostEnvironmentVar) ?? "localhost";
            var port = Environment.GetEnvironmentVariable(InfluxDbPortEnvironmentVar) ?? "8086";
            var db = Environment.GetEnvironmentVariable(InfluxDbDbEnvironmentVar) ?? "phobos-db";

            services.AddMetrics(b =>
            {
                b.Configuration.Configure(o =>
                    {
                        o.GlobalTags.Add("host", Dns.GetHostName());
                        o.DefaultContextLabel = "akka.net";
                        o.Enabled = true;
                        o.ReportingEnabled = true;
                    })
                    .Report.ToInfluxDb($"http://{host}:{port}", db)
                    .OutputMetrics.AsInfluxDbLineProtocol()
                    .Build();
            });

            services.AddMetricsReportingHostedService();
        }

        public static void ConfigureJaegerTracing(IServiceCollection services)
        {
            ISender BuildSender()
            {
                var endpoint = Environment.GetEnvironmentVariable(JaegerEndpointEnvironmentVar);
                
                if (string.IsNullOrEmpty(endpoint))
                {
                    var port = Environment.GetEnvironmentVariable(JaegerAgentPortEnvironmentVar) ?? "6831";
                    var agentHost = Environment.GetEnvironmentVariable(JaegerAgentHostEnvironmentVar) ?? "localhost";
                    var udpPort = int.Parse(port);
                    return new UdpSender(agentHost, udpPort, 0);
                }

                return new HttpSender(endpoint);
            }

            services.AddSingleton<ITracer>(sp =>
            {
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                var builder = BuildSender();
                var logReporter = new LoggingReporter(loggerFactory);

                var remoteReporter = new RemoteReporter.Builder()
                    .WithLoggerFactory(loggerFactory) // optional, defaults to no logging
                    .WithMaxQueueSize(100) // optional, defaults to 100
                    .WithFlushInterval(TimeSpan.FromSeconds(1)) // optional, defaults to TimeSpan.FromSeconds(1)
                    .WithSender(builder) // optional, defaults to UdpSender("localhost", 6831, 0)
                    .Build();

                var sampler = new ConstSampler(true); // keep sampling disabled

                // name the service after the executing assembly
                var tracer = new Tracer.Builder(typeof(Startup).Assembly.GetName().Name)
                    .WithReporter(new CompositeReporter(remoteReporter, logReporter))
                    .WithSampler(sampler)
                    .WithScopeManager(new ActorScopeManager()); // IMPORTANT: ActorScopeManager needed to properly correlate trace inside Akka.NET

                return tracer.Build();
            });
        }

        public static void ConfigureAkka(IServiceCollection services)
        {
            services.AddSingleton(sp =>
            {
                var metrics = sp.GetRequiredService<IMetricsRoot>();
                var tracer = sp.GetRequiredService<ITracer>();

                var config = ConfigurationFactory.Load()
                    .BootstrapFromDocker()
                    .UseSerilog();

                var phobosSetup = PhobosSetup.Create(new PhobosConfigBuilder()
                        .WithMetrics(m => m.SetMetricsRoot(metrics)) // binds Phobos to same IMetricsRoot as ASP.NET Core
                        .WithTracing(t => t.SetTracer(tracer))) // binds Phobos to same tracer as ASP.NET Core
                    .WithSetup(BootstrapSetup.Create()
                        .WithConfig(config) // passes in the HOCON for Akka.NET to the ActorSystem
                        .WithActorRefProvider(PhobosProviderSelection.Cluster)); // last line activates Phobos inside Akka.NET

                var sys = ActorSystem.Create("ClusterSys", phobosSetup);

                // create actor "container" and bind it to DI, so it can be used by ASP.NET Core
                return new AkkaActors(sys);
            });

            // this will manage Akka.NET lifecycle
            services.AddSingleton<BackgroundService, AkkaService>();
        }        
    }

    public static class AppMetricsExtensions
    {
        private static readonly string DefaultConfigSection = nameof(MetricEndpointsOptions);

        public static IServiceCollection AddMetrics(this IServiceCollection services, Action<IMetricsBuilder> setupMetrics)
        {
            var builder = new MetricsBuilder();
            setupMetrics(builder);

            return AddMetrics(services, builder);
        }

        public static IServiceCollection AddMetrics(this IServiceCollection services, IMetricsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var metrics = builder.Build();

            AddCoreServices(services, metrics);

            return services;
        }

        private static void AddCoreServices(IServiceCollection services, IMetricsRoot metrics)
        {
            services.TryAddSingleton<IClock>(metrics.Clock);
            services.TryAddSingleton<IFilterMetrics>(metrics.Filter);
            services.TryAddSingleton<IMetricsOutputFormatter>(metrics.DefaultOutputMetricsFormatter);
            services.TryAddSingleton<IReadOnlyCollection<IMetricsOutputFormatter>>(metrics.OutputMetricsFormatters);
            services.TryAddSingleton<IEnvOutputFormatter>(metrics.DefaultOutputEnvFormatter);
            services.TryAddSingleton<IReadOnlyCollection<IEnvOutputFormatter>>(metrics.OutputEnvFormatters);
            services.TryAddSingleton<EnvironmentInfoProvider>(new EnvironmentInfoProvider());
            services.TryAddSingleton<IMetrics>(metrics);
            services.TryAddSingleton<IMetricsRoot>(metrics);
            services.TryAddSingleton<MetricsOptions>(metrics.Options);
            services.TryAddSingleton<IReadOnlyCollection<IReportMetrics>>(metrics.Reporters);
            services.TryAddSingleton<IRunMetricsReports>(metrics.ReportRunner);
            services.TryAddSingleton<AppMetricsMarkerService, AppMetricsMarkerService>();
        }
    }
}
