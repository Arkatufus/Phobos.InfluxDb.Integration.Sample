using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Extensions.Options;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet.Tracking.Internal
{
    /// <summary>
    ///     Sets up default metric tracking middleware options for <see cref="MetricsWebTrackingOptions"/>.
    /// </summary>
    public class MetricsTrackingMiddlewareOptionsSetup : IConfigureOptions<MetricsWebTrackingOptions>
    {
        /// <inheritdoc />
        public void Configure(MetricsWebTrackingOptions options)
        {
        }
    }
}