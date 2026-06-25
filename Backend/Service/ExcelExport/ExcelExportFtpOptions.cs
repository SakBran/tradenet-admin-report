namespace API.Service.ExcelExport
{
    /// <summary>
    /// FTP settings for the "Ftp" storage backend (bound from ExcelExport:Ftp).
    /// Credentials are secrets — set them on the server (server appsettings.json or env vars
    /// like ExcelExport__Ftp__Password); deploy.ps1 does not carry repo appsettings to the server.
    /// </summary>
    public sealed class ExcelExportFtpOptions
    {
        /// <summary>FTP host name or IP (control connection). Required when Storage = "Ftp".</summary>
        public string Host { get; set; } = "";

        /// <summary>FTP control port (default 21).</summary>
        public int Port { get; set; } = 21;

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

        /// <summary>Remote base directory under which date-sharded files are written, e.g. "/ExcelExports".</summary>
        public string BasePath { get; set; } = "/";

        /// <summary>Control + data connect timeout (seconds). Generous by default to absorb slow banners.</summary>
        public int ConnectTimeoutSeconds { get; set; } = 60;

        /// <summary>Read/transfer timeout (seconds) for uploads and downloads.</summary>
        public int OperationTimeoutSeconds { get; set; } = 120;
    }
}
