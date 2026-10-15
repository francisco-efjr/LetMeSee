using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace LetMeSee
{
    public enum ReceiverState { Stopped, Starting, Running, Error }

    public sealed class ReceiverStateChangedEventArgs : EventArgs
    {
        public ReceiverState State { get; private set; }
        public string Message { get; private set; }
        public ReceiverStateChangedEventArgs(ReceiverState state, string message) { State = state; Message = message; }
    }

    // Running means only that the owned engine process is alive; no client-connection claim is made.
    // Events may be raised on a worker thread. UI subscribers must marshal to their UI thread.
    public sealed class ReceiverService : IDisposable
    {
        private readonly object _gate = new object();
        private readonly object _outputGate = new object();
        private readonly string _applicationDirectory;
        private readonly StringBuilder _recentOutput = new StringBuilder();
        private Process _process;
        private EngineJob _job;
        private FileStream _configuration;
        private string _configurationPath;
        private string _registryPath;
        private Timer _timer;
        private bool _disposed;
        private bool _stopping;
        private int _operation;
        private ReceiverState _state;
        private string _status = "Receptor parado.";
        private string _sessionPin;

        public event EventHandler<ReceiverStateChangedEventArgs> StateChanged;
        public ReceiverState State { get { lock (_gate) return _state; } }
        public string StatusMessage { get { lock (_gate) return _status; } }
        public bool HasOwnedProcess { get { lock (_gate) return _process != null; } }
        public string SessionPin { get { lock (_gate) return _sessionPin; } }

        public ReceiverService(string applicationDirectory) { _applicationDirectory = Path.GetFullPath(applicationDirectory); }

        public void Start(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            AppSettings snapshot = settings.Clone();
            string error;
            if (!snapshot.Validate(out error)) { SetError(error); return; }
            int operation;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("ReceiverService");
                if (_stopping || _state == ReceiverState.Starting || _process != null) return;
                operation = ++_operation;
                _state = ReceiverState.Starting;
                _status = "Verificando motor local...";
                _sessionPin = null;
            }
            Raise(ReceiverState.Starting, "Verificando motor local...");
            ThreadPool.QueueUserWorkItem(delegate { StartWorker(snapshot, operation); });
        }

        private void StartWorker(AppSettings settings, int operation)
        {
            Process process = null;
            EngineJob job = null;
            FileStream configuration = null;
            string configurationPath = null;
            string registryPath = null;
            bool transferred = false;
            try
            {
                string bin = Path.Combine(_applicationDirectory, "bin");
                RuntimeVerificationResult verification = RuntimeVerifier.Verify(bin);
                if (!verification.IsValid) { FailOperation(operation, verification.Message); return; }
                if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                { FailOperation(operation, "O receptor requer Windows 10 ou 11."); return; }
                lock (_gate) { if (_disposed || _operation != operation) return; }

                string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSee");
                Directory.CreateDirectory(local);
                string session = Guid.NewGuid().ToString("N");
                configurationPath = Path.Combine(local, "receiver-" + session + ".conf");
                registryPath = Path.Combine(local, "plugins-" + session + ".cache");
                // Held read-only to prevent global UxPlay options from enabling recording or debug files.
                configuration = new FileStream(configurationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
                configuration.Flush();
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Path.Combine(bin, "uxplay.exe");
                string sessionPin = settings.RequirePin ? GenerateSessionPin() : null;
                info.Arguments = UxPlayArguments.Build(settings) + " -rc " + UxPlayArguments.Quote(configurationPath) +
                    (sessionPin == null ? "" : " -pin " + sessionPin);
                info.WorkingDirectory = bin;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                ConfigureEnvironment(info, bin, registryPath);
                job = new EngineJob();
                process = new Process();
                process.StartInfo = info;
                Process owned = process;
                process.Exited += delegate { OnExited(owned); };
                lock (_gate)
                {
                    if (_disposed || _operation != operation) return;
                    if (!process.Start()) throw new InvalidOperationException();
                    job.Assign(process);
                    _process = process;
                    _job = job;
                    _configuration = configuration;
                    _configurationPath = configurationPath;
                    _registryPath = registryPath;
                    _sessionPin = sessionPin;
                    transferred = true;
                    _state = ReceiverState.Running;
                    _status = "Motor ativo. Aguardando espelhamento no dispositivo.";
                    lock (_outputGate) _recentOutput.Length = 0;
                    StartDrain(process.StandardOutput, operation);
                    StartDrain(process.StandardError, operation);
                    process.EnableRaisingEvents = true;
                    if (settings.SessionLimitMinutes > 0 && _process == process)
                        _timer = new Timer(delegate { StopAtLimit(operation); }, null, TimeSpan.FromMinutes(settings.SessionLimitMinutes), Timeout.InfiniteTimeSpan);
                }
                // Identity check prevents an exit during startup from publishing a stale Running state.
                lock (_gate) { if (_process != process || _state != ReceiverState.Running) return; }
                Raise(ReceiverState.Running, "Motor ativo. Aguardando espelhamento no dispositivo.");
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException || ex is NotSupportedException || ex is CryptographicException || ex is System.Security.SecurityException)) throw;
                FailOperation(operation, "Nao foi possivel iniciar o motor com seguranca. Consulte o diagnostico.");
            }
            finally
            {
                if (!transferred)
                {
                    if (job != null) job.Dispose();
                    if (process != null)
                    {
                        try { if (!process.HasExited) { process.Kill(); process.WaitForExit(1500); } } catch (InvalidOperationException) { } catch (Win32Exception) { }
                        process.Dispose();
                    }
                    if (configuration != null) configuration.Dispose();
                    DeleteLocal(configurationPath);
                    DeleteLocal(registryPath);
                }
            }
        }

        private static void ConfigureEnvironment(ProcessStartInfo info, string bin, string registryPath)
        {
            List<string> remove = new List<string>();
            foreach (DictionaryEntry entry in info.EnvironmentVariables)
            {
                string key = (string)entry.Key;
                if (key.StartsWith("GST_", StringComparison.OrdinalIgnoreCase) || key.Equals("UXPLAYRC", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("G_DEBUG", StringComparison.OrdinalIgnoreCase) || key.Equals("G_MESSAGES_DEBUG", StringComparison.OrdinalIgnoreCase)) remove.Add(key);
            }
            foreach (string key in remove) info.EnvironmentVariables.Remove(key);
            string plugins = Path.Combine(bin, "lib", "gstreamer-1.0");
            info.EnvironmentVariables["PATH"] = bin + ";" + Path.Combine(bin, "bin") + ";" + Environment.SystemDirectory;
            info.EnvironmentVariables["GST_PLUGIN_PATH"] = plugins;
            info.EnvironmentVariables["GST_PLUGIN_PATH_1_0"] = plugins;
            info.EnvironmentVariables["GST_PLUGIN_SYSTEM_PATH"] = "";
            info.EnvironmentVariables["GST_PLUGIN_SYSTEM_PATH_1_0"] = "";
            info.EnvironmentVariables["GST_REGISTRY"] = registryPath;
            info.EnvironmentVariables["GST_REGISTRY_1_0"] = registryPath;
            // Scan verified plugins in-process: no detached plugin scanner may outlive the receiver.
            info.EnvironmentVariables["GST_REGISTRY_FORK"] = "no";
        }

        private void StartDrain(StreamReader reader, int operation)
        {
            Thread thread = new Thread(delegate()
            {
                char[] buffer = new char[256];
                try
                {
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        lock (_gate) { if (_operation != operation || _disposed) return; }
                        lock (_outputGate)
                        {
                            _recentOutput.Append(buffer, 0, count);
                            if (_recentOutput.Length > 8192) _recentOutput.Remove(0, _recentOutput.Length - 8192);
                        }
                    }
                }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
                catch (InvalidOperationException) { }
            });
            thread.IsBackground = true;
            thread.Name = "LetMeSee engine output";
            thread.Start();
        }

        public void Stop() { StopInternal("Receptor parado.", null); }
        private void StopAtLimit(int operation)
        {
            StopInternal("Limite de tempo atingido. Receptor parado.", operation);
        }

        private void StopInternal(string message, int? expectedOperation)
        {
            Process process;
            EngineJob job;
            lock (_gate)
            {
                if (expectedOperation.HasValue && (_disposed || _operation != expectedOperation.Value || _process == null)) return;
                if (_stopping) return;
                _stopping = true;
                ++_operation; // Cancels an in-flight hash verification and ignores stale exit callbacks.
                _sessionPin = null;
                process = _process;
                job = _job;
                if (_timer != null) { _timer.Dispose(); _timer = null; }
            }
            bool exited = true;
            if (job != null) job.Dispose(); // KILL_ON_JOB_CLOSE also terminates owned descendants.
            if (process != null)
            {
                try
                {
                    if (!process.HasExited) process.Kill();
                    exited = process.WaitForExit(2000);
                }
                catch (Win32Exception) { try { exited = process.HasExited; } catch (InvalidOperationException) { exited = false; } }
                catch (InvalidOperationException) { exited = false; }
            }
            ReceiverState state;
            lock (_gate)
            {
                _job = null;
                if (exited)
                {
                    if (process != null) process.Dispose();
                    _process = null;
                    CleanupLocal();
                    _state = ReceiverState.Stopped;
                    _status = message;
                }
                else
                {
                    // Keep the process identity so a retry or Dispose can attempt termination again.
                    _state = ReceiverState.Error;
                    _status = "Nao foi possivel confirmar o encerramento do motor. Feche o processo uxplay pelo Gerenciador de Tarefas.";
                }
                _stopping = false;
                state = _state;
                message = _status;
            }
            lock (_outputGate) _recentOutput.Length = 0;
            Raise(state, message);
        }

        private void OnExited(Process process)
        {
            lock (_gate)
            {
                if (_process != process || _stopping || _disposed) return;
                ++_operation;
                _sessionPin = null;
                _process = null;
                if (_timer != null) { _timer.Dispose(); _timer = null; }
                if (_job != null) { _job.Dispose(); _job = null; }
                process.Dispose();
                CleanupLocal();
                _state = ReceiverState.Error;
                _status = "O motor encerrou. Verifique o diagnostico antes de iniciar novamente.";
            }
            lock (_outputGate) _recentOutput.Length = 0;
            Raise(ReceiverState.Error, "O motor encerrou. Verifique o diagnostico antes de iniciar novamente.");
        }

        private void CleanupLocal()
        {
            if (_configuration != null) { _configuration.Dispose(); _configuration = null; }
            DeleteLocal(_configurationPath);
            DeleteLocal(_registryPath);
            _configurationPath = null;
            _registryPath = null;
        }
        internal static string GenerateSessionPin()
        {
            // UxPlay 1.73.6 accepts 0001..9999; rejection sampling avoids modulo bias.
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[2];
                int value;
                do { random.GetBytes(bytes); value = bytes[0] | (bytes[1] << 8); } while (value >= 59994);
                return ((value % 9999) + 1).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        private static void DeleteLocal(string path)
        { if (path == null) return; try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        private void SetError(string message)
        { lock (_gate) { if (_disposed || _process != null || _state == ReceiverState.Starting) return; _state = ReceiverState.Error; _status = message; } Raise(ReceiverState.Error, message); }
        private void FailOperation(int operation, string message)
        { lock (_gate) { if (_disposed || _operation != operation) return; _state = ReceiverState.Error; _status = message; } Raise(ReceiverState.Error, message); }
        private void Raise(ReceiverState state, string message)
        {
            EventHandler<ReceiverStateChangedEventArgs> handler = StateChanged;
            if (handler != null) handler(this, new ReceiverStateChangedEventArgs(state, message));
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; }
            StopInternal("Receptor parado.", null);
            StateChanged = null;
        }
    }

    // Windows owns this handle and terminates the engine tree even if the GUI crashes.
    internal sealed class EngineJob : IDisposable
    {
        private IntPtr _handle;
        public EngineJob()
        {
            _handle = CreateJobObject(IntPtr.Zero, null);
            if (_handle == IntPtr.Zero) throw new Win32Exception();
            ExtendedLimits limits = new ExtendedLimits();
            limits.Basic.LimitFlags = 0x00002000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            if (!SetInformationJobObject(_handle, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits))))
            { Dispose(); throw new Win32Exception(); }
        }
        public void Assign(Process process)
        { if (!AssignProcessToJobObject(_handle, process.Handle)) throw new Win32Exception(); }
        public void Dispose()
        { IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero); if (handle != IntPtr.Zero) CloseHandle(handle); GC.SuppressFinalize(this); }
        ~EngineJob() { Dispose(); }

        [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
        {
            public long ProcessUserTime;
            public long JobUserTime;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSet;
            public UIntPtr MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] private struct IoCounters
        { public ulong ReadOperations; public ulong WriteOperations; public ulong OtherOperations; public ulong ReadBytes; public ulong WriteBytes; public ulong OtherBytes; }
        [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
        { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemoryLimit; public UIntPtr JobMemoryLimit; public UIntPtr PeakProcessMemoryUsed; public UIntPtr PeakJobMemoryUsed; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(IntPtr job, int informationClass, ref ExtendedLimits information, uint length);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    }
}
