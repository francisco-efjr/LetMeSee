using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using System.Text;
using System.Windows.Forms;

namespace LetMeSee
{
    internal sealed class PreparationForm : Form
    {
        private readonly string applicationDirectory;
        private PreparationSnapshot snapshot;
        private PreparationReport report;
        private bool changing;
        private bool inspecting;
        private int revision;
        private bool closed;
        private DateTime inspectedAt = DateTime.MinValue;
        private readonly ComboBox family = Choice();
        private readonly ComboBox method = Choice();
        private readonly ComboBox experience = Choice();
        private readonly ComboBox devices = Choice();
        private readonly CheckBox network = Confirm("Celular e computador estão na mesma rede privada de confiança.");
        private readonly CheckBox deviceReady = Confirm("Preparei o aparelho seguindo as orientações abaixo.");
        private readonly CheckBox privacy = Confirm("Ocultei notificações e dados pessoais antes de compartilhar.");
        private readonly CheckBox firewallReviewed = Confirm("Conferi manualmente as regras privadas do firewall.");
        private readonly CheckBox bonjourReviewed = Confirm("Conferi manualmente o serviço Bonjour para descoberta.");
        private readonly TextBox checks = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, BackColor = DesignTokens.Window, ForeColor = DesignTokens.Text, Font = DesignTokens.Body, AccessibleName = "Resultado do checklist", TabStop = false };
        private readonly ActionButton continueButton = new ActionButton("Concluir preparação", true);
        private readonly ActionButton refreshButton = new ActionButton("Verificar novamente", false);
        private readonly ActionButton runtimeButton = new ActionButton("Preparar motor…", false);
        private readonly ActionButton bonjourButton = new ActionButton("Configurar descoberta…", false);
        private readonly ActionButton firewallButton = new ActionButton("Configurar firewall…", false);
        private readonly Label summary = new Label { AutoSize = false, Dock = DockStyle.Fill, ForeColor = DesignTokens.Muted, TextAlign = ContentAlignment.MiddleLeft };
        private readonly TextBox receiverName = new TextBox();
        private readonly CheckBox mute = Confirm("Silenciar o áudio recebido");
        private readonly CheckBox tray = Confirm("Continuar na bandeja ao fechar");
        private readonly CheckBox animation = Confirm("Animar o radar ao abrir");
        private readonly CheckBox pin = Confirm("Pedir código de pareamento no AirPlay");
        private readonly ComboBox limit = Choice();
        public AppSettings Settings { get; private set; }
        public PreparationSelection Selection { get; private set; }
        public PreparationConfirmations Confirmations { get; private set; }

