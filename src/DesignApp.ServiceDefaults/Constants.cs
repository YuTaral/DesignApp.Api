namespace DesignApp.ServiceDefaults;

/// <summary>The fixed values of the shared service defaults.</summary>
public static class Constants
{
    /// <summary>Health checks and telemetry.</summary>
    public static class Health
    {
        /// <summary>The endpoint where every health check must pass.</summary>
        public const string ReadyPath = "/health";

        /// <summary>The endpoint where only the liveness checks must pass.</summary>
        public const string AlivePath = "/alive";

        /// <summary>The check that the app responds at all.</summary>
        public const string SelfCheck = "self";

        /// <summary>The tag of the liveness checks.</summary>
        public const string LiveTag = "live";

        /// <summary>The setting that turns on the OTLP exporter.</summary>
        public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    }
}
