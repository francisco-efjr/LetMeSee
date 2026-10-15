using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LetMeSee
{
    public sealed class NetworkAddress
    {
        public string Name { get; private set; }
        public string Address { get; private set; }
        internal NetworkAddress(string name, string address) { Name = name; Address = address; }
        public override string ToString() { return Address + " (" + Name + ")"; }
    }

    public static class NetworkService
    {
        public static List<NetworkAddress> GetActiveIPv4()
        {
            List<NetworkAddress> addresses = new List<NetworkAddress>();
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.IsReceiveOnly ||
                        (adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet && adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)) continue;
                    // Managed .NET does not expose a definitive hardware flag. Exclude known virtual adapters.
                    string description = (adapter.Description + " " + adapter.Name).ToLowerInvariant();
                    if (description.Contains("virtual") || description.Contains("vpn") || description.Contains("tap-") ||
                        description.Contains("tunnel") || description.Contains("wireguard") || description.Contains("tailscale") || description.Contains("zerotier")) continue;
                    foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        byte[] bytes = unicast.Address.GetAddressBytes();
                        if (bytes[0] == 127 || bytes[0] == 0 || (bytes[0] == 169 && bytes[1] == 254)) continue;
                        addresses.Add(new NetworkAddress(adapter.Name, unicast.Address.ToString()));
                    }
                }
            }
            catch (NetworkInformationException) { }
            catch (System.Security.SecurityException) { }
            addresses.Sort(delegate(NetworkAddress a, NetworkAddress b) { return String.Compare(a.Name + a.Address, b.Name + b.Address, StringComparison.Ordinal); });
            return addresses;
        }
    }

    public sealed class DiagnosticReport
    {
        public string Runtime { get; internal set; }
        public string Network { get; internal set; }
        public string Bonjour { get; internal set; }
        public List<NetworkAddress> Addresses { get; internal set; }
    }

    public static class DiagnosticService
    {
        public static DiagnosticReport Inspect(string applicationDirectory)
        {
            List<NetworkAddress> addresses = NetworkService.GetActiveIPv4();
            return new DiagnosticReport
            {
                Runtime = RuntimeVerifier.Verify(System.IO.Path.Combine(applicationDirectory, "bin")).Message,
                Network = addresses.Count > 0 ? "Interface local IPv4 ativa. Confirme que o iPhone esta na mesma rede." : "Nenhuma interface Wi-Fi ou Ethernet IPv4 ativa foi identificada.",
                Bonjour = InspectBonjour(),
                Addresses = addresses
            };
        }

        private static string InspectBonjour()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return "Consulta do Bonjour disponivel somente no Windows.";
            IntPtr manager = OpenSCManager(null, null, 0x0001); // CONNECT, no mutation rights.
            if (manager == IntPtr.Zero) return "Bonjour: nao foi possivel consultar os servicos com esta conta.";
            try
            {
                IntPtr service = OpenService(manager, "Bonjour Service", 0x0004); // QUERY_STATUS only.
                if (service == IntPtr.Zero)
                    return Marshal.GetLastWin32Error() == 1060 ? "Bonjour nao instalado. Builds com mDNS interno podem dispensar este servico." : "Bonjour: consulta indisponivel com esta conta.";
                try
                {
                    ServiceStatus status;
                    if (!QueryServiceStatus(service, out status)) return "Bonjour: estado indisponivel.";
                    return status.CurrentState == 4 ? "Servico Bonjour em execucao." : "Servico Bonjour instalado, mas nao esta em execucao.";
                }
                finally { CloseServiceHandle(service); }
            }
            finally { CloseServiceHandle(manager); }
        }

        [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus
        { public uint ServiceType; public uint CurrentState; public uint ControlsAccepted; public uint Win32ExitCode; public uint ServiceSpecificExitCode; public uint CheckPoint; public uint WaitHint; }
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr OpenSCManager(string machineName, string databaseName, uint access);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr OpenService(IntPtr manager, string serviceName, uint access);
        [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
        [DllImport("advapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseServiceHandle(IntPtr handle);
    }
}
