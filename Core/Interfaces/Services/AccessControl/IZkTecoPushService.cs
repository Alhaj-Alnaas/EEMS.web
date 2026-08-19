namespace Core.Interfaces.Services.AccessControl
{
    /// <summary>
    /// ZKTeco ADMS / Push Protocol handler (IF-001).
    /// Devices initiate HTTP calls; the server responds with OK / config / queued commands.
    /// </summary>
    public interface IZkTecoPushService
    {
        /// <summary>GET /iclock/cdata?SN=...&amp;options=all — handshake / option negotiation.</summary>
        Task<string> HandleHandshakeAsync(
            string serial,
            string? pushver = null,
            string? pushOptionsFlag = null,
            CancellationToken ct = default);

        /// <summary>POST /iclock/cdata — device pushes ATTLOG / OPERLOG / etc.</summary>
        Task<string> HandleDataPushAsync(string serial, string? table, string body, string? stamp = null, CancellationToken ct = default);

        /// <summary>POST /iclock/querydata — DATA QUERY results on newer firmware.</summary>
        Task<string> HandleQueryDataAsync(string serial, string? table, string? type, string body, CancellationToken ct = default);

        /// <summary>Queues INFO + USERINFO + FINGERTMP + BIODATA pull commands.</summary>
        Task EnqueueBiometricPullAsync(Guid readerId, string? pin = null, CancellationToken ct = default);

        /// <summary>POST /iclock/registry — first visit returns RegistryCode only; later visits return registry=ok.</summary>
        Task<(string Body, string SessionId)> HandleRegistryAsync(string serial, string body, bool alreadyHasToken, CancellationToken ct = default);

        /// <summary>GET/POST /iclock/push — Security PUSH config download after RegistryCode.</summary>
        Task<(string Body, string SessionId)> HandlePushConfigAsync(string serial, CancellationToken ct = default);

        /// <summary>GET /iclock/getrequest — device polls for pending commands.</summary>
        Task<string> HandleGetRequestAsync(string serial, string? info = null, CancellationToken ct = default);

        /// <summary>GET /iclock/ping or /iclock/request — same as getrequest on many firmwares.</summary>
        Task<string> HandlePollAsync(string serial, string? info = null, CancellationToken ct = default);

        /// <summary>POST /iclock/devicecmd — device acknowledges executed commands.</summary>
        Task<string> HandleDeviceCmdAsync(string serial, string body, CancellationToken ct = default);

        /// <summary>Build the Push protocol line for a queued command.</summary>
        string FormatCommand(int protocolCommandId, string commandType, string? payload);

        /// <summary>Updates stats from querydata count parameter.</summary>
        Task UpdateQueryDataCountAsync(string serial, string tableName, int count);
    }
}
