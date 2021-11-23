using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using App.Metrics.Infrastructure;

namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet
{
    public interface IEnvResponseWriter
    {
        Task WriteAsync(HttpContext context, EnvironmentInfo environmentInfo, CancellationToken token = default);
    }
}