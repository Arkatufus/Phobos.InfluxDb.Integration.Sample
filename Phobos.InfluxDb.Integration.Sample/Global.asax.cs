using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Phobos.Prometheus.Integration.Sample
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801

    public class WebApiApplication : HttpApplication
    {
        public WebApiApplication()
        {
            Disposed += OnDisposed;
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private void OnDisposed(object sender, EventArgs e)
        {
            using (var stopCts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                var backgroundServices = DependencyResolver.Current.GetServices(typeof(BackgroundService));
                var tasks = backgroundServices
                    .Select(backgroundService 
                        => ((BackgroundService) backgroundService).StopAsync(stopCts.Token)).ToList();

                Task.WhenAll(tasks).Wait(stopCts.Token);
            }
        }
    }
}