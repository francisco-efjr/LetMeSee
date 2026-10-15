using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace LetMeSee
{
    public sealed class PreparationProbe : IDisposable
    {
        private readonly string _applicationDirectory;
        private AndroidBridge _bridge;
        private bool _disposed;
        public PreparationProbe(string applicationDirectory) { _applicationDirectory = Path.GetFullPath(applicationDirectory); }

        public PreparationSnapshot Inspect(PreparationSelection selection) { return Inspect(selection, CancellationToken.None); }
        public PreparationSnapshot Inspect(PreparationSelection selection, CancellationToken cancellation)
        {
            if (_disposed) throw new ObjectDisposedException("PreparationProbe");
            if (selection == null) throw new ArgumentNullException("selection");
            if (_bridge != null) { _bridge.Dispose(); _bridge = null; }
            PreparationSnapshot snapshot = new PreparationSnapshot();
            snapshot.Host = CurrentHost();
            InspectNetwork(snapshot);
            string error;
            if (!selection.Validate(out error)) { snapshot.ProbeMessage = error; return snapshot; }
            cancellation.ThrowIfCancellationRequested();
            if (snapshot.Host != HostPlatform.Windows) return snapshot;
            if (selection.Device == DeviceFamily.IOS)
            {
                RuntimeVerificationResult result = RuntimeVerifier.Verify(Path.Combine(_applicationDirectory, "bin"));
                snapshot.AirPlayRuntimeReady = result.IsValid;
                snapshot.AirPlayRuntimeMessage = result.Message;
                InspectAirPlayPorts(snapshot);
                cancellation.ThrowIfCancellationRequested();
                InspectWindowsNetwork(snapshot, cancellation);
            }
            else
            {
                string directory = String.IsNullOrWhiteSpace(selection.AndroidRuntimeDirectory)
                    ? Path.Combine(_applicationDirectory, "engines", "android") : Path.GetFullPath(selection.AndroidRuntimeDirectory);
                RuntimeVerificationResult result = AndroidRuntimeVerifier.Verify(directory);
                snapshot.AndroidRuntimeReady = result.IsValid;
                snapshot.AndroidRuntimeMessage = result.Message;
                if (selection.Method == MirrorMethod.AndroidWireless) InspectWindowsNetwork(snapshot, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (result.IsValid)
                {
                    try
                    {
                        _bridge = new AndroidBridge(directory, cancellation);
                        snapshot.AndroidDevices = _bridge.ReadDevices(cancellation);
                        // mDNS discovery is asynchronous; give an already paired wireless device a bounded second chance.
                        if (selection.Method == MirrorMethod.AndroidWireless && snapshot.AndroidDevices.Count == 0)
                        {
                            if (cancellation.WaitHandle.WaitOne(500)) cancellation.ThrowIfCancellationRequested();
                            snapshot.AndroidDevices = _bridge.ReadDevices(cancellation);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException || ex is SocketException || ex is System.Security.SecurityException)) throw;
                        if (_bridge != null) { _bridge.Dispose(); _bridge = null; }
                        snapshot.ProbeMessage = "A consulta Android falhou. Verifique o pacote, o cabo e as politicas locais; o servidor ADB de outros programas nao sera reiniciado.";
                    }
                }
            }
            cancellation.ThrowIfCancellationRequested();
            return snapshot;
        }

        internal AndroidBridge TakeAndroidBridge()
        { AndroidBridge bridge = _bridge; _bridge = null; return bridge; }

        public static HostPlatform CurrentHost()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return Environment.OSVersion.Version.Major >= 10 ? HostPlatform.Windows : HostPlatform.Unsupported;
            return File.Exists("/System/Library/CoreServices/SystemVersion.plist") ? HostPlatform.MacOS : HostPlatform.Unsupported;
        }
        private static void InspectNetwork(PreparationSnapshot snapshot)
        {
            try
            {
                foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (network.NetworkInterfaceType == NetworkInterfaceType.Tunnel || network.NetworkInterfaceType == NetworkInterfaceType.Ppp) snapshot.HasVpnOrTunnel = true;
                    foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
                        if (!IPAddress.IsLoopback(address.Address) && address.Address.AddressFamily == AddressFamily.InterNetwork && !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) snapshot.HasNetwork = true;
                }
            }
            catch (NetworkInformationException) { snapshot.ProbeMessage = "Nao foi possivel consultar os adaptadores de rede. Confira a conexao local manualmente."; }
            catch (System.Security.SecurityException) { snapshot.ProbeMessage = "A politica local impediu a consulta dos adaptadores de rede."; }
        }

        private static void InspectAirPlayPorts(PreparationSnapshot snapshot)
        {
            try
            {
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                bool available = true;
                foreach (IPEndPoint endpoint in properties.GetActiveTcpListeners())
                    if (endpoint.Port == 7000 || endpoint.Port == 7001 || endpoint.Port == 7100) available = false;
                foreach (IPEndPoint endpoint in properties.GetActiveUdpListeners())
                    if (endpoint.Port == 6000 || endpoint.Port == 6001 || endpoint.Port == 7011) available = false;
                snapshot.AirPlayPortsKnown = true;
                snapshot.AirPlayPortsAvailable = available;
            }
            catch (NetworkInformationException) { }
            catch (System.Security.SecurityException) { }
        }

        private void InspectWindowsNetwork(PreparationSnapshot snapshot, CancellationToken cancellation)
        {
            // Constant read-only command. The expected path travels as environment data, never interpolated code.
            const string command = "$ErrorActionPreference='Stop'; " +
                "try { $p=@(Get-NetConnectionProfile); if($p.Count -gt 0){foreach($n in $p){'PROFILE:'+ $n.NetworkCategory}} } catch {} ; " +
                "try { $b=Get-Service -Name 'Bonjour Service' -ErrorAction SilentlyContinue; 'BONJOUR:'+ [int]($null -ne $b -and $b.Status -eq 'Running') } catch {} ; " +
                "try { $ok=$true; foreach($spec in @(@('LetMeSee-AirPlay-TCP','TCP','7000,7001,7100'),@('LetMeSee-AirPlay-UDP','UDP','6000,6001,7011'))) { " +
                "$r=Get-NetFirewallRule -Name $spec[0] -ErrorAction SilentlyContinue; if($null -eq $r -or $r.Enabled -ne 'True' -or $r.Direction -ne 'Inbound' -or $r.Action -ne 'Allow' -or [string]$r.Profile -ne 'Private'){$ok=$false;continue}; " +
                "$a=$r|Get-NetFirewallApplicationFilter; $d=$r|Get-NetFirewallAddressFilter; $t=$r|Get-NetFirewallPortFilter; " +
                "if($a.Program -ne $env:LETMESEE_EXPECTED_ENGINE -or @($d.RemoteAddress).Count -ne 1 -or $d.RemoteAddress -ne 'LocalSubnet' -or $t.Protocol -ne $spec[1] -or ((@($t.LocalPort)|Sort-Object) -join ',') -ne $spec[2]){$ok=$false} }; " +
                "'FIREWALL:'+ [int]$ok } catch {}";
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powershell)) return;
            try
            {
                using (EngineJob job = new EngineJob())
                using (Process process = new Process())
                {
                    ProcessStartInfo info = new ProcessStartInfo();
                    info.FileName = powershell;
                    info.Arguments = "-NoLogo -NoProfile -NonInteractive -Command " + UxPlayArguments.Quote(command);
                    info.UseShellExecute = false;
                    info.CreateNoWindow = true;
                    info.RedirectStandardOutput = true;
                    info.RedirectStandardError = true;
                    // Read-only network checks must resolve Windows modules, never a user's shadow module.
                    info.EnvironmentVariables["PSModulePath"] = Path.Combine(Path.GetDirectoryName(powershell), "Modules");
                    info.EnvironmentVariables["PATH"] = Environment.SystemDirectory + ";" + Path.GetDirectoryName(powershell);
                    info.EnvironmentVariables["LETMESEE_EXPECTED_ENGINE"] = Path.Combine(_applicationDirectory, "bin", "uxplay.exe");
                    process.StartInfo = info;
                    if (!process.Start()) return;
                    try
                    {
                        job.Assign(process);
                        BoundedProcessOutput output = new BoundedProcessOutput(process.StandardOutput, 8192);
                        BoundedProcessOutput errors = new BoundedProcessOutput(process.StandardError, 8192);
                        Stopwatch timeout = Stopwatch.StartNew();
                        while (!process.WaitForExit(50))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (timeout.ElapsedMilliseconds > 8000) return;
                        }
                        if (process.ExitCode != 0) return;
                        using (StringReader reader = new StringReader(output.Complete()))
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                if (line == "PROFILE:Public") { snapshot.NetworkProfileKnown = true; snapshot.HasPublicNetwork = true; }
                                else if (line == "PROFILE:Private") snapshot.NetworkProfileKnown = true;
                                else if (line == "PROFILE:DomainAuthenticated") { snapshot.NetworkProfileKnown = true; snapshot.HasDomainNetwork = true; }
                                else if (line == "BONJOUR:1" || line == "BONJOUR:0") { snapshot.BonjourStatusKnown = true; snapshot.BonjourReady = line == "BONJOUR:1"; }
                                else if (line == "FIREWALL:1" || line == "FIREWALL:0") { snapshot.FirewallStatusKnown = true; snapshot.FirewallReady = line == "FIREWALL:1"; }
                            }
                        }
                        errors.Complete();
                    }
                    finally { AndroidBridge.KillOwned(process); }
                }
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException || ex is System.Security.SecurityException)) throw;
                // Unknown is explicitly presented by the checklist; no mutation or elevation is attempted.
            }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_bridge != null) { _bridge.Dispose(); _bridge = null; }
        }
    }
}
