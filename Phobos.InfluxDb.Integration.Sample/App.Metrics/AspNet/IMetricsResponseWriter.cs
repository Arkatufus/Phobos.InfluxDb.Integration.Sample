using System.Threading;
using System.Threading.Tasks;
using System.Web;
using App.Metrics;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet
{
    public interface IMetricsResponseWriter
    {
        /// <summary>
        ///     Writes the specified <see cref="MetricsDataValueSource" /> to the <see cref="HttpContext" /> response.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext" /> for which the snapshot will be written.</param>
        /// <param name="metricsData">The metrics snapshot to write.</param>
        /// <param name="token">The <see cref="CancellationToken" /></param>
        /// <returns>A <see cref="Task" /> which writes the given <see cref="MetricsDataValueSource" /> to the HTTP reponse.</returns>
        Task WriteAsync(HttpContext context, MetricsDataValueSource metricsData, CancellationToken token = default);
    }
}