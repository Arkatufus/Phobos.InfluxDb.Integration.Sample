using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Phobos.Prometheus.Integration.Sample
{
    public class DefaultDependencyResolver : IDependencyResolver
    {
        private readonly IServiceProvider _services;

        public DefaultDependencyResolver(IServiceProvider services)
        {
            _services = services;
        }

        public object GetService(Type serviceType)
            => _services.GetService(serviceType);

        public IEnumerable<object> GetServices(Type serviceType)
            => _services.GetServices(serviceType);
    }
}