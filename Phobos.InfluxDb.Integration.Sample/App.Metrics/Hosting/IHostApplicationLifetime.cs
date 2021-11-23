using System.Threading;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.Hosting
{
    /// <summary>
    /// Allows consumers to be notified of application lifetime events.
    /// </summary>
    public interface IHostApplicationLifetime
    {
        /// <summary>
        /// Triggered when the application host has fully started.
        /// </summary>
        CancellationToken ApplicationStarted { get; }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// Shutdown will block until this event completes.
        /// </summary>
        CancellationToken ApplicationStopping { get; }
    }
}