using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PMS.web
{
    /// <summary>
    /// Resolves the machine's usable LAN IPv4 addresses so the UI and startup log can show
    /// the ADMS URL a ZKTeco reader must target (a reader can never reach localhost).
    /// </summary>
    public static class HostAddressInfo
    {
        public static IReadOnlyList<string> GetLanAddresses()
        {
            var result = new List<string>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;

                    var text = addr.Address.ToString();
                    if (text.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                    if (!result.Contains(text)) result.Add(text);
                }
            }

            return result;
        }
    }
}
