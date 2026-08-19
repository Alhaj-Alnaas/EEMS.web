using Core.Interfaces.Services.AccessControl;
using Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PMS.web.Controllers
{
    /// <summary>
    /// ZKTeco ADMS / Push Protocol endpoints. Devices call these over HTTP (IF-001).
    /// Configure device Cloud/ADMS server URL to this host (e.g. http://server/iclock).
    /// </summary>
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [ApiController]
    [Route("iclock")]
    public class ZkTecoPushController : ControllerBase
    {
        private readonly IZkTecoPushService _push;
        private readonly IReaderService _readers;
        private readonly IReaderConnectionLogService _connectionLog;
        private readonly ILogger<ZkTecoPushController> _logger;

        public ZkTecoPushController(
            IZkTecoPushService push,
            IReaderService readers,
            IReaderConnectionLogService connectionLog,
            ILogger<ZkTecoPushController> logger)
        {
            _push = push;
            _readers = readers;
            _connectionLog = connectionLog;
            _logger = logger;
        }

        /// <summary>Handshake / options (GET) or data upload (POST).</summary>
        [HttpGet("cdata")]
        [HttpPost("cdata")]
        public async Task<IActionResult> CData(
            [FromQuery(Name = "SN")] string? sn,
            [FromQuery] string? table,
            [FromQuery] string? Stamp,
            [FromQuery] string? pushver,
            [FromQuery(Name = "PushOptionsFlag")] string? pushOptionsFlag,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            var endpoint = HttpMethods.IsGet(Request.Method) ? "/iclock/cdata (handshake)" : $"/iclock/cdata ({table ?? "data"})";
            await LogHitAsync(sn, endpoint);

            if (HttpMethods.IsGet(Request.Method))
            {
                var handshake = await _push.HandleHandshakeAsync(sn, pushver, pushOptionsFlag, ct);
                return DeviceReply(handshake);
            }

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);

            _logger.LogWarning(
                "CDATA POST SN={SN} table={Table} stamp={Stamp} bodyLen={Len} bodyPreview={Preview}",
                sn, table ?? "-", Stamp ?? "-", body.Length,
                body.Length > 0 ? body[..Math.Min(body.Length, 300)] : "(empty)");

            var result = await _push.HandleDataPushAsync(sn, table, body, Stamp, ct);
            return DeviceReply(result);
        }

        /// <summary>Device polls for queued commands (standard path).</summary>
        [HttpGet("getrequest")]
        public Task<IActionResult> GetRequest(
            [FromQuery(Name = "SN")] string? sn,
            [FromQuery(Name = "INFO")] string? info,
            CancellationToken ct)
            => PollAsync(sn, info, "/iclock/getrequest", ct);

        /// <summary>Heartbeat + command poll — many ZKTeco firmwares use this instead of getrequest.</summary>
        [HttpGet("ping")]
        [HttpGet("request")]
        public Task<IActionResult> Ping(
            [FromQuery(Name = "SN")] string? sn,
            [FromQuery(Name = "INFO")] string? info,
            CancellationToken ct)
            => PollAsync(sn, info, "/iclock/ping", ct);

        private async Task<IActionResult> PollAsync(string? sn, string? info, string endpoint, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            await LogHitAsync(sn, endpoint);
            var result = await _push.HandlePollAsync(sn, info, ct);
            return DeviceReply(result);
        }

        private ContentResult DeviceReply(string text, bool forcePush = false)
        {
            var body = text.Replace("\r\n", "\n");
            var accept = Request.Headers.Accept.ToString();
            var agent = Request.Headers.UserAgent.ToString();
            var usePush = forcePush
                || accept.Contains("application/push", StringComparison.OrdinalIgnoreCase)
                || agent.Contains("iClock", StringComparison.OrdinalIgnoreCase);
            return Content(body, usePush ? "application/push; charset=UTF-8" : "text/plain");
        }

        /// <summary>Modern firmware posts DATA QUERY results here.</summary>
        [HttpPost("querydata")]
        public async Task<IActionResult> QueryData(
            [FromQuery(Name = "SN")] string? sn,
            [FromQuery] string? table,
            [FromQuery] string? type,
            [FromQuery(Name = "cmdid")] string? cmdId,
            [FromQuery] string? tablename,
            [FromQuery] string? count,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            await LogHitAsync(sn, $"/iclock/querydata ({tablename ?? table ?? type ?? "data"})");
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);

            var effectiveTable = tablename ?? table ?? type;

            _logger.LogWarning(
                "QUERYDATA detail SN={SN} tablename={TN} table={T} type={Tp} count={Cnt} cmdid={Cmd} bodyLen={Len} bodyPreview={Preview}",
                sn, tablename ?? "-", table ?? "-", type ?? "-", count ?? "-", cmdId ?? "-", body.Length,
                body.Length > 0 ? body[..Math.Min(body.Length, 200)] : "(empty)");

            if (int.TryParse(count, out var rowCount) && !string.IsNullOrWhiteSpace(tablename))
            {
                await _push.UpdateQueryDataCountAsync(sn, tablename, rowCount);
            }

            // count-only announcement (no body) — just acknowledge, data comes in subsequent posts
            if (false && string.IsNullOrWhiteSpace(body))
            {
                _logger.LogInformation("QUERYDATA count-only SN={SN} table={TN} count={Cnt} — awaiting data posts",
                    sn, effectiveTable ?? "-", count ?? "-");
                return DeviceReply("OK");
            }

            var result = await _push.HandleQueryDataAsync(sn, effectiveTable, type, body, ct);
            return DeviceReply(result);
        }

        /// <summary>Device acknowledges executed commands.</summary>
        [HttpPost("devicecmd")]
        public async Task<IActionResult> DeviceCmd(
            [FromQuery(Name = "SN")] string? sn,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            await LogHitAsync(sn, "/iclock/devicecmd");
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync(ct);
            var result = await _push.HandleDeviceCmdAsync(sn, body, ct);
            return DeviceReply(result);
        }

        /// <summary>Optional registry endpoint some firmwares call.</summary>
        [HttpGet("registry")]
        [HttpPost("registry")]
        public async Task<IActionResult> Registry(
            [FromQuery(Name = "SN")] string? sn,
            CancellationToken ct)
        {
            var body = "";
            if (HttpMethods.IsPost(Request.Method))
            {
                using var reader = new StreamReader(Request.Body);
                body = await reader.ReadToEndAsync(ct);
            }

            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            var hasToken = Request.Headers.Cookie.ToString()
                .Contains("token=", StringComparison.OrdinalIgnoreCase);

            await LogHitAsync(sn, "/iclock/registry");
            var (result, sessionId) = await _push.HandleRegistryAsync(sn, body, hasToken, ct);
            SetPushSessionCookie(sessionId);
            return DeviceReply(result, forcePush: true);
        }

        /// <summary>Security PUSH 7.5 — device downloads server config after RegistryCode.</summary>
        [HttpGet("push")]
        [HttpPost("push")]
        public async Task<IActionResult> PushConfig(
            [FromQuery(Name = "SN")] string? sn,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sn))
                return DeviceReply("OK");

            await LogHitAsync(sn, "/iclock/push");
            var (result, sessionId) = await _push.HandlePushConfigAsync(sn, ct);
            SetPushSessionCookie(sessionId);
            return DeviceReply(result, forcePush: true);
        }

        private void SetPushSessionCookie(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            Response.Headers.Append("Set-Cookie", $"JSESSIONID={sessionId}; Path=/; HttpOnly");
        }

        private async Task LogHitAsync(string serial, string endpoint)
        {
            var registered = await _readers.GetBySerialAsync(serial.Trim());
            var ip = GetClientIp();
            _connectionLog.Record(new ReaderConnectionEvent(
                ReceivedAt: DateTime.Now,
                DeviceSerial: serial.Trim(),
                RemoteIp: ip,
                Endpoint: endpoint,
                HttpMethod: Request.Method,
                IsRegistered: registered != null,
                Note: registered == null ? "SN غير مسجّل في النظام" : null));

            _logger.LogInformation(
                "Push hit {Endpoint} SN={Sn} from {Ip} registered={Reg}",
                endpoint, serial, ip, registered != null);
        }

        private string GetClientIp()
        {
            var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            var ip = HttpContext.Connection.RemoteIpAddress;
            if (ip == null) return "unknown";
            // Kestrel often reports IPv4 as ::ffff:10.205.0.49 — normalize for the UI.
            if (ip.IsIPv4MappedToIPv6)
                ip = ip.MapToIPv4();
            return ip.ToString();
        }
    }
}
