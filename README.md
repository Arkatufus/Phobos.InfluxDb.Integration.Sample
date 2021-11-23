# Phobos And ASP .NET MVC 4 Integration Sample

This simple project illustrates the steps needed to backport Phobos integration
from ASP .NET Core to ASP .NET MVC 4 running in .NET Framework 4.7.1.

Almost all of the code needed are located on the root directory of the project,
except for the code inside `Controllers/HomeController.cs` which is used to inject
an active tracing span every time the main index page is refreshed.

The main code that are needed are as follow:
- The `AppMetricsExtension` static class inside `Startup.cs`:

  These codes are needed to bootstrap App.Metrics services to ASP .NET. Most of App.Metrics.AspNetCore
  are stripped, only the bare minimum initialization codes are kept.
  
- The `Startup` class itself:

  Phobos and AppMetrics would not work without `Microsoft.Extensions.DependencyInjection` and 
  `Microsoft.Extensions.Logging`, so they needed to be added manually inside the `Configuration` method.
  MVC 4 also does not have the `IHostedService` and `BackgroundService` feature in netcore, so these
  has to be ported back. These background services are started after all of the services have been
  registered and set up; they are terminated by hooking into the `Disposed` event in the `HttpApplication`
  class inside the `Global.asax.cs` file. The ordering of these set up steps are quite important:
  - a `ServiceCollection` has to be instantiated first 
  - options are then loaded, if you are using `Microsoft.Extensions.Configuration`. 
  - all services that are going to leverage the dependency injection feature needs to be registered
    with the service collection. Since MVC 4 does not do this automatically, all controllers needs
    to be registered manually with the service collection.
  - Configure and register App.Metrics, InfluxDB, Jaeger, and Akka
  - After everything is set up, all background services needs to be started.
  - A note on background services, in this implementation, if you're going to override the `StartAsync`
    method, it is very important that you return immediately. All long running asynchronous tasks and 
    loops should be executed inside the `ExecuteAsync` method.

- The `MetricReporterBackgroundService` class:

  This is an exact copy of the same class from App.Metrics. Its job is to schedule metrics reporting
  for any registered metrics reporter at regular interval.
  
- The `DefaultDependencyResolver` class:
  
  This class is needed by `Microsoft.Extensions.DependencyInjection` to hold a reference to the
  `ServiceProvider` instance that is created at the start of the `Startup` class.

- The `SerilogBootstrapper` and `SeqBootstrapper` class inside the `SerilogBootstrapper.cs` file

  These classes are used to set up Serilog and Seq.

- The `Web.config` file
  
  The Akka HOCON settings are stored in a configuration section inside this file
  
## Running The Sample Locally

- You will need to have docker installed on your machine before running this sample. 
- Inside PowerShell or CMD, navigate to the docker directory inside the project folder. 
- Run `docker-compose up` to start Seq, Jaeger, InfluxDB, and Grafana inside their pre-configured docker instances.
- Open the project solution file inside Visual Studio, you will need at least Visual Studio 2019.
- Run the web application inside IIS from inside Visual Studio.
- The traces and logs can be viewed inside their respective applications:
  - Jaeger at http://localhost:16686
  - Grafana at http://localhost:3000
  - Seq at http://localhost:8988
  - InfluxDB at http://localhost:8086
- Refresh the web page a few times to generate some traffic. If you are running in debug mode, you will
  encounter exceptions while doing this. These exceptions are intentionally thrown to simulate failures.

