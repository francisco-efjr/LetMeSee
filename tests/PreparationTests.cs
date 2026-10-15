using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LetMeSee;

internal static class PreparationTests
{
    private static int _passed;
    private static int _failed;
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-L")
        {
            int port = Int32.Parse(args[1].Substring(args[1].LastIndexOf(':') + 1));
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(); WritePid("server");
            while (true) { using (TcpClient client = listener.AcceptTcpClient()) { } }
        }
        if (args.Length > 0 && args[0] == "-H") { Console.WriteLine("List of devices attached\nSERIAL001\tdevice product:fixture model:Test_Phone transport_id:1\n"); return 0; }
        if (args.Length > 0 && args[0] == "--serial") { WritePid("mirror"); Thread.Sleep(Timeout.Infinite); return 0; }
        if (args.Length > 0 && args[0] == "-unrelated") { Thread.Sleep(Timeout.Infinite); return 0; }
        Run("selection validates device, method and serial boundaries", Selection);
        Run("clean system and saved confirmations cannot bypass missing runtime", CleanSystem);
        Run("guided and technical modes require the same safety checks", Experience);
        Run("Windows, macOS and unsupported host matrix", Hosts);
        Run("public, offline and unknown networks", Networks);
        Run("Bonjour, firewall and port evidence cannot be overridden", AirPlayPrerequisites);
        Run("unknown AirPlay evidence requires explicit manual review", UnknownAirPlayPrerequisites);
        Run("unauthorized, offline, missing and emulator Android", DeviceStates);
        Run("multiple Androids require explicit serial and stale serial never switches", DeviceSelection);
        Run("USB and already paired wireless methods stay separate", Transports);
        Run("adb parsing validates documented rows and rejects ambiguity", Parsing);
        Run("scrcpy arguments enforce view-only with no clipboard", Arguments);
        Run("complete Android inventory covers server, libraries and data", Inventory);
        Run("Android manifest rejects traversal, alias and duplicate paths", Traversal);
        Run("cancelled startup and unsupported host never launch", Cancellation);
        if (Environment.OSVersion.Platform == PlatformID.Win32NT && PreparationProbe.CurrentHost() == HostPlatform.Windows)
            Run("private ADB and owned mirror stop without touching unrelated sessions", OwnedProcesses);
        else Console.WriteLine("SKIP Windows private-process lifecycle: requires Windows 10/11.");
        Console.WriteLine("Passed: " + _passed + "; Failed: " + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static PreparationSelection Android(MirrorMethod method)
    { return new PreparationSelection { Device = DeviceFamily.Android, Method = method }; }
    private static PreparationConfirmations Confirmed()
    { return new PreparationConfirmations { PrivacyReady = true, TrustedNetwork = true, AirPlayReady = true, UsbDebuggingReady = true, WirelessPaired = true, BonjourReviewed = true, FirewallReviewed = true }; }
    private static PreparationSnapshot Ready()
    {
        PreparationSnapshot snapshot = new PreparationSnapshot { Host = HostPlatform.Windows, AirPlayRuntimeReady = true, AndroidRuntimeReady = true, HasNetwork = true, NetworkProfileKnown = true,
            FirewallStatusKnown = true, FirewallReady = true, BonjourStatusKnown = true, BonjourReady = true, AirPlayPortsKnown = true, AirPlayPortsAvailable = true };
        snapshot.AndroidDevices.Add(new AndroidDevice("SERIAL001", "Phone", AndroidDeviceState.Device, false));
        return snapshot;
    }
    private static PreparationReport Report(PreparationSelection selection, PreparationSnapshot snapshot)
    { return PreparationChecklist.Evaluate(selection, Confirmed(), snapshot); }
    private static void Selection()
    {
        string error;
        PreparationSelection selection = Android(MirrorMethod.AndroidUsb);
        Assert(selection.Validate(out error), "valid Android USB");
        selection.Method = MirrorMethod.AirPlay;
        Assert(!selection.Validate(out error), "device and method mismatch");
        selection.Method = (MirrorMethod)999;
        Assert(!selection.Validate(out error), "unknown method");
        selection = Android(MirrorMethod.AndroidUsb);
        foreach (string serial in new[] { "-d", "a b", "a\"b", "x\ny", "x&evil", "x|evil", "$(evil)", new String('a', 256), "a\\b" })
        { selection.AndroidSerial = serial; Assert(!selection.Validate(out error), "invalid serial"); }
        foreach (string serial in new[] { "ABC123", "192.168.1.2:44231", "[fe80::1%4]:44231", "adb-SERIAL-random._adb-tls-connect._tcp" })
        { selection.AndroidSerial = serial; Assert(selection.Validate(out error), "documented serial forms"); }
        selection.AndroidSerial = null; selection.AndroidRuntimeDirectory = "relative-folder";
        Assert(!selection.Validate(out error), "import directory must be explicit and rooted");
        Assert(!Object.ReferenceEquals(selection, selection.Clone()), "selection snapshot");
    }
    private static void CleanSystem()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.AirPlayRuntimeReady = false;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "saved confirmations do not bypass missing AirPlay");
        snapshot = Ready(); snapshot.AndroidRuntimeReady = false;
        Assert(!Report(Android(MirrorMethod.AndroidUsb), snapshot).CanStart, "saved confirmations do not bypass missing Android");
        Assert(!PreparationChecklist.Evaluate(Android(MirrorMethod.AndroidUsb), new PreparationConfirmations(), Ready()).CanStart, "first run needs manual device and privacy confirmations");
        Assert(!PreparationChecklist.Evaluate(new PreparationSelection(), new PreparationConfirmations(), Ready()).CanStart, "first iOS run needs manual confirmations");
    }
    private static void Experience()
    {
        foreach (ExperienceLevel level in new[] { ExperienceLevel.Guided, ExperienceLevel.Technical })
        {
            PreparationSelection selection = Android(MirrorMethod.AndroidUsb); selection.Experience = level;
            Assert(Report(selection, Ready()).CanStart, "fully prepared user can start");
            PreparationConfirmations confirmations = Confirmed(); confirmations.PrivacyReady = false;
            Assert(!PreparationChecklist.Evaluate(selection, confirmations, Ready()).CanStart, "expert cannot skip privacy");
        }
    }
    private static void Hosts()
    {
        foreach (HostPlatform host in new[] { HostPlatform.Windows, HostPlatform.MacOS, HostPlatform.Unsupported })
        {
            foreach (PreparationSelection selection in new[] { new PreparationSelection(), Android(MirrorMethod.AndroidUsb) })
            { PreparationSnapshot snapshot = Ready(); snapshot.Host = host; Assert(Report(selection, snapshot).CanStart == (host == HostPlatform.Windows), "Windows executable host gate"); }
        }
        foreach (MirrorMethod method in new[] { MirrorMethod.MacNativeAirPlay, MirrorMethod.MacQuickTime })
        { PreparationSnapshot snapshot = Ready(); snapshot.Host = HostPlatform.MacOS; Assert(!Report(new PreparationSelection { Method = method }, snapshot).CanStart, "native Mac guides do not pretend WinForms executes on Mac"); }
    }
    private static void Networks()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.HasNetwork = false;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "AirPlay requires network");
        Assert(Report(Android(MirrorMethod.AndroidUsb), snapshot).CanStart, "USB works offline");
        snapshot = Ready(); snapshot.HasPublicNetwork = true;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "public profile blocks even when confirmed trusted");
        Assert(Report(Android(MirrorMethod.AndroidUsb), snapshot).CanStart, "public profile does not block cable");
        snapshot = Ready(); snapshot.HasDomainNetwork = true;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "domain profile cannot use private-only AirPlay rules");
        Assert(Report(Android(MirrorMethod.AndroidUsb), snapshot).CanStart, "domain profile does not block cable");
        snapshot = Ready(); snapshot.HasVpnOrTunnel = true;
        Assert(Report(new PreparationSelection(), snapshot).CanStart, "VPN is warning rather than silent mutation");
        snapshot.NetworkProfileKnown = false;
        PreparationConfirmations confirmations = Confirmed(); confirmations.TrustedNetwork = false;
        Assert(!PreparationChecklist.Evaluate(new PreparationSelection(), confirmations, snapshot).CanStart, "unknown network requires manual review");
    }
    private static void AirPlayPrerequisites()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.BonjourReady = false;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "known missing Bonjour cannot be confirmed away");
        snapshot = Ready(); snapshot.FirewallReady = false;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "known missing firewall cannot be confirmed away");
        snapshot = Ready(); snapshot.AirPlayPortsAvailable = false;
        Assert(!Report(new PreparationSelection(), snapshot).CanStart, "known port conflict cannot be confirmed away");
    }
    private static void UnknownAirPlayPrerequisites()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.BonjourStatusKnown = false; snapshot.FirewallStatusKnown = false;
        PreparationConfirmations confirmations = Confirmed(); confirmations.BonjourReviewed = false; confirmations.FirewallReviewed = false;
        Assert(!PreparationChecklist.Evaluate(new PreparationSelection(), confirmations, snapshot).CanStart, "unknown services require explicit manual checks");
        confirmations.BonjourReviewed = true;
        Assert(!PreparationChecklist.Evaluate(new PreparationSelection(), confirmations, snapshot).CanStart, "firewall independently required");
        confirmations.FirewallReviewed = true;
        Assert(PreparationChecklist.Evaluate(new PreparationSelection(), confirmations, snapshot).CanStart, "manual review permits unknown evidence only");
    }
    private static void DeviceStates()
    {
        foreach (AndroidDeviceState state in new[] { AndroidDeviceState.Unauthorized, AndroidDeviceState.Offline, AndroidDeviceState.Unavailable })
        {
            PreparationSnapshot snapshot = Ready(); snapshot.AndroidDevices.Clear(); snapshot.AndroidDevices.Add(new AndroidDevice("A", "Phone", state, false));
            Assert(!Report(Android(MirrorMethod.AndroidUsb), snapshot).CanStart, "unusable authorization state cannot pass");
        }
        PreparationSnapshot empty = Ready(); empty.AndroidDevices.Clear();
        Assert(!Report(Android(MirrorMethod.AndroidUsb), empty).CanStart, "no device blocks");
        empty.AndroidDevices.Add(new AndroidDevice("emulator-5554", "Emulator", AndroidDeviceState.Device, false));
        Assert(!Report(Android(MirrorMethod.AndroidUsb), empty).CanStart, "emulator does not silently replace phone");
    }
    private static void DeviceSelection()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.AndroidDevices.Add(new AndroidDevice("SECOND", "Phone", AndroidDeviceState.Device, false));
        PreparationSelection selection = Android(MirrorMethod.AndroidUsb);
        Assert(!Report(selection, snapshot).CanStart, "ambiguous device selection blocks");
        selection.AndroidSerial = "SECOND";
        Assert(Report(selection, snapshot).CanStart && Report(selection, snapshot).SelectedAndroidSerial == "SECOND", "explicit serial selected");
        snapshot.AndroidDevices.RemoveAt(1);
        Assert(!Report(selection, snapshot).CanStart, "stale serial never falls back to another phone");
        selection.AndroidSerial = null;
        Assert(Report(selection, snapshot).SelectedAndroidSerial == "SERIAL001", "single serial pinned before execution");
    }
    private static void Transports()
    {
        PreparationSnapshot snapshot = Ready(); snapshot.AndroidDevices.Add(new AndroidDevice("192.168.1.2:42111", "Phone", AndroidDeviceState.Device, true));
        PreparationSelection selection = Android(MirrorMethod.AndroidWireless);
        Assert(Report(selection, snapshot).CanStart && Report(selection, snapshot).SelectedAndroidSerial == "192.168.1.2:42111", "wireless selects authorized endpoint");
        selection.AndroidSerial = "SERIAL001";
        Assert(!Report(selection, snapshot).CanStart, "USB serial cannot start wireless method");
        selection.AndroidSerial = null;
        snapshot.HasNetwork = false;
        Assert(!Report(selection, snapshot).CanStart, "wireless requires active local network");
        snapshot.HasNetwork = true;
        PreparationConfirmations confirmations = Confirmed(); confirmations.WirelessPaired = false;
        Assert(!PreparationChecklist.Evaluate(selection, confirmations, snapshot).CanStart, "wireless pairing cannot be inferred from checkbox-free flow");
    }
    private static void Parsing()
    {
        IList<AndroidDevice> devices = AndroidDevicesParser.Parse("* daemon started successfully\nList of devices attached\nABC\tdevice product:x model:Pixel_9 transport_id:1\nDEF unauthorized\n192.168.1.2:42111 offline\nadb-ABC-X._adb-tls-connect._tcp device model:Pixel\n");
        Assert(devices.Count == 4 && devices[0].Model == "Pixel_9" && devices[1].State == AndroidDeviceState.Unauthorized && devices[2].IsWireless && devices[3].IsWireless, "documented devices and TLS discovery parsed");
        foreach (string output in new[] { "not adb", "List of devices attached\nABC device\nABC device", "List of devices attached\n-evil device", "List of devices attached\nABC\" device", new String('a', 65537) })
            Reject(delegate { AndroidDevicesParser.Parse(output); });
        Assert(AndroidDevicesParser.Parse("List of devices attached\nABC no permissions\n")[0].State == AndroidDeviceState.Unavailable, "unknown state fails closed");
    }
    private static void Arguments()
    {
        string arguments = AndroidArguments.Build("192.168.1.2:42111", true);
        Assert(arguments == "--serial \"192.168.1.2:42111\" --no-control --no-clipboard-autosync --window-title LetMeSee --no-audio", "exact readonly arguments");
        Assert(!AndroidArguments.Build("ABC", false).Contains("--no-audio"), "mute independent of transport");
        foreach (string serial in new[] { "-d", "ABC --tcpip", "x&kill", "x\"y", "" }) Reject(delegate { AndroidArguments.Build(serial, false); });
    }
    private static void Inventory()
    {
        WithDirectory(delegate(string directory)
        {
            string[] files = { "scrcpy.exe", "adb.exe", "scrcpy-server", "codec.dll", "icon.png" };
            foreach (string file in files) File.WriteAllText(Path.Combine(directory, file), "fixture:" + file);
            Manifest(directory, files);
            Assert(AndroidRuntimeVerifier.Verify(directory).IsValid, "all runtime files verified");
            File.AppendAllText(Path.Combine(directory, "scrcpy-server"), "changed");
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "extensionless Android server tampering blocks");
            Manifest(directory, files);
            File.WriteAllText(Path.Combine(directory, "unlisted.txt"), "injection");
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "complete inventory includes data");
            File.Delete(Path.Combine(directory, "unlisted.txt"));
            File.WriteAllText(Path.Combine(directory, "run.cmd"), "fixture");
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "unlisted scripts blocked");
            File.Delete(Path.Combine(directory, "run.cmd"));
            File.Delete(Path.Combine(directory, "scrcpy-server"));
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "missing server blocks");
            Manifest(directory, "scrcpy.exe", "adb.exe");
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "server must be in manifest");
        });
    }
    private static void Traversal()
    {
        WithDirectory(delegate(string directory)
        {
            foreach (string file in new[] { "scrcpy.exe", "adb.exe", "scrcpy-server" }) File.WriteAllText(Path.Combine(directory, file), "fixture");
            Manifest(directory, "scrcpy.exe", "adb.exe", "scrcpy-server");
            string valid = File.ReadAllText(Path.Combine(directory, "runtime-files.sha256"));
            foreach (string path in new[] { "../file.dll", "..\\file.dll", "/absolute", "C:/outside", "x//file", "./file", "file.dll:stream", "file.", "file ", "NUL.dll", "COM1.dll", "x/LPT2.txt" })
            { File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), valid + new String('0', 64) + "  " + path + "\n"); Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "unsafe or alias path rejected"); }
            File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), valid + Hash(Path.Combine(directory, "adb.exe")) + "  ADB.EXE\n");
            Assert(!AndroidRuntimeVerifier.Verify(directory).IsValid, "case-insensitive duplicates rejected");
        });
    }
    private static void Cancellation()
    {
        WithDirectory(delegate(string directory)
        {
            using (AndroidMirrorService service = new AndroidMirrorService(directory))
            {
                service.Start(new AppSettings(), Android(MirrorMethod.AndroidUsb), Confirmed());
                service.Stop(); Thread.Sleep(100);
                Assert(service.State == ReceiverState.Stopped && !service.HasOwnedProcess, "cancelled probe cannot publish stale errors or start");
            }
        });
    }
    private static void OwnedProcesses()
    {
        WithDirectory(delegate(string directory)
        {
            string runtime = Path.Combine(directory, "engines", "android"); Directory.CreateDirectory(runtime);
            File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(runtime, "adb.exe"));
            File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(runtime, "scrcpy.exe"));
            File.WriteAllText(Path.Combine(runtime, "scrcpy-server"), "fixture");
            Manifest(runtime, "adb.exe", "scrcpy.exe", "scrcpy-server");
            string previous = Environment.GetEnvironmentVariable("LETMESEE_TEST_PID_DIRECTORY");
            Environment.SetEnvironmentVariable("LETMESEE_TEST_PID_DIRECTORY", directory);
            ProcessStartInfo info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "-unrelated"); info.UseShellExecute = false; info.CreateNoWindow = true;
            using (Process unrelated = Process.Start(info))
            using (AndroidMirrorService service = new AndroidMirrorService(directory))
            {
                try
                {
                    service.Start(new AppSettings { Mute = true }, Android(MirrorMethod.AndroidUsb), Confirmed());
                    Wait(delegate { return service.State == ReceiverState.Running || service.State == ReceiverState.Error; }, "start");
                    Assert(service.State == ReceiverState.Running && service.HasOwnedProcess, service.StatusMessage);
                    Wait(delegate { return File.Exists(Path.Combine(directory, "mirror.pid")); }, "mirror fixture");
                    int mirror = Int32.Parse(File.ReadAllText(Path.Combine(directory, "mirror.pid")));
                    int server = Int32.Parse(File.ReadAllText(Path.Combine(directory, "server.pid")));
                    service.Stop();
                    Assert(service.State == ReceiverState.Stopped && !service.HasOwnedProcess, "owned shutdown confirmed");
                    Wait(delegate { return !Exists(mirror) && !Exists(server); }, "owned server and mirror exit");
                    Assert(!unrelated.HasExited, "unrelated Android session preserved");
                }
                finally { if (!unrelated.HasExited) { unrelated.Kill(); unrelated.WaitForExit(2000); } Environment.SetEnvironmentVariable("LETMESEE_TEST_PID_DIRECTORY", previous); }
            }
        });
    }
    private static void WritePid(string kind)
    { string directory = Environment.GetEnvironmentVariable("LETMESEE_TEST_PID_DIRECTORY"); if (!String.IsNullOrEmpty(directory)) File.WriteAllText(Path.Combine(directory, kind + ".pid"), Process.GetCurrentProcess().Id.ToString()); }
    private static bool Exists(int id)
    { try { using (Process process = Process.GetProcessById(id)) return !process.HasExited; } catch (ArgumentException) { return false; } }
    private static void Wait(Func<bool> condition, string reason)
    { Stopwatch watch = Stopwatch.StartNew(); while (!condition()) { if (watch.ElapsedMilliseconds > 8000) throw new Exception("Timed out: " + reason); Thread.Sleep(25); } }
    private static void Manifest(string directory, params string[] files)
    { StringBuilder text = new StringBuilder(); foreach (string file in files) text.Append(Hash(Path.Combine(directory, file))).Append("  ").Append(file).Append('\n'); File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), text.ToString(), new UTF8Encoding(false)); }
    private static string Hash(string path)
    { using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
    private static void WithDirectory(Action<string> test)
    { string directory = Path.Combine(Path.GetTempPath(), "LetMeSee-preparation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); try { test(directory); } finally { try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } } }
    private static void Reject(Action test)
    { bool rejected = false; try { test(); } catch (ArgumentException) { rejected = true; } Assert(rejected, "unsafe input rejected"); }
    private static void Assert(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static void Run(string name, Action test)
    { try { test(); _passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { _failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
}
