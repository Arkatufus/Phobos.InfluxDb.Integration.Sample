namespace Phobos.Prometheus.Integration.Sample.App.Metrics.AspNet.Endpoints
{
    public class MetricsEndpointsHostingOptions
    {
        /// <summary>
        ///     Gets or sets the port to host available endpoints provided by App Metrics.
        /// </summary>
        /// <remarks>
        ///     This overrides all endpoing specific port configuration allowing a the port to be specific on a single
        ///     setting.
        /// </remarks>
        /// <value>
        ///     The App Metrics available endpoint's port.
        /// </value>
        public int? AllEndpointsPort { get; set; }

        /// <summary>
        ///     Gets or sets the environment info endpoint, defaults to /env.
        /// </summary>
        /// <value>
        ///     The environment info endpoint.
        /// </value>
        public string EnvironmentInfoEndpoint { get; set; } = "/env";

        /// <summary>
        ///     Gets or sets the port to host the env info endpoint.
        /// </summary>
        /// <value>
        ///     The env info endpoint's port.
        /// </value>
        public int? EnvironmentInfoEndpointPort { get; set; }

        /// <summary>
        ///     Gets or sets the metrics endpoint, defaults to /metrics.
        /// </summary>
        /// <value>
        ///     The metrics endpoint.
        /// </value>
        public string MetricsEndpoint { get; set; } = "/metrics";

        /// <summary>
        ///     Gets or sets the port to host the metrics endpoint.
        /// </summary>
        /// <value>
        ///     The metrics endpoint's port.
        /// </value>
        public int? MetricsEndpointPort { get; set; }

        /// <summary>
        ///     Gets or sets the metrics text endpoint, defaults to metrics-text.
        /// </summary>
        /// <value>
        ///     The metrics text endpoint.
        /// </value>
        public string MetricsTextEndpoint { get; set; } = "/metrics-text";

        /// <summary>
        ///     Gets or sets the port to host the metrics text endpoint.
        /// </summary>
        /// <value>
        ///     The metrics text endpoint's port.
        /// </value>
        public int? MetricsTextEndpointPort { get; set; }
    }
}