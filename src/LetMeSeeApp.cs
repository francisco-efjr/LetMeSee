using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LetMeSee
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (Mutex instance = new Mutex(false, "Local\\LetMeSee.Desktop"))
            {
                bool acquired;
                try { acquired = instance.WaitOne(0, false); } catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { MessageBox.Show("O LetMeSee já está aberto. Procure a janela ou o ícone na bandeja.", "LetMeSee"); return; }
                try { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
                finally { instance.ReleaseMutex(); }
            }
        }
    }

    // The mirror surface has exactly two controls. All preparation lives in its own dialog.
    public sealed class MainForm : Form
    {
        private readonly string applicationDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly SettingsStore settingsStore = new SettingsStore();
        private readonly ReceiverService airplay;
        private readonly AndroidMirrorService android;
        private AppSettings settings;
        private PreparationSelection selection;
        private PreparationConfirmations confirmations = new PreparationConfirmations();
        private DateTime preparedAt = DateTime.MinValue;
        private readonly ActionButton environmentButton = new ActionButton("Selecionar ambiente", false);
        private readonly ActionButton startButton = new ActionButton("Começar a espelhar", true);
        private readonly Label state = new Label();
        private readonly Label guidance = new Label();
        private NotifyIcon tray;
        private ContextMenuStrip trayMenu;
        private int intention;
        private string preparationNotice;
        private bool checking;
        private bool disposed;
        private bool forceClose;
        private bool preparationInvalidated;
        private readonly SonarPanel sonar = new SonarPanel();

        public MainForm()
        {
            settings = settingsStore.Load(); selection = EnvironmentStore.Load();
            airplay = new ReceiverService(applicationDirectory); android = new AndroidMirrorService(applicationDirectory);
            airplay.StateChanged += StateChanged; android.StateChanged += StateChanged;
            Text = "LetMeSee"; Font = DesignTokens.Body; BackColor = DesignTokens.Window; ForeColor = DesignTokens.Text;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(820, 660); MinimumSize = new Size(700, 620); StartPosition = FormStartPosition.CenterScreen;
            string iconPath = Path.Combine(applicationDirectory, "app_icon.ico"); Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;
            BuildSurface(); BuildTray();
            Shown += delegate { sonar.Play(settings.EnableAnimation); OpenPreparation(); };
            FormClosing += ClosingRequested;
            SystemEvents.PowerModeChanged += PowerChanged;
            NetworkChange.NetworkAddressChanged += NetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChanged;
        }
        private static Label Label(string text, Font font, Color color, int height)
        { return new Label { Text = text, Font = font, ForeColor = color, AutoSize = false, Dock = DockStyle.Top, Height = height, TextAlign = ContentAlignment.MiddleCenter }; }
        private void BuildSurface()
        {
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(48, 24, 48, 24) };
            float[] sizes = { 44, 180, 62, 42, 64, 16, 64, 94 };
            foreach (float size in sizes) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, size));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.Controls.Add(Label("LETMESEE  /  SUA TELA, POR PERTO.", DesignTokens.CaptionStrong, DesignTokens.Muted, 44), 0, 0);
            sonar.Size = new Size(180, 180); sonar.Anchor = AnchorStyles.None; layout.Controls.Add(sonar, 0, 1);
            layout.Controls.Add(Label("Sua tela. Só isso.", DesignTokens.HeroTitle, DesignTokens.Text, 62), 0, 2);
            state.Text = "Prepare o ambiente para começar"; state.ForeColor = DesignTokens.Muted; state.Dock = DockStyle.Fill; state.TextAlign = ContentAlignment.MiddleCenter; state.AccessibleRole = AccessibleRole.StaticText;
            layout.Controls.Add(state, 0, 3);
            environmentButton.Dock = DockStyle.Fill; environmentButton.Margin = new Padding(80, 4, 80, 4); environmentButton.Click += delegate { OpenPreparation(); };
            layout.Controls.Add(environmentButton, 0, 4);
            startButton.Dock = DockStyle.Fill; startButton.Margin = new Padding(80, 4, 80, 4); startButton.Click += Toggle;
            layout.Controls.Add(startButton, 0, 6);
            guidance.ForeColor = DesignTokens.Muted; guidance.Dock = DockStyle.Fill; guidance.TextAlign = ContentAlignment.MiddleCenter; guidance.Padding = new Padding(0, 12, 0, 0);
            layout.Controls.Add(guidance, 0, 7); Controls.Add(layout); UpdateSurface();
        }
        private bool Owned { get { return airplay.HasOwnedProcess || android.HasOwnedProcess; } }
        private ReceiverState ActiveState { get { return selection.Device == DeviceFamily.IOS ? airplay.State : android.State; } }
        private void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir LetMeSee", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            trayMenu.Items.Add("Parar espelhamento", null, delegate { Stop(); });
            trayMenu.Items.Add("Sair", null, delegate { forceClose = true; Close(); });
            tray = new NotifyIcon { Icon = Icon, Text = "LetMeSee · parado", ContextMenuStrip = trayMenu, Visible = true };
            tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
        }
        private void OpenPreparation()
        {
            if (checking || Owned || ActiveState == ReceiverState.Starting) return;
            InvalidatePreparation();
            using (PreparationForm dialog = new PreparationForm(applicationDirectory, settings, selection))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    settings = dialog.Settings; selection = dialog.Selection; confirmations = dialog.Confirmations;
                    preparedAt = DateTime.UtcNow; preparationInvalidated = false;
                    bool saved = settingsStore.Save(settings) && EnvironmentStore.Save(selection);
                    preparationNotice = saved ? "Checklist concluído. Os requisitos serão conferidos outra vez ao começar." : "Ambiente pronto nesta sessão. Não foi possível salvar suas preferências.";
                }
            }
            UpdateSurface();
        }
        private void Toggle(object sender, EventArgs e)
        {
            if (Owned || ActiveState == ReceiverState.Starting) { Stop(); return; }
            if (checking) return;
            if (preparedAt == DateTime.MinValue || preparationInvalidated || (DateTime.UtcNow < preparedAt || DateTime.UtcNow - preparedAt > TimeSpan.FromMinutes(5)))
            { OpenPreparation(); return; }
            checking = true; int capturedIntention = ++intention; UpdateSurface();
            PreparationSelection captured = selection.Clone(); PreparationConfirmations consent = confirmations.Clone();
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object s, DoWorkEventArgs args) { using (PreparationProbe inspector = new PreparationProbe(applicationDirectory)) args.Result = PreparationChecklist.Evaluate(captured, consent, inspector.Inspect(captured)); };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                worker.Dispose(); checking = false; if (disposed) return;
                if (capturedIntention != intention || preparationInvalidated) { preparedAt = DateTime.MinValue; UpdateSurface(); guidance.Text = "A preparação mudou. Selecione o ambiente para verificar novamente."; return; }
                if (args.Error != null || !((PreparationReport)args.Result).CanStart)
                { preparedAt = DateTime.MinValue; UpdateSurface(); OpenPreparation(); return; }
                PreparationReport report = (PreparationReport)args.Result; captured.AndroidSerial = report.SelectedAndroidSerial; selection = captured;
                if (selection.Device == DeviceFamily.IOS) airplay.Start(settings.Clone()); else android.Start(settings.Clone(), selection.Clone(), consent);
                UpdateSurface();
            };
            worker.RunWorkerAsync();
        }
        private void InvalidatePreparation()
        { intention++; preparationInvalidated = true; preparedAt = DateTime.MinValue; confirmations = new PreparationConfirmations(); preparationNotice = null; }
        private void Stop() { InvalidatePreparation(); airplay.Stop(); android.Stop(); UpdateSurface(); }
        private void StateChanged(object sender, ReceiverStateChangedEventArgs e)
        {
            if (disposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { if (e.State == ReceiverState.Error || e.State == ReceiverState.Stopped) InvalidatePreparation(); UpdateSurface(); })); } catch (InvalidOperationException) { }
        }
        private void UpdateSurface()
        {
            ReceiverState active = ActiveState; if (active == ReceiverState.Error && preparedAt != DateTime.MinValue) active = ReceiverState.Stopped; bool busy = Owned || active == ReceiverState.Starting;
            environmentButton.Text = "Ambiente  ·  " + EnvironmentStore.Describe(selection); environmentButton.AccessibleName = environmentButton.Text;
            environmentButton.Enabled = !checking && !busy;
            startButton.Text = busy ? "Parar espelhamento" : checking ? "Verificando ambiente…" : "Começar a espelhar";
            startButton.AccessibleName = startButton.Text; startButton.Enabled = !checking && (busy || (preparedAt != DateTime.MinValue && !preparationInvalidated));
            state.Text = checking ? "Conferindo os requisitos do ambiente…" : active == ReceiverState.Running ? (selection.Device == DeviceFamily.IOS ? "Receptor AirPlay disponível" : "Janela Android iniciada") : active == ReceiverState.Starting ? "Preparando a conexão…" : active == ReceiverState.Error ? "O ambiente precisa de atenção" : preparedAt == DateTime.MinValue ? "Selecione o ambiente e conclua o checklist" : "Tudo pronto para começar";
            state.ForeColor = active == ReceiverState.Error ? DesignTokens.Danger : active == ReceiverState.Running ? DesignTokens.Success : DesignTokens.Muted;
            if (active == ReceiverState.Running)
                guidance.Text = selection.Device == DeviceFamily.IOS ? "No iPhone: Central de Controle → Espelhar a Tela → " + settings.ReceiverName + (settings.RequirePin ? "\nCódigo de pareamento: " + airplay.SessionPin : "") : "O vídeo abre em outra janela. O Android permanece somente em visualização.";
            else if (active == ReceiverState.Error) guidance.Text = selection.Device == DeviceFamily.IOS ? airplay.StatusMessage : android.StatusMessage;
            else if (preparedAt == DateTime.MinValue) guidance.Text = "A preparação fica fora desta tela. Nada começa sem sua ação.";
            else if (!checking && active == ReceiverState.Stopped) guidance.Text = preparationNotice ?? "Checklist concluído. A conexão é conferida novamente ao começar.";
            if (tray != null) tray.Text = busy ? "LetMeSee · sessão ativa" : "LetMeSee · parado";
        }
        private void PowerChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (disposed || !IsHandleCreated || (e.Mode != PowerModes.Suspend && e.Mode != PowerModes.Resume)) return;
            try { BeginInvoke(new Action(delegate { InvalidatePreparation(); if (e.Mode == PowerModes.Suspend) Stop(); UpdateSurface(); })); } catch (InvalidOperationException) { }
        }
        private void NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) { NetworkChanged(sender, EventArgs.Empty); }
        private void NetworkChanged(object sender, EventArgs e)
        {
            if (disposed || !IsHandleCreated || selection.Method == MirrorMethod.AndroidUsb) return;
            try { BeginInvoke(new Action(delegate { Stop(); guidance.Text = "A rede mudou. Selecione o ambiente para conferir a conexão novamente."; })); } catch (InvalidOperationException) { }
        }
        private void ClosingRequested(object sender, FormClosingEventArgs e)
        {
            if (!forceClose && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            { e.Cancel = true; Hide(); tray.ShowBalloonTip(2000, "LetMeSee", "Use Sair no ícone da bandeja para encerrar.", ToolTipIcon.Info); return; }
            Stop();
            if (Owned) { e.Cancel = true; Show(); guidance.Text = "Não foi possível confirmar o encerramento. Tente parar novamente."; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; SystemEvents.PowerModeChanged -= PowerChanged;
                NetworkChange.NetworkAddressChanged -= NetworkChanged; NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChanged;
                airplay.StateChanged -= StateChanged; android.StateChanged -= StateChanged;
                airplay.Dispose(); android.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); } if (trayMenu != null) trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
