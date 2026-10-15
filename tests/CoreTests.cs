using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LetMeSee;

internal static class CoreTests
{
    private static int _passed;
    private static int _failed;

    public static int Main(string[] args)
    {
        // Local, network-free engine fixture for process ownership tests.
        if (args.Length > 0 && args[0] == "-child")
        { File.WriteAllText(args[1], Process.GetCurrentProcess().Id.ToString()); Thread.Sleep(Timeout.Infinite); return 0; }
        if (args.Length > 1 && args[0] == "-n")
        {
            if (args[1] == "Exit immediately") return 37;
            if (args[1] == "Spawn child")
            {
                ProcessStartInfo info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    "-child " + UxPlayArguments.Quote(Path.Combine(Environment.CurrentDirectory, "child.pid")));
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                using (Process child = Process.Start(info)) { }
            }
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
        Run("safe defaults and validation", SettingsValidation);
        Run("Windows argument boundaries", Arguments);
        Run("settings round trip and unsafe XML", Persistence);
        Run("cryptographic session PIN boundaries", PinGeneration);
        Run("manifest integrity and complete executable inventory", Manifest);
        Run("manifest traversal and alias rejection", Traversal);
        Run("cancelled startup cannot reactivate", CancelStartup);
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Run("owned process and descendant shutdown", Lifecycle);
            Run("unexpected engine exit", UnexpectedExit);
        }
        Console.WriteLine("Passed: " + _passed + "; Failed: " + _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void SettingsValidation()
    {
        AppSettings settings = new AppSettings();
        Assert(settings.ReceiverName == "LetMeSee" && !settings.Mute && !settings.MinimizeToTray && settings.EnableAnimation && settings.RequirePin && settings.SessionLimitMinutes == 0, "safe defaults");
        string error;
        foreach (string name in new[] { "", " ", "\t", "abc\n", "a\"b", "a'b", "a\\b", "-d", " sala", "sala ", new String('a', 49), "a\u202Eb", "\uD800" })
        { settings.ReceiverName = name; Assert(!settings.Validate(out error), "reject unsafe receiver name"); }
        foreach (string name in new[] { "A", new String('x', 48), "Sala Águia", "Pulse & Radar | 01", "Sala \uD83D\uDCF1" })
        { settings.ReceiverName = name; Assert(settings.Validate(out error), "accept valid receiver name"); }
        settings.ReceiverName = "LetMeSee";
        foreach (int minutes in new[] { 0, 15, 30, 60 })
        { settings.SessionLimitMinutes = minutes; Assert(settings.Validate(out error), "accept supported session limit"); }
        settings.SessionLimitMinutes = 1;
        Assert(!settings.Validate(out error), "reject arbitrary session limit");
    }

    private static void Arguments()
    {
        AppSettings settings = new AppSettings { ReceiverName = "Sala & Pulse | 01" };
        string arguments = UxPlayArguments.Build(settings);
        Assert(arguments == "-n \"Sala & Pulse | 01\" -nh -p -vs d3d11videosink", "exact safe arguments; no hostname suffix");
        settings.Mute = true;
        Assert(UxPlayArguments.Build(settings).EndsWith(" -as 0", StringComparison.Ordinal), "documented audio mute option");
        settings.ReceiverName = "-d";
        ExpectArgumentException(delegate { UxPlayArguments.Build(settings); });
        if (Environment.OSVersion.Platform != PlatformID.Win32NT) return;
        string[] values = { "", "spaces here", "C:\\folder with spaces\\", "a\"b", "\\\\server\\folder\\", "$(literal) & | < > %PATH%", "Águia" };
        foreach (string value in values)
        {
            int count;
            IntPtr argv = CommandLineToArgvW("app.exe " + UxPlayArguments.Quote(value), out count);
            Assert(argv != IntPtr.Zero, "native Windows argument parsing");
            try
            {
                Assert(count == 2, "one quoted argument");
                Assert(Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, IntPtr.Size)) == value, "argument round trip");
            }
            finally { LocalFree(argv); }
        }
    }

    private static void Persistence()
    {
        WithDirectory(delegate(string directory)
        {
            SettingsStore store = new SettingsStore(Path.Combine(directory, "profile", "settings.xml"));
            Assert(store.Load().ReceiverName == "LetMeSee", "missing settings use defaults");
            AppSettings expected = new AppSettings { ReceiverName = "Sala Águia & 1", Mute = true, MinimizeToTray = true, EnableAnimation = false, RequirePin = false, SessionLimitMinutes = 30 };
            Assert(store.Save(expected), "persist settings");
            AppSettings actual = store.Load();
            Assert(actual.ReceiverName == expected.ReceiverName && actual.Mute && actual.MinimizeToTray && !actual.EnableAnimation && !actual.RequirePin && actual.SessionLimitMinutes == 30, "settings round trip");
            expected.ReceiverName = "Novo nome";
            Assert(store.Save(expected) && store.Load().ReceiverName == "Novo nome", "atomic settings replacement");
            string legacy = File.ReadAllText(store.SettingsPath).Replace("<RequirePin>False</RequirePin>", "");
            File.WriteAllText(store.SettingsPath, legacy);
            Assert(store.Load().RequirePin, "older settings safely enable session PIN");
            File.WriteAllText(store.SettingsPath, "<!DOCTYPE LetMeSee [<!ENTITY external SYSTEM 'file:///missing'>]><LetMeSee version='1'>&external;</LetMeSee>");
            Assert(store.Load().ReceiverName == "LetMeSee" && store.LastError != null, "DTD and external entity rejected");
            File.WriteAllText(store.SettingsPath, new String('x', 16385));
            Assert(store.Load().ReceiverName == "LetMeSee" && store.LastError != null, "oversized settings rejected");
            File.WriteAllText(store.SettingsPath, "<LetMeSee version='1'><ReceiverName>Sala</ReceiverName></LetMeSee>");
            Assert(store.Load().ReceiverName == "LetMeSee" && store.LastError != null, "missing required settings fields use safe defaults");
            File.WriteAllText(store.SettingsPath, "<LetMeSee version='99' />");
            Assert(store.Load().ReceiverName == "LetMeSee" && store.LastError != null, "unsupported settings version uses safe defaults");
        });
    }

    private static void PinGeneration()
    {
        HashSet<string> observed = new HashSet<string>();
        for (int i = 0; i < 128; i++)
        {
            string pin = ReceiverService.GenerateSessionPin();
            int value;
            Assert(pin.Length == 4 && Int32.TryParse(pin, out value) && value >= 1 && value <= 9999, "four-digit PIN accepted by engine");
            observed.Add(pin);
        }
        Assert(observed.Count > 1, "PIN generation does not return a fixed code");
    }

    private static void Manifest()
    {
        WithDirectory(delegate(string directory)
        {
            string engine = Path.Combine(directory, "uxplay.exe");
            File.WriteAllText(engine, "fixture-engine");
            Directory.CreateDirectory(Path.Combine(directory, "lib", "gstreamer-1.0"));
            string plugin = Path.Combine(directory, "lib", "gstreamer-1.0", "video.dll");
            File.WriteAllText(plugin, "fixture-plugin");
            WriteManifest(directory, "uxplay.exe", "lib/gstreamer-1.0/video.dll");
            RuntimeVerificationResult result = RuntimeVerifier.Verify(directory);
            Assert(result.IsValid && result.VerifiedFileCount == 2, "valid nested plugin inventory");
            File.AppendAllText(plugin, "tampered");
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "plugin tampering blocks start");
            WriteManifest(directory, "uxplay.exe", "lib/gstreamer-1.0/video.dll");
            File.WriteAllText(Path.Combine(directory, "unlisted.DLL"), "injected");
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "unlisted DLL blocked, case insensitive");
            File.Delete(Path.Combine(directory, "unlisted.DLL"));
            File.WriteAllText(Path.Combine(directory, "helper.ps1"), "Write-Host fixture");
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "unlisted script blocked");
            File.Delete(Path.Combine(directory, "helper.ps1"));
            File.Delete(engine);
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "missing engine blocked");
            File.WriteAllText(engine, "fixture-engine");
            WriteManifest(directory, "lib/gstreamer-1.0/video.dll");
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "engine must be listed");
            File.Delete(Path.Combine(directory, "runtime-files.sha256"));
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "manifest required");
        });
    }

    private static void Traversal()
    {
        WithDirectory(delegate(string directory)
        {
            File.WriteAllText(Path.Combine(directory, "uxplay.exe"), "fixture");
            string engineLine = Hash(Path.Combine(directory, "uxplay.exe")) + "  uxplay.exe\n";
            foreach (string relative in new[] { "../outside.dll", "..\\outside.dll", "/absolute.dll", "C:/outside.dll", "\\\\server\\share.dll", "plugins/../outside.dll", "plugins//file.dll", "./file.dll", "file.dll:stream", "file.dll.", "file.dll ", "plugins/./file.dll" })
            {
                File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), engineLine + new String('0', 64) + "  " + relative + "\n");
                Assert(!RuntimeVerifier.Verify(directory).IsValid, "unsafe manifest path blocked");
            }
            File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), engineLine + engineLine.Replace("uxplay.exe", "UXPLAY.EXE"));
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "case-insensitive duplicate blocked");
            File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), "not-a-valid-hash  uxplay.exe\n");
            Assert(!RuntimeVerifier.Verify(directory).IsValid, "malformed manifest blocked");
        });
    }

    private static void CancelStartup()
    {
        WithDirectory(delegate(string directory)
        {
            using (ReceiverService service = new ReceiverService(directory))
            {
                service.Start(new AppSettings());
                service.Stop();
                Thread.Sleep(150);
                Assert(service.State == ReceiverState.Stopped && !service.HasOwnedProcess, "cancelled verification cannot publish stale error or start a process");
            }
        });
    }

    private static void Lifecycle()
    {
        WithEngine(delegate(string directory)
        {
            string childFile = Path.Combine(directory, "bin", "child.pid");
            using (ReceiverService service = new ReceiverService(directory))
            {
                service.Start(new AppSettings { ReceiverName = "Spawn child" });
                Wait(delegate { return service.State == ReceiverState.Running || service.State == ReceiverState.Error; }, "engine startup");
                Assert(service.State == ReceiverState.Running && service.HasOwnedProcess, "owned running engine");
                Assert(service.SessionPin != null && service.SessionPin.Length == 4, "PIN exposed only for active session");
                int childId = 0;
                Wait(delegate
                {
                    try { return File.Exists(childFile) && Int32.TryParse(File.ReadAllText(childFile), out childId) && childId > 0; }
                    catch (IOException) { return false; }
                }, "child process startup");
                Stopwatch timer = Stopwatch.StartNew();
                service.Stop();
                Assert(timer.ElapsedMilliseconds < 3500, "bounded stop");
                Assert(service.State == ReceiverState.Stopped && !service.HasOwnedProcess, "confirmed process shutdown");
                Assert(service.SessionPin == null, "PIN cleared on stop");
                Wait(delegate { return !ProcessExists(childId); }, "descendant shutdown");
                service.Start(new AppSettings());
                Wait(delegate { return service.State == ReceiverState.Running || service.State == ReceiverState.Error; }, "engine restart");
                Assert(service.State == ReceiverState.Running, "restart after owned shutdown");
                service.Dispose();
                Assert(!service.HasOwnedProcess, "dispose releases owned process");
            }
        });
    }

    private static void UnexpectedExit()
    {
        WithEngine(delegate(string directory)
        {
            using (ReceiverService service = new ReceiverService(directory))
            {
                service.Start(new AppSettings { ReceiverName = "Exit immediately" });
                Wait(delegate { return service.State == ReceiverState.Error; }, "engine exit notification");
                Assert(!service.HasOwnedProcess, "unexpected exit clears old process identity");
                Assert(service.SessionPin == null, "PIN cleared on unexpected exit");
                service.Start(new AppSettings());
                Wait(delegate { return service.State == ReceiverState.Running || service.State == ReceiverState.Error; }, "restart after engine failure");
                Assert(service.State == ReceiverState.Running, "old process exit cannot stop replacement");
            }
        });
    }

    private static bool ProcessExists(int id)
    {
        try { using (Process process = Process.GetProcessById(id)) return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
    private static void Wait(Func<bool> condition, string reason)
    { Stopwatch timer = Stopwatch.StartNew(); while (!condition()) { if (timer.ElapsedMilliseconds > 8000) throw new Exception("Timed out: " + reason); Thread.Sleep(25); } }
    private static void WithEngine(Action<string> test)
    {
        WithDirectory(delegate(string directory)
        {
            string bin = Path.Combine(directory, "bin"); Directory.CreateDirectory(bin);
            File.Copy(Assembly.GetExecutingAssembly().Location, Path.Combine(bin, "uxplay.exe"));
            WriteManifest(bin, "uxplay.exe");
            test(directory);
        });
    }
    private static void WithDirectory(Action<string> test)
    {
        string directory = Path.Combine(Path.GetTempPath(), "LetMeSee-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { test(directory); }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    private static void WriteManifest(string directory, params string[] paths)
    {
        StringBuilder content = new StringBuilder();
        foreach (string path in paths) content.Append(Hash(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)))).Append("  ").Append(path).Append('\n');
        File.WriteAllText(Path.Combine(directory, "runtime-files.sha256"), content.ToString(), new UTF8Encoding(false));
    }
    private static string Hash(string path)
    { using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
    private static void ExpectArgumentException(Action test)
    { bool rejected = false; try { test(); } catch (ArgumentException) { rejected = true; } Assert(rejected, "invalid arguments rejected before process invocation"); }
    private static void Assert(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    private static void Run(string name, Action test)
    { try { test(); _passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { _failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); } }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr value);
}
