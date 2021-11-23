using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using App.Metrics.Formatters;
using App.Metrics.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet
{
    public class DefaultEnvResponseWriter : IEnvResponseWriter
    {
        private readonly IEnvOutputFormatter _fallbackFormatter;
        private readonly IEnvOutputFormatter _formatter;
        private readonly EnvFormatterCollection _formatters;

        public DefaultEnvResponseWriter(
            IEnvOutputFormatter fallbackFormatter,
            IReadOnlyCollection<IEnvOutputFormatter> formatters)
        {
            if (formatters == null)
            {
                throw new ArgumentNullException(nameof(formatters));
            }

            _formatters = new EnvFormatterCollection(formatters.ToList());
            _fallbackFormatter = fallbackFormatter ?? throw new ArgumentNullException(nameof(fallbackFormatter));
        }

        public DefaultEnvResponseWriter(IEnvOutputFormatter formatter)
        {
            _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        }

        public Task WriteAsync(HttpContext context, EnvironmentInfo environmentInfo, CancellationToken token = default)
        {
            var formatter = _formatter ?? context.Request.Headers.ResolveFormatter(_fallbackFormatter,
                metricsMediaTypeValue => _formatters.GetType(metricsMediaTypeValue));

            //context.SetNoCacheHeaders();
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            if (formatter == default(IEnvOutputFormatter))
            {
                context.Response.StatusCode = 406; //StatusCodes.Status406NotAcceptable;
                context.Response.Headers[HeaderNames.ContentType] = context.Request.ContentType;
                return Task.CompletedTask;
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Headers[HeaderNames.ContentType] = formatter.MediaType.ContentType;

            return formatter.WriteAsync(context.Response.OutputStream, environmentInfo, token);
        }
    }

    internal static class RequestHeaderExtensions
    {
        public static TFormatter ResolveFormatter<TFormatter>(
            this NameValueCollection headers,
            TFormatter fallbackFormatter,
            Func<MetricsMediaTypeValue, TFormatter> resolveOutputFormatter)
        {

            var acceptValues = headers.GetValues(HeaderNames.Accept);

            if (acceptValues == null)
            {
                return fallbackFormatter;
            }

            var formatter = fallbackFormatter;

            foreach (var accept in acceptValues)
            {
                if(!MediaTypeHeaderValue.TryParse(accept, out var mediaType))
                    continue;

                var metricsMediaTypeValue = mediaType.ToMetricsMediaType();

                if (metricsMediaTypeValue != default)
                {
                    formatter = resolveOutputFormatter(metricsMediaTypeValue);
                }

                if (formatter != null)
                {
                    return formatter;
                }
            }

            return fallbackFormatter;
        }

        public static MetricsMediaTypeValue ToMetricsMediaType(this MediaTypeHeaderValue mediaTypeHeaderValue)
        {
            var mediaType = new StringSegment(mediaTypeHeaderValue.MediaType);

            var versionAndFormatTokens = mediaType.SubType().Value.Split('-');


            if (string.IsNullOrWhiteSpace(mediaType.Type().Value)
                || string.IsNullOrWhiteSpace(mediaType.SubType().Value)
                || versionAndFormatTokens.Length != 2)
            {
                return default;
            }

            var versionAndFormat = versionAndFormatTokens[1].Split('+');

            if (versionAndFormat.Length != 2)
            {
                return default;
            }

            return new MetricsMediaTypeValue(
                mediaType.Type().Value,
                versionAndFormatTokens[0],
                versionAndFormat[0],
                versionAndFormat[1]);
        }

        public static StringSegment Type(this StringSegment mediaType)
            => mediaType.Subsegment(0, mediaType.IndexOf('/'));

        public static StringSegment SubType(this StringSegment mediaType)
            => mediaType.Subsegment(0, mediaType.IndexOf('/') + 1);

    }
}