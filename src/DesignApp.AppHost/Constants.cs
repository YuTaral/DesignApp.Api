namespace DesignApp.AppHost;

/// <summary>
/// The AppHost's fixed values. The AppHost can't reference the API's types, so values the API
/// reads too say which constant they mirror.
/// </summary>
public static class Constants
{
    /// <summary>The resources the AppHost starts and how they are wired together.</summary>
    public static class Resources
    {
        /// <summary>The Postgres server.</summary>
        public const string Postgres = "postgres";

        /// <summary>The database. Mirrors <c>DesignApp.Infrastructure.Constants.Config.DatabaseName</c>.</summary>
        public const string Database = "designapp";

        /// <summary>The storage user parameter.</summary>
        public const string StorageUser = "storage-user";

        /// <summary>The storage user's name.</summary>
        public const string StorageUserName = "designapp";

        /// <summary>The storage password parameter.</summary>
        public const string StoragePassword = "storage-password";

        /// <summary>The length of the generated storage password.</summary>
        public const int StoragePasswordLength = 32;

        /// <summary>The storage container.</summary>
        public const string Storage = "storage";

        /// <summary>The storage image.</summary>
        public const string StorageImage = "rustfs/rustfs";

        /// <summary>The storage image's version.</summary>
        public const string StorageTag = "1.0.4";

        /// <summary>The storage's access key variable.</summary>
        public const string StorageAccessKeyVariable = "RUSTFS_ACCESS_KEY";

        /// <summary>The storage's secret key variable.</summary>
        public const string StorageSecretKeyVariable = "RUSTFS_SECRET_KEY";

        /// <summary>The storage's S3 port.</summary>
        public const int S3Port = 9000;

        /// <summary>The storage's S3 endpoint.</summary>
        public const string S3Endpoint = "s3";

        /// <summary>The storage's console port.</summary>
        public const int ConsolePort = 9001;

        /// <summary>The storage's console endpoint.</summary>
        public const string ConsoleEndpoint = "console";

        /// <summary>The storage's health check path.</summary>
        public const string StorageHealthPath = "/health";

        /// <summary>The storage's data volume.</summary>
        public const string StorageVolume = "designapp-storage";

        /// <summary>Where the storage keeps its data.</summary>
        public const string StorageDataPath = "/data";

        /// <summary>The API project.</summary>
        public const string Api = "api";

        /// <summary>The API's storage address setting. Mirrors the <c>Storage</c> section of the API.</summary>
        public const string StorageServiceUrlVariable = "Storage__ServiceUrl";

        /// <summary>The API's storage access key setting.</summary>
        public const string StorageAccessKeySetting = "Storage__AccessKey";

        /// <summary>The API's storage secret key setting.</summary>
        public const string StorageSecretKeySetting = "Storage__SecretKey";

        /// <summary>The web app's folder, next to the API's repository.</summary>
        public const string WebFolder = "DesignApp.Web";

        /// <summary>The web app.</summary>
        public const string Web = "web";

        /// <summary>The web app's endpoint.</summary>
        public const string WebEndpoint = "http";

        /// <summary>The web app's fixed port.</summary>
        public const int WebPort = 5173;

        /// <summary>The second allowed CORS origin of the API. Mirrors <c>DesignApp.Api.Constants.Http.WebOriginsKey</c>.</summary>
        public const string WebOriginVariable = "Cors__WebOrigins__1";
    }
}
