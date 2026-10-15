using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LetMeSee
{
    // Private loopback server: probing must never restart or kill Android Studio's global adb server.
    internal sealed class AndroidBridge : IDisposable
    {
        private readonly string _directory;
        private readonly int _port;
        private EngineJob _job;
        private Process _server;
        private bool _disposed;

        public AndroidBridge(string directory, CancellationToken cancellation)
        {
            _directory = Path.GetFullPath(directory);
            _port = ReservePort();
            try
            {
                _job = new EngineJob();
                _server = new Process();
                _server.StartInfo = CreateInfo("adb.exe", "-L tcp:127.0.0.1:" + _port + " server nodaemon");
                if (!_server.Start()) throw new IOException("Nao foi possivel iniciar o servidor Android privado.");
                _job.Assign(_server);
                BoundedProcessOutput.Drain(_server.StandardOutput);
                BoundedProcessOutput.Drain(_server.StandardError);
                // The explicit bind must succeed before any client can use this endpoint.
                for (int index = 0; index < 20; index++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (_server.HasExited) throw new IOException("Servidor Android privado indisponivel.");
                    if (CanConnect(_port)) { Thread.Sleep(80); if (_server.HasExited) throw new IOException("Porta Android privada indisponivel."); return; }
                    Thread.Sleep(50);
                }
                throw new IOException("O servidor Android privado nao respondeu a tempo.");
            }
            catch { Dispose(); throw; }
        }

        public IList<AndroidDevice> ReadDevices(CancellationToken cancellation)
        {
            EnsureAlive();
            string arguments = "-H 127.0.0.1 -P " + _port + " devices -l";
            using (Process process = new Process())
            {
                process.StartInfo = CreateInfo("adb.exe", arguments);
                if (!process.Start()) throw new IOException("Nao foi possivel consultar o Android.");
                try
                {
                    _job.Assign(process);
                    BoundedProcessOutput output = new BoundedProcessOutput(process.StandardOutput, 65536);
                    BoundedProcessOutput errors = new BoundedProcessOutput(process.StandardError, 8192);
                    Stopwatch timeout = Stopwatch.StartNew();
                    while (!process.WaitForExit(50))
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (timeout.ElapsedMilliseconds > 5000) throw new IOException("A consulta Android excedeu o tempo limite.");
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if (process.ExitCode != 0) throw new IOException("Nao foi possivel consultar o Android com seguranca.");
                    string text = output.Complete();
                    errors.Complete();
                    EnsureAlive();
                    return AndroidDevicesParser.Parse(text);
                }
                finally { KillOwned(process); }
            }
        }

        public Process StartMirror(string serial, bool mute)
        {
            EnsureAlive();
            Process process = new Process();
            process.StartInfo = CreateInfo("scrcpy.exe", AndroidArguments.Build(serial, mute));
            try
            {
                if (!process.Start()) throw new IOException("Nao foi possivel iniciar scrcpy.");
                _job.Assign(process);
                // Never persist engine output: it may contain identifiers or device metadata.
                BoundedProcessOutput.Drain(process.StandardOutput);
                BoundedProcessOutput.Drain(process.StandardError);
                return process;
            }
            catch { KillOwned(process); process.Dispose(); throw; }
        }

        internal ProcessStartInfo CreateInfo(string executable, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = Path.Combine(_directory, executable);
            info.Arguments = arguments;
            info.WorkingDirectory = _directory;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            List<string> remove = new List<string>();
            foreach (DictionaryEntry entry in info.EnvironmentVariables)
            {
                string key = (string)entry.Key;
                if (key.StartsWith("ADB_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("SCRCPY_", StringComparison.OrdinalIgnoreCase) || key.StartsWith("SDL_", StringComparison.OrdinalIgnoreCase) || key.Equals("ANDROID_SERIAL", StringComparison.OrdinalIgnoreCase) || key.Equals("ANDROID_USER_HOME", StringComparison.OrdinalIgnoreCase) || key.Equals("ANDROID_SDK_HOME", StringComparison.OrdinalIgnoreCase)) remove.Add(key);
            }
            foreach (string key in remove) info.EnvironmentVariables.Remove(key);
            info.EnvironmentVariables["PATH"] = _directory + ";" + Environment.SystemDirectory;
            info.EnvironmentVariables["ADB"] = Path.Combine(_directory, "adb.exe");
            info.EnvironmentVariables["ADB_SERVER_SOCKET"] = "tcp:127.0.0.1:" + _port;
            info.EnvironmentVariables["SCRCPY_SERVER_PATH"] = Path.Combine(_directory, "scrcpy-server");
            return info;
        }

        private void EnsureAlive()
        {
            if (_disposed) throw new ObjectDisposedException("AndroidBridge");
            if (_server == null || _server.HasExited) throw new IOException("Servidor Android privado encerrado. Verifique novamente.");
        }
        private static int ReservePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
            finally { listener.Stop(); }
        }
        private static bool CanConnect(int port)
        {
            try { using (TcpClient client = new TcpClient()) { client.Connect(IPAddress.Loopback, port); return true; } }
            catch (SocketException) { return false; }
        }
        internal static void KillOwned(Process process)
        {
            if (process == null) return;
            try { if (!process.HasExited) { process.Kill(); process.WaitForExit(1500); } }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_job != null) { _job.Dispose(); _job = null; }
            if (_server != null) { KillOwned(_server); _server.Dispose(); _server = null; }
        }
    }

    internal sealed class BoundedProcessOutput
    {
        private readonly Thread _thread;
        private readonly StringBuilder _text = new StringBuilder();
        private readonly int _limit;
        private volatile bool _overflow;
        private volatile bool _failed;
        public BoundedProcessOutput(StreamReader reader, int limit)
        {
            _limit = limit;
            _thread = new Thread(delegate()
            {
                char[] buffer = new char[512];
                try
                {
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (_limit == 0) continue;
                        lock (_text)
                        {
                            if (_text.Length + count > _limit) { _overflow = true; continue; }
                            _text.Append(buffer, 0, count);
                        }
                    }
                }
                catch (IOException) { _failed = true; }
                catch (ObjectDisposedException) { _failed = true; }
                catch (InvalidOperationException) { _failed = true; }
            });
            _thread.IsBackground = true;
            _thread.Name = "LetMeSee private engine output";
            _thread.Start();
        }
        public string Complete()
        {
            if (!_thread.Join(1000) || _overflow || _failed) throw new IOException("Resposta do processo local incompleta ou excessiva.");
            lock (_text) return _text.ToString();
        }
        public static void Drain(StreamReader reader) { new BoundedProcessOutput(reader, 0); }
    }
}
