using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using App.Metrics;
using App.Metrics.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Phobos.Prometheus.Integration.Sample.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static IServiceCollection AddControllersAsServices(this IServiceCollection services,
            IEnumerable<Type> controllerTypes)
        {
            foreach (var type in controllerTypes)
            {
                services.AddTransient(type);
            }

            return services;
        }

        public static IServiceCollection AddConfiguration(this IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            // TODO: Instantiate options class here
            //services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.Key));
            return services;
        }
        
        public static IServiceCollection AddMetricsReportingHostedService(
            this IServiceCollection services,
            EventHandler<UnobservedTaskExceptionEventArgs> unobservedTaskExceptionHandler = null)
        {
            services.AddSingleton<BackgroundService, MetricsReporterBackgroundService>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<MetricsOptions>();
                var metrics = serviceProvider.GetRequiredService<IMetrics>();
                var reporters = serviceProvider.GetService<IReadOnlyCollection<IReportMetrics>>();
                var instance = new MetricsReporterBackgroundService(metrics, options, reporters);
                if (unobservedTaskExceptionHandler != null)
                {
                    instance.UnobservedTaskException += unobservedTaskExceptionHandler;
                }

                return instance;
            });

            return services;
        }
    }
}