using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace LetMeSee
{
    // Owns only one mirror and its private adb server; it never controls the global adb service.
    public sealed class AndroidMirrorService : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _applicationDirectory;
        private Process _process;
        private AndroidBridge _bridge;
        private CancellationTokenSource _cancellation;
        private Timer _timer;
        private int _operation;
        private bool _disposed;
        private bool _stopping;
        private ReceiverState _state;
        private string _status = "Espelhamento Android parado.";

        public event EventHandler<ReceiverStateChangedEventArgs> StateChanged;
        public ReceiverState State { get { lock (_gate) return _state; } }
        public string StatusMessage { get { lock (_gate) return _status; } }
        public bool HasOwnedProcess { get { lock (_gate) return _process != null; } }

        public AndroidMirrorService(string applicationDirectory) { _applicationDirectory = Path.GetFullPath(applicationDirectory); }

        public void Start(AppSettings settings, PreparationSelection selection, PreparationConfirmations confirmations)
        {
            if (settings == null || selection == null || confirmations == null) throw new ArgumentNullException("settings, selection or confirmations");
            AppSettings options = settings.Clone();
            PreparationSelection chosen = selection.Clone();
            PreparationConfirmations confirmed = confirmations.Clone();
            string error;
            if (!options.Validate(out error) || !chosen.Validate(out error) || chosen.Device != DeviceFamily.Android)
            { PublishError(error ?? "Escolha um ambiente Android."); return; }
            int operation;
            CancellationTokenSource source;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("AndroidMirrorService");
                if (_stopping || _process != null || _state == ReceiverState.Starting) return;
                operation = ++_operation;
                source = _cancellation = new CancellationTokenSource();
                _state = ReceiverState.Starting;
                _status = "Revalidando checklist e autorizacao Android...";
            }
            Raise(ReceiverState.Starting, "Revalidando checklist e autorizacao Android...");
            ThreadPool.QueueUserWorkItem(delegate { StartWorker(options, chosen, confirmed, operation, source); });
        }

        private void StartWorker(AppSettings options, PreparationSelection selection, PreparationConfirmations confirmations, int operation, CancellationTokenSource source)
        {
            CancellationToken cancellation = source.Token;
            AndroidBridge bridge = null;
            Process process = null;
            bool transferred = false;
            try
            {
                using (PreparationProbe probe = new PreparationProbe(_applicationDirectory))
                {
                    PreparationSnapshot snapshot = probe.Inspect(selection, cancellation);
                    PreparationReport report = PreparationChecklist.Evaluate(selection, confirmations, snapshot);
                    if (!report.CanStart) { Fail(operation, FirstFailure(report)); return; }
                    bridge = probe.TakeAndroidBridge();
                    if (bridge == null || String.IsNullOrEmpty(report.SelectedAndroidSerial)) { Fail(operation, "Android nao esta pronto. Revise o checklist."); return; }
                    // Pin the actual serial, including the single-device case. Do not let scrcpy choose another device later.
                    selection.AndroidSerial = report.SelectedAndroidSerial;
                    snapshot.AndroidDevices = bridge.ReadDevices(cancellation);
                    report = PreparationChecklist.Evaluate(selection, confirmations, snapshot);
                    if (!report.CanStart) { Fail(operation, FirstFailure(report)); return; }
                    cancellation.ThrowIfCancellationRequested();
                    lock (_gate)
                    {
                        if (_disposed || _operation != operation || cancellation.IsCancellationRequested) return;
                        process = bridge.StartMirror(report.SelectedAndroidSerial, options.Mute);
                        Process owned = process;
                        process.Exited += delegate { OnExited(owned); };
                        _process = process;
                        _bridge = bridge;
                        transferred = true;
                        _state = ReceiverState.Running;
                        _status = "Motor Android ativo. Aguardando video na janela do scrcpy.";
                        process.EnableRaisingEvents = true;
                        if (options.SessionLimitMinutes > 0 && _process == process)
                            _timer = new Timer(delegate { StopInternal("Limite de tempo atingido. Espelhamento parado.", operation); }, null, TimeSpan.FromMinutes(options.SessionLimitMinutes), Timeout.InfiniteTimeSpan);
                    }
                }
                lock (_gate) { if (_process != process || _state != ReceiverState.Running) return; }
                Raise(ReceiverState.Running, "Motor Android ativo. Aguardando video na janela do scrcpy.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is Win32Exception || ex is InvalidOperationException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException)) throw;
                Fail(operation, "Nao foi possivel iniciar Android com seguranca. Confira o checklist, as autorizacoes e o pacote local.");
            }
            finally
            {
                if (!transferred)
                {
                    if (bridge != null) bridge.Dispose();
                    if (process != null) { AndroidBridge.KillOwned(process); process.Dispose(); }
                }
                lock (_gate)
                {
                    if (_cancellation == source) _cancellation = null;
                    source.Dispose();
                }
            }
        }

        public void Stop() { StopInternal("Espelhamento Android parado.", null); }
        private void StopInternal(string message, int? expectedOperation)
        {
            Process process;
            AndroidBridge bridge;
            lock (_gate)
            {
                if (expectedOperation.HasValue && (_disposed || expectedOperation.Value != _operation || _process == null)) return;
                if (_stopping) return;
                _stopping = true;
                ++_operation;
                if (_cancellation != null) { _cancellation.Cancel(); _cancellation = null; }
                if (_timer != null) { _timer.Dispose(); _timer = null; }
                process = _process;
                bridge = _bridge;
                _bridge = null;
            }
            if (bridge != null) bridge.Dispose();
            bool exited = true;
            if (process != null)
            {
                AndroidBridge.KillOwned(process);
                try { exited = process.HasExited; }
                catch (InvalidOperationException) { exited = false; }
            }
            ReceiverState state;
            lock (_gate)
            {
                if (exited) { if (process != null) process.Dispose(); _process = null; }
                state = _state = exited ? ReceiverState.Stopped : ReceiverState.Error;
                _status = exited ? message : "Nao foi possivel confirmar o fim do processo Android. Tente parar novamente.";
                message = _status;
                _stopping = false;
            }
            Raise(state, message);
        }

        private void OnExited(Process process)
        {
            AndroidBridge bridge;
            lock (_gate)
            {
                if (_disposed || _stopping || _process != process) return;
                _process = null;
                bridge = _bridge;
                _bridge = null;
                if (_timer != null) { _timer.Dispose(); _timer = null; }
                _state = ReceiverState.Error;
                _status = "A janela Android foi fechada ou a conexao terminou. Revise o checklist para iniciar novamente.";
            }
            if (bridge != null) bridge.Dispose();
            process.Dispose();
            Raise(ReceiverState.Error, StatusMessage);
        }
        private void PublishError(string message)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("AndroidMirrorService");
                if (_process != null || _state == ReceiverState.Starting || _stopping) return;
                _state = ReceiverState.Error; _status = message;
            }
            Raise(ReceiverState.Error, message);
        }
        private void Fail(int operation, string message)
        {
            lock (_gate)
            {
                if (_disposed || _operation != operation) return;
                _state = ReceiverState.Error; _status = message;
            }
            Raise(ReceiverState.Error, message);
        }
        private static string FirstFailure(PreparationReport report)
        {
            foreach (PreparationCheck check in report.Checks)
                if (check.Status == CheckStatus.Blocked || check.Status == CheckStatus.ActionRequired) return check.Detail;
            return "Revise o checklist antes de iniciar.";
        }
        private void Raise(ReceiverState state, string message)
        {
            EventHandler<ReceiverStateChangedEventArgs> handler = StateChanged;
            if (handler != null) handler(this, new ReceiverStateChangedEventArgs(state, message));
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; }
            Stop();
        }
    }
}
