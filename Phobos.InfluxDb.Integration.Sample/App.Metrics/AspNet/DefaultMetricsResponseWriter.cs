using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using App.Metrics;
using App.Metrics.Formatters;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet
{
    public class DefaultMetricsResponseWriter : IMetricsResponseWriter
    {
        private readonly IMetricsOutputFormatter _fallbackFormatter;
        private readonly IMetricsOutputFormatter _formatter;
        private readonly MetricsFormatterCollection _formatters;

        public DefaultMetricsResponseWriter(
            IMetricsOutputFormatter fallbackFormatter,
            IReadOnlyCollection<IMetricsOutputFormatter> formatters)
        {
            if (formatters == null)
            {
                throw new ArgumentNullException(nameof(formatters));
            }

            _formatters = new MetricsFormatterCollection(formatters.ToList());
            _fallbackFormatter = fallbackFormatter;
        }

        // ReSharper disable UnusedMember.Global
        public DefaultMetricsResponseWriter(IMetricsOutputFormatter formatter)
        // ReSharper restore UnusedMember.Global
        {
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        /// <inheritdoc />
        public Task WriteAsync(HttpContext context, MetricsDataValueSource metricsData, CancellationToken token = default)
        {
            var formatter = _formatter ?? context.Request.Headers.ResolveFormatter(_fallbackFormatter,
                metricsMediaTypeValue => _formatters.GetType(metricsMediaTypeValue));

            //context.SetNoCacheHeaders();
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            if (formatter == default(IMetricsOutputFormatter))
            {
                context.Response.StatusCode = 406;//StatusCodes.Status406NotAcceptable;
                context.Response.Headers[HeaderNames.ContentType] = context.Request.ContentType;
                return Task.CompletedTask;
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Headers[HeaderNames.ContentType] = formatter.MediaType.ContentType;

            return formatter.WriteAsync(context.Response.OutputStream, metricsData, token);
        }
    }
}