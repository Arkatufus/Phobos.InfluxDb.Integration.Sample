using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Util;
using Akka.Event;
using Akka.Routing;
using App.Metrics.Timer;
using OpenTracing.Util;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;
using Phobos.Actor;
using Phobos.Prometheus.Integration.Sample.App.Metrics.Hosting;

namespace Phobos.Prometheus.Integration.Sample
{
 public sealed class ChildActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public ChildActor()
        {
            ReceiveAny(_ =>
            {
                if (ThreadLocalRandom.Current.Next(0, 3) == 1) throw new ApplicationException("I'm crashing!");

                _log.Info("Received: {0}", _);
                Sender.Tell(_);
                Self.Tell(PoisonPill.Instance);

                var span = Context.GetInstrumentation().ActiveSpan;

                if (ThreadLocalRandom.Current.Next(0, 4) == 2)
                    // send a random integer to our parent in order to generate an "unhandled"
                    // message periodically
                    Context.Parent.Tell(ThreadLocalRandom.Current.Next());
            });
        }

        protected override void PreRestart(Exception reason, object message)
        {
            // re-send the message that caused us to crash so we can reprocess
            Self.Tell(message, Sender);
        }
    }

    public sealed class ConsoleActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public ConsoleActor()
        {
            Receive<string>(_ =>
            {
                // use the local metrics handle to record a timer duration for how long this block of code takes to execute
                Context.GetInstrumentation().Monitor.Timer.Time(new TimerOptions {Name = "ProcessingTime"}, () =>
                {
                    // start another span programmatically inside actor
                    using (var newSpan = Context.GetInstrumentation().Tracer.BuildSpan("SecondOp").StartActive())
                    {
                        var child = Context.ActorOf(Props.Create(() => new ChildActor()));
                        _log.Info("Spawned {child}", child);

                        child.Forward(_);
                    }
                });
            });
        }
    }

    /// <summary>
    ///     To add some color to the traces
    /// </summary>
    public sealed class RouterForwarderActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private readonly IActorRef _routerActor;

        public RouterForwarderActor(IActorRef routerActor)
        {
            _routerActor = routerActor;
            Receive<string>(_ =>
            {
                _log.Info("Received: {0}", _);
                _routerActor.Forward(_);
            });
        }
    }

    /// <summary>
    ///     Container for retaining actors
    /// </summary>
    public sealed class AkkaActors
    {
        public AkkaActors(ActorSystem sys)
        {
            Sys = sys;
            ConsoleActor = sys.ActorOf(Props.Create(() => new ConsoleActor()), "console");
            RouterActor = sys.ActorOf(Props.Empty.WithRouter(FromConfig.Instance), "echo");
            RouterForwarderActor = sys.ActorOf(Props.Create(() => new RouterForwarderActor(RouterActor)), "fwd");
        }

        internal ActorSystem Sys { get; }

        public IActorRef ConsoleActor { get; }

        internal IActorRef RouterActor { get; }

        public IActorRef RouterForwarderActor { get; }
    }

    public class AkkaService : BackgroundService
    {
        private readonly AkkaActors _actors;

        public AkkaService(AkkaActors actors, IServiceProvider services)
        {
            _actors = actors;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // start https://cmd.petabridge.com/ for diagnostics and profit
            var pbm = PetabridgeCmd.Get(_actors.Sys); // start Pbm
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(new RemoteCommands());
            pbm.Start(); // begin listening for PBM management commands

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return CoordinatedShutdown.Get(_actors.Sys).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}