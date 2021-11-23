using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Akka.Actor;
using Microsoft.Extensions.Logging;
using OpenTracing;
using ILoggerFactory = Microsoft.Owin.Logging.ILoggerFactory;

namespace Phobos.Prometheus.Integration.Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly AkkaActors _actors;
        private readonly ITracer _tracer;
        private readonly ILogger<HomeController> _logger;
        
        public HomeController(AkkaActors actors, ITracer tracer, ILogger<HomeController> logger)
        {
            _actors = actors;
            _tracer = tracer;
            _logger = logger;
        }
        
        public async Task<ActionResult> Index()
        {
            _logger.LogInformation("Index called");
            using (var s = _tracer.BuildSpan("Cluster.Ask").StartActive())
            {
                var resp = await _actors.RouterForwarderActor.Ask<string>($"hit from {HttpContext.Session.SessionID}",
                    TimeSpan.FromSeconds(5));
                return new ContentResult {Content = resp};
            }
        }
    }
}