        public PreparationForm(string applicationDirectory, AppSettings settings, PreparationSelection selection)
        {
            this.applicationDirectory = applicationDirectory; Settings = settings.Clone(); Selection = selection.Clone(); Confirmations = new PreparationConfirmations();
            EnvironmentStore.ApplyRuntimeDirectory(Selection);
            Text = "Preparar ambiente · LetMeSee"; ClientSize = new Size(900, 820); MinimumSize = new Size(820, 760);
            StartPosition = FormStartPosition.CenterParent; BackColor = DesignTokens.Window; ForeColor = DesignTokens.Text; Font = DesignTokens.Body;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            Build(); Shown += delegate { Inspect(); };
            NetworkChange.NetworkAddressChanged += NetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            FormClosed += delegate { closed = true; NetworkChange.NetworkAddressChanged -= NetworkChanged; NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChanged; SystemEvents.PowerModeChanged -= PowerChanged; };
        }
        private static ComboBox Choice() { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, BackColor = DesignTokens.Raised, ForeColor = DesignTokens.Text }; }
        private static CheckBox Confirm(string text) { return new CheckBox { Text = text, AutoSize = true, MaximumSize = new Size(800, 0), ForeColor = DesignTokens.Text, Margin = new Padding(4, 8, 4, 8) }; }
        private static Label Caption(string text) { return new Label { Text = text, AutoSize = true, ForeColor = DesignTokens.Muted, Anchor = AnchorStyles.Left }; }
        private void Build()
        {
            Panel bottom = new Panel { Dock = DockStyle.Bottom, Height = 74, Padding = new Padding(20, 12, 20, 12) };
            continueButton.Dock = DockStyle.Right; continueButton.Width = 220; continueButton.Enabled = false; continueButton.Click += Complete;
            bottom.Controls.Add(summary); bottom.Controls.Add(continueButton); Controls.Add(bottom);
            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(20, 8) };
            TabPage setup = new TabPage("Preparação") { BackColor = DesignTokens.Window, ForeColor = DesignTokens.Text, Padding = new Padding(20) };
            TabPage options = new TabPage("Opções") { BackColor = DesignTokens.Window, ForeColor = DesignTokens.Text, Padding = new Padding(24) };
            tabs.TabPages.Add(setup); tabs.TabPages.Add(options); Controls.Add(tabs); tabs.BringToFront();
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (float size in new[] { 70f, 160f, 100f, 166f }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, size));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); setup.Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "Antes de espelhar, vamos preparar.\nO Windows foi identificado. Corrija os itens pendentes; nada é instalado sem sua ação.", Font = DesignTokens.Step, ForeColor = DesignTokens.Text, Dock = DockStyle.Fill }, 0, 0);
            TableLayoutPanel fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            string[] captions = { "Seu aparelho", "Como conectar", "Como prefere ajuda", "Android encontrado" }; ComboBox[] boxes = { family, method, experience, devices };
            for (int i = 0; i < boxes.Length; i++) { fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); fields.Controls.Add(Caption(captions[i]), 0, i); fields.Controls.Add(boxes[i], 1, i); boxes[i].AccessibleName = captions[i]; }
            layout.Controls.Add(fields, 0, 1);
            family.Items.AddRange(new object[] { "iPhone ou iPad", "Android" }); experience.Items.AddRange(new object[] { "Passo a passo", "Detalhes técnicos" });
            changing = true; family.SelectedIndex = Selection.Device == DeviceFamily.IOS ? 0 : 1; experience.SelectedIndex = Selection.Experience == ExperienceLevel.Guided ? 0 : 1; PopulateMethods(); changing = false;
            family.SelectedIndexChanged += delegate { if (!changing) { Selection.Device = family.SelectedIndex == 0 ? DeviceFamily.IOS : DeviceFamily.Android; Selection.Method = Selection.Device == DeviceFamily.IOS ? MirrorMethod.AirPlay : MirrorMethod.AndroidUsb; changing = true; PopulateMethods(); changing = false; InvalidateSelection(); } };
            method.SelectedIndexChanged += delegate { if (!changing) { Selection.Method = Selection.Device == DeviceFamily.IOS ? MirrorMethod.AirPlay : method.SelectedIndex == 0 ? MirrorMethod.AndroidUsb : MirrorMethod.AndroidWireless; InvalidateSelection(); } };
            experience.SelectedIndexChanged += delegate { Selection.Experience = experience.SelectedIndex == 0 ? ExperienceLevel.Guided : ExperienceLevel.Technical; Render(); };
            devices.SelectedIndexChanged += delegate { if (!changing) { AndroidDevice selected = devices.SelectedItem as AndroidDevice; string serial = selected == null ? null : selected.Serial; if (serial != Selection.AndroidSerial) { deviceReady.Checked = false; privacy.Checked = false; } Selection.AndroidSerial = serial; Render(); } };
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
            refreshButton.Width = 190; runtimeButton.Width = 185; firewallButton.Width = 200;
            refreshButton.Click += delegate { Inspect(); }; runtimeButton.Click += delegate { PrepareRuntime(); }; firewallButton.Click += delegate { ConfigureFirewall(); };
            actions.Controls.Add(refreshButton); actions.Controls.Add(runtimeButton); actions.Controls.Add(firewallButton); bonjourButton.Width = 230; bonjourButton.Click += delegate { ConfigureBonjour(); }; actions.Controls.Add(bonjourButton); layout.Controls.Add(actions, 0, 2);
            FlowLayoutPanel approvals = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            foreach (CheckBox box in new[] { network, deviceReady, privacy, firewallReviewed, bonjourReviewed }) { approvals.Controls.Add(box); box.CheckedChanged += delegate { Render(); }; }
            layout.Controls.Add(approvals, 0, 3); layout.Controls.Add(checks, 0, 4);
            FlowLayoutPanel preferences = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false }; options.Controls.Add(preferences);
            preferences.Controls.Add(Caption("Nome do receptor AirPlay (sem expor seu usuário do Windows)")); receiverName.Text = Settings.ReceiverName; receiverName.Width = 480; receiverName.MaxLength = 48; receiverName.AccessibleName = "Nome AirPlay"; preferences.Controls.Add(receiverName);
            mute.Checked = Settings.Mute; tray.Checked = Settings.MinimizeToTray; animation.Checked = Settings.EnableAnimation; pin.Checked = Settings.RequirePin;
            foreach (CheckBox box in new[] { mute, tray, animation, pin }) preferences.Controls.Add(box);
            preferences.Controls.Add(Caption("Encerrar sessão automaticamente")); limit.Items.AddRange(new object[] { "Sem limite", "15 minutos", "30 minutos", "60 minutos" }); limit.Dock = DockStyle.None; limit.Width = 260; limit.SelectedIndex = Array.IndexOf(new[] { 0, 15, 30, 60 }, Settings.SessionLimitMinutes); preferences.Controls.Add(limit);
            preferences.Controls.Add(new Label { Text = "O app não grava a tela. Android abre somente em visualização.\nNo iPhone, apps protegidos podem impedir a exibição.\nAo terminar, revogue autorizações de depuração que não precisar mais.", ForeColor = DesignTokens.Muted, AutoSize = true, MaximumSize = new Size(720, 0), Margin = new Padding(0, 24, 0, 0) });
            Render();
        }
        private void PopulateMethods()
        {
            method.Items.Clear(); if (Selection.Device == DeviceFamily.IOS) { method.Items.Add("Sem fio · AirPlay"); method.SelectedIndex = 0; }
            else { method.Items.AddRange(new object[] { "Cabo USB · recomendado", "Wi-Fi · Android já pareado" }); method.SelectedIndex = Selection.Method == MirrorMethod.AndroidWireless ? 1 : 0; }
        }
        private void InvalidateSelection()
        {
            revision++; snapshot = null; report = null; Selection.AndroidSerial = null;
            network.Checked = deviceReady.Checked = privacy.Checked = firewallReviewed.Checked = bonjourReviewed.Checked = false;
            changing = true; devices.Items.Clear(); changing = false; Render(); if (!inspecting) Inspect();
        }
        private void ReadConfirmations()
        {
            Confirmations.TrustedNetwork = network.Checked; Confirmations.PrivacyReady = privacy.Checked;
            Confirmations.AirPlayReady = Selection.Device == DeviceFamily.IOS && deviceReady.Checked;
            Confirmations.UsbDebuggingReady = Selection.Method == MirrorMethod.AndroidUsb && deviceReady.Checked;
            Confirmations.WirelessPaired = Selection.Method == MirrorMethod.AndroidWireless && deviceReady.Checked;
            Confirmations.FirewallReviewed = firewallReviewed.Checked; Confirmations.BonjourReviewed = bonjourReviewed.Checked;
        }
        private void Render()
        {
            ReadConfirmations(); bool ios = Selection.Device == DeviceFamily.IOS; bool wireless = ios || Selection.Method == MirrorMethod.AndroidWireless;
            network.Visible = wireless; firewallButton.Visible = bonjourButton.Visible = ios; runtimeButton.Text = ios ? "Preparar AirPlay…" : "Preparar Android…";
            devices.Enabled = !ios && !inspecting;
            firewallReviewed.Visible = ios && snapshot != null && !snapshot.FirewallStatusKnown;
            bonjourReviewed.Visible = ios && snapshot != null && !snapshot.BonjourStatusKnown;
            deviceReady.Text = ios ? "Desbloqueei o iPhone e sei abrir Central de Controle → Espelhar a Tela." : wireless ? "Pareei meu Android 11+ com depuração sem fio nesta rede." : "Usei cabo de dados, ativei Depuração USB e autorizei este computador no Android.";
            if (snapshot != null)
            {
                report = PreparationChecklist.Evaluate(Selection, Confirmations, snapshot);
                StringBuilder text = new StringBuilder();
                foreach (PreparationCheck check in report.Checks)
                {
                    string mark = check.Status == CheckStatus.Ready ? "Pronto" : check.Status == CheckStatus.Warning ? "Atenção" : check.Status == CheckStatus.Blocked ? "Bloqueado" : "Confirmar";
                    text.AppendLine(mark + " · " + check.Title + (Selection.Experience == ExperienceLevel.Technical ? " [" + check.Id + "]" : ""));
                    if (check.Status != CheckStatus.Ready || Selection.Experience == ExperienceLevel.Technical) text.AppendLine(check.Detail);
                    text.AppendLine();
                }
                checks.Text = text.ToString(); continueButton.Enabled = !inspecting && report.CanStart;
                summary.Text = inspecting ? "Verificando…" : report.CanStart ? "Checklist concluído. Pode continuar." : "Há itens pendentes no checklist.";
            }
            else { checks.Text = "Conecte o aparelho e verifique os requisitos do ambiente escolhido."; continueButton.Enabled = false; summary.Text = inspecting ? "Verificando os requisitos locais…" : "Aguardando verificação."; }
            refreshButton.Enabled = runtimeButton.Enabled = firewallButton.Enabled = bonjourButton.Enabled = !inspecting;
        }
        private void Inspect()
        {
            if (inspecting || closed) return; inspecting = true; int capturedRevision = revision; PreparationSelection captured = Selection.Clone(); EnvironmentStore.ApplyRuntimeDirectory(captured); Selection.AndroidRuntimeDirectory = captured.AndroidRuntimeDirectory; Render();
            BackgroundWorker worker = new BackgroundWorker(); worker.DoWork += delegate(object s, DoWorkEventArgs args) { using (PreparationProbe inspector = new PreparationProbe(applicationDirectory)) args.Result = inspector.Inspect(captured); };
            worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
            {
                worker.Dispose(); inspecting = false; if (closed) return;
                if (capturedRevision != revision) { Inspect(); return; }
                if (args.Error != null) { snapshot = null; Render(); checks.Text = "Não foi possível verificar este ambiente. Reconecte o aparelho e tente novamente. Nenhuma configuração do sistema foi alterada."; return; }
                snapshot = (PreparationSnapshot)args.Result; inspectedAt = DateTime.UtcNow;
                changing = true; devices.Items.Clear();
                foreach (AndroidDevice device in snapshot.AndroidDevices) if (!device.IsEmulator && device.IsWireless == (Selection.Method == MirrorMethod.AndroidWireless)) devices.Items.Add(device);
                if (Selection.AndroidSerial != null)
                    for (int i = 0; i < devices.Items.Count; i++) if (((AndroidDevice)devices.Items[i]).Serial == Selection.AndroidSerial) devices.SelectedIndex = i;
                if (Selection.AndroidSerial == null && devices.Items.Count == 1 && devices.SelectedIndex < 0) devices.SelectedIndex = 0;
                changing = false; AndroidDevice chosen = devices.SelectedItem as AndroidDevice; if (chosen != null) { if (Selection.AndroidSerial != chosen.Serial) { deviceReady.Checked = false; privacy.Checked = false; } Selection.AndroidSerial = chosen.Serial; } Render();
            }; worker.RunWorkerAsync();
        }
        private void PowerChanged(object sender, PowerModeChangedEventArgs e)
        { if (e.Mode == PowerModes.Suspend || e.Mode == PowerModes.Resume) InvalidateFromSystem(); }
        private void NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e) { NetworkChanged(sender, EventArgs.Empty); }
        private void NetworkChanged(object sender, EventArgs e) { if (Selection.Method != MirrorMethod.AndroidUsb) InvalidateFromSystem(); }
        private void InvalidateFromSystem()
        {
            if (closed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(delegate { if (!closed) InvalidateSelection(); })); } catch (InvalidOperationException) { }
        }
        private void Complete(object sender, EventArgs e)
        {
            if (inspecting || snapshot == null || report == null || !report.CanStart) return;
            if (DateTime.UtcNow < inspectedAt || DateTime.UtcNow - inspectedAt > TimeSpan.FromMinutes(5)) { InvalidateSelection(); return; }
            AppSettings updated = new AppSettings { ReceiverName = receiverName.Text.Trim(), Mute = mute.Checked, RequirePin = pin.Checked, MinimizeToTray = tray.Checked, EnableAnimation = animation.Checked, SessionLimitMinutes = new[] { 0, 15, 30, 60 }[limit.SelectedIndex] };
            string error; if (!updated.Validate(out error)) { summary.Text = error; return; }
            Selection.AndroidSerial = report.SelectedAndroidSerial; Settings = updated; DialogResult = DialogResult.OK; Close();
        }
        private void PrepareRuntime()
        {
            string script = Selection.Device == DeviceFamily.IOS ? "download_deps.ps1" : "prepare_android.ps1";
            string size = Selection.Device == DeviceFamily.IOS ? "cerca de 123 MB" : "cerca de 11 MB";
            if (MessageBox.Show(this, "Baixar o motor oficial verificado (" + size + ")?\n\nO SHA-256 será conferido. Nenhum serviço, driver ou regra de firewall será instalado por esta operação.", "Preparar motor", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            RunScript(script, false);
        }
        private void ConfigureBonjour()
        {
            if (MessageBox.Show(this, "Instalar o serviço de descoberta Bonjour da Apple?\n\nEsta ação é opcional e exige administrador. O pacote e a assinatura Apple serão verificados antes de abrir o instalador Apple para você concluir. Se a assinatura não for válida, nada será instalado.", "Configurar descoberta", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK) RunScript("install_bonjour.ps1", true);
        }
        private void ConfigureFirewall()
        {
            if (MessageBox.Show(this, "Permitir somente AirPlay na sub-rede local e em redes Privadas? O Windows solicitará permissão de administrador.", "Configurar firewall", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK) RunScript("setup_firewall.ps1", true);
        }
        private void RunScript(string name, bool elevate)
        {
            string script = Path.Combine(applicationDirectory, "scripts", name); if (!File.Exists(script)) { checks.Text = "Script ausente. Reinstale a partir de um pacote verificado."; return; }
            try
            {
                inspecting = true; revision++; snapshot = null; Render();
                Process process = new Process();
                string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                process.StartInfo = new ProcessStartInfo(powershell, "-NoProfile -ExecutionPolicy RemoteSigned -File " + UxPlayArguments.Quote(script)) { WorkingDirectory = applicationDirectory, UseShellExecute = true, Verb = elevate ? "runas" : "" };
                process.EnableRaisingEvents = true;
                process.Exited += delegate
                {
                    int exitCode = process.ExitCode; process.Dispose(); if (closed || !IsHandleCreated) return;
                    try { BeginInvoke(new Action(delegate { inspecting = false; if (exitCode == 0) Inspect(); else if (exitCode == 3010) { Render(); checks.Text = "O Windows solicitou reinicialização para concluir a instalação. Salve seu trabalho e reinicie o computador manualmente antes de preparar novamente."; } else { Render(); checks.Text = "A preparação não foi concluída. Verifique as mensagens do script ou use o pacote de instalação verificado. Nenhuma conclusão do checklist foi aprovada."; } })); } catch (InvalidOperationException) { }
                };
                try { process.Start(); } catch { process.Dispose(); throw; }
            }
            catch (Win32Exception) { inspecting = false; Render(); checks.Text = "A ação foi cancelada ou não pôde ser iniciada. Verifique novamente quando estiver pronto."; }
        }
    }
}
