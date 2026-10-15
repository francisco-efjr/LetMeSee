using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace LetMeSee
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // Prevent two receivers competing for the same ports in the current user session.
            using (Mutex instance = new Mutex(false, "Local\\LetMeSee.Desktop"))
            {
                bool acquired;
                try { acquired = instance.WaitOne(0, false); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) { MessageBox.Show("O LetMeSee já está aberto. Procure o ícone na bandeja ou a janela na barra de tarefas.", "LetMeSee"); return; }
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }

    public sealed class MainForm : Form
    {
        private readonly string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly SettingsStore store = new SettingsStore();
        private readonly ReceiverService receiver;
        private AppSettings settings;
        private readonly Panel content = new Panel();
        private readonly Label status = new Label();
        private readonly Label footer = new Label();
        private readonly Timer clock = new Timer();
        private readonly ToolTip tips = new ToolTip();
        private NotifyIcon tray;
        private ContextMenuStrip trayMenu;
        private ActionButton toggle;
        private Label message;
        private Label elapsed;
        private SonarPanel sonar;
        private ComboBox addresses;
        private DateTime? startedAt;
        private bool forceClose;
        private string currentPage = "Espelhamento";
        private ActionButton[] navigation;
        private int diagnosticGeneration;
        private bool disposed;

        public MainForm()
        {
            settings = store.Load(); receiver = new ReceiverService(appDirectory);
            receiver.StateChanged += ReceiverChanged;
            Text = "LetMeSee · Receptor AirPlay";
            Font = DesignTokens.Body; ForeColor = DesignTokens.Text; BackColor = DesignTokens.Window;
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(1100, 780); MinimumSize = new Size(1040, 780);
            StartPosition = FormStartPosition.CenterScreen;
            string iconPath = Path.Combine(appDirectory, "app_icon.ico");
            if (File.Exists(iconPath)) Icon = new Icon(iconPath);
            else Icon = SystemIcons.Application;
            BuildShell(); BuildTray(); ShowMirror();
            FormClosing += ClosingRequested;
            clock.Interval = 1000; clock.Tick += TickClock; clock.Start();
            Shown += delegate
            {
                if (sonar != null) sonar.Play(settings.EnableAnimation);
                if (!string.IsNullOrEmpty(store.LastError)) Notify("As preferências não puderam ser lidas. Os valores padrão estão em uso.");
            };
        }

        private static Label TextLabel(string text, Font font, Color color, int width, int height)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = false, Width = width, Height = height, BackColor = Color.Transparent };
        }
        private static void Place(Control parent, Control child, int x, int y) { child.Location = new Point(x, y); parent.Controls.Add(child); }
        private static ActionButton Button(string text, bool primary, int width, EventHandler action)
        {
            ActionButton button = new ActionButton(text, primary) { Width = width }; button.Click += action; return button;
        }
        private SurfacePanel Card(int height)
        {
            return new SurfacePanel { Height = height, Width = Math.Max(760, content.ClientSize.Width - 48), Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
        }
        private void BuildShell()
        {
            Panel sidebar = new Panel { Dock = DockStyle.Left, Width = 196, BackColor = DesignTokens.Surface };
            Place(sidebar, TextLabel("LETMESEE", DesignTokens.Brand, DesignTokens.Text, 170, 36), 20, 30);
            Place(sidebar, TextLabel("SUA TELA, POR PERTO.", DesignTokens.Fine, DesignTokens.Muted, 176, 28), 20, 67);
            navigation = new ActionButton[3];
            string[] pages = { "Espelhamento", "Diagnóstico", "Preferências" };
            for (int i = 0; i < pages.Length; i++)
            {
                string page = pages[i];
                navigation[i] = Button(page, false, 164, delegate { Navigate(page); });
                navigation[i].TextAlign = ContentAlignment.MiddleLeft;
                navigation[i].AccessibleDescription = "Abrir " + page;
                Place(sidebar, navigation[i], 16, 132 + i * 52);
            }
            Label local = TextLabel("RECEPÇÃO LOCAL\n\nSem conta.\nSem telemetria no app.\nVocê decide quando ligar.", DesignTokens.Small, DesignTokens.Muted, 158, 125);
            local.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Place(sidebar, local, 20, ClientSize.Height - 160);
            Panel top = new Panel { Dock = DockStyle.Top, Height = 92 };
            Place(top, TextLabel("Estação de espelhamento", DesignTokens.NavigationTitle, DesignTokens.Text, 420, 40), 24, 20);
            status.Text = "○ Desligado"; status.ForeColor = DesignTokens.Muted; status.Size = new Size(240, 28);
            status.TextAlign = ContentAlignment.MiddleRight; status.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Place(top, status, ClientSize.Width - sidebar.Width - 264, 31);
            footer.Dock = DockStyle.Bottom; footer.Height = 34; footer.Padding = new Padding(24, 6, 0, 0);
            footer.Font = DesignTokens.Small; footer.ForeColor = DesignTokens.Muted;
            footer.Text = "AirPlay para Windows · o receptor começa desligado";
            content.Dock = DockStyle.Fill; content.AutoScroll = true;
            Panel body = new Panel { Dock = DockStyle.Fill }; body.Controls.Add(content); body.Controls.Add(footer); body.Controls.Add(top);
            Controls.Add(body); Controls.Add(sidebar);
        }
        private void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Abrir LetMeSee", null, delegate { Show(); WindowState = FormWindowState.Normal; Activate(); });
            trayMenu.Items.Add("Parar receptor", null, delegate { receiver.Stop(); });
            trayMenu.Items.Add("Sair", null, delegate { forceClose = true; Close(); });
            tray = new NotifyIcon { Icon = Icon, Text = "LetMeSee · desligado", Visible = true, ContextMenuStrip = trayMenu };
            tray.DoubleClick += delegate { Show(); WindowState = FormWindowState.Normal; Activate(); };
        }
        private void Navigate(string page)
        {
            currentPage = page;
            if (page == "Espelhamento") ShowMirror();
            else if (page == "Diagnóstico") ShowDiagnostics();
            else ShowPreferences();
        }
        private void ClearPage()
        {
            diagnosticGeneration++;
            while (content.Controls.Count > 0) { Control control = content.Controls[0]; content.Controls.RemoveAt(0); control.Dispose(); }
            toggle = null; message = null; elapsed = null; sonar = null; addresses = null;
            for (int i = 0; i < navigation.Length; i++)
            {
                bool selected = navigation[i].Text == currentPage;
                navigation[i].BackColor = selected ? DesignTokens.Raised : DesignTokens.Surface;
                navigation[i].ForeColor = selected ? DesignTokens.Accent : DesignTokens.Muted;
                navigation[i].FlatAppearance.BorderColor = selected ? DesignTokens.Accent : DesignTokens.Surface;
            }
        }
        private void ShowMirror()
        {
            ClearPage();
            SurfacePanel hero = Card(220); hero.BackColor = DesignTokens.Paper; hero.Paper = true;
            Place(content, hero, 24, 0);
            Place(hero, TextLabel("DO IPHONE PARA O SEU PC", DesignTokens.CaptionStrong, DesignTokens.Orange, 400, 28), 24, 22);
            Place(hero, TextLabel("Sua tela.\nEm boa companhia.", DesignTokens.HeroTitle, DesignTokens.Ink, 460, 108), 22, 48);
            Place(hero, TextLabel("Compartilhe uma ideia, apresente um app\nou veja tudo em uma tela maior.", DesignTokens.Body, DesignTokens.Ink, 440, 48), 24, 164);
            sonar = new SonarPanel { Size = new Size(220, 200), Anchor = AnchorStyles.Right | AnchorStyles.Top };
            Place(hero, sonar, hero.Width - 244, 10); sonar.Play(false);

            SurfacePanel session = Card(112); Place(content, session, 24, 236);
            message = TextLabel("Receptor desligado", DesignTokens.CardTitle, DesignTokens.Text, 450, 30); Place(session, message, 20, 16);
            elapsed = TextLabel(settings.RequirePin ? "Código de conexão ativado · use uma rede privada." : "Sem código de conexão · use apenas uma rede de confiança.", DesignTokens.Small, DesignTokens.Muted, 445, 44); Place(session, elapsed, 20, 52);
            toggle = Button("&Iniciar receptor", true, 220, ToggleReceiver); toggle.Height = 48;
            toggle.Anchor = AnchorStyles.Right | AnchorStyles.Top; Place(session, toggle, session.Width - 240, 32);
            tips.SetToolTip(toggle, "Anuncia este computador para dispositivos AirPlay na rede local. Alt+I para iniciar.");

            SurfacePanel connection = Card(130); Place(content, connection, 24, 364);
            Place(connection, TextLabel("NOME NO IPHONE", DesignTokens.Caption, DesignTokens.Muted, 280, 24), 20, 16);
            Label name = TextLabel(settings.ReceiverName, DesignTokens.Name, DesignTokens.Text, 350, 40); name.AutoEllipsis = true;
            Place(connection, name, 20, 44); tips.SetToolTip(name, settings.ReceiverName);
            Place(connection, Button("&Copiar nome", false, 140, delegate { CopyName(); }), 20, 84);
            Place(connection, TextLabel("REDE DO COMPUTADOR", DesignTokens.Caption, DesignTokens.Muted, 340, 24), 404, 16);
            addresses = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 312, BackColor = DesignTokens.Raised, ForeColor = DesignTokens.Text, AccessibleName = "Endereços locais disponíveis" };
            Place(connection, addresses, 404, 46);
            Place(connection, Button("Atualizar rede", false, 148, delegate { RefreshNetwork(); }), 404, 84);
            RefreshNetwork();

            SurfacePanel guide = Card(112); Place(content, guide, 24, 510);
            string[] steps = { "01  Conectar", "02  Espelhar", "03  Selecionar" };
            string[] detail = { "iPhone e PC na mesma\nrede Wi-Fi de confiança.", "Abra a Central de Controle\ne toque em Espelhar a Tela.", "Escolha o nome do receptor.\nA imagem abre em outra janela." };
            for (int i = 0; i < 3; i++)
            {
                Place(guide, TextLabel(steps[i], DesignTokens.Step, DesignTokens.Accent, 230, 28), 20 + i * 248, 16);
                Place(guide, TextLabel(detail[i], DesignTokens.Small, DesignTokens.Muted, 232, 52), 20 + i * 248, 48);
            }
            UpdateState();
        }
        private void RefreshNetwork()
        {
            if (addresses == null) return;
            addresses.Items.Clear();
            foreach (NetworkAddress address in NetworkService.GetActiveIPv4()) addresses.Items.Add(address.Address + " · " + address.Name);
            if (addresses.Items.Count == 0) addresses.Items.Add("Nenhuma rede local ativa");
            addresses.SelectedIndex = 0;
            tips.SetToolTip(addresses, "Lista de endereços para conferência. O UxPlay anuncia nas interfaces disponíveis; esta lista não restringe o acesso.");
        }
        private void ToggleReceiver(object sender, EventArgs e)
        {
            if (receiver.HasOwnedProcess || receiver.State == ReceiverState.Starting) receiver.Stop();
            else
            {
                receiver.Start(settings.Clone());
            }
        }
        private void ReceiverChanged(object sender, ReceiverStateChangedEventArgs e)
        {
            if (disposed || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action(UpdateState)); } catch (InvalidOperationException) { }
        }
        private void UpdateState()
        {
            bool running = receiver.State == ReceiverState.Running;
            bool starting = receiver.State == ReceiverState.Starting;
            if (running && !startedAt.HasValue) startedAt = DateTime.UtcNow;
            if (!running) startedAt = null;
            status.Text = running ? "● Receptor ativo" : starting ? "◌ Verificando motor" : receiver.State == ReceiverState.Error ? "! Atenção" : "○ Desligado";
            status.ForeColor = running ? DesignTokens.Success : receiver.State == ReceiverState.Error ? DesignTokens.Danger : DesignTokens.Muted;
            tray.Text = running ? "LetMeSee · receptor ativo" : starting ? "LetMeSee · iniciando" : receiver.HasOwnedProcess ? "LetMeSee · conferir encerramento" : "LetMeSee · desligado";
            if (toggle != null)
            {
                toggle.Text = receiver.HasOwnedProcess || starting ? "&Parar receptor" : "&Iniciar receptor";
                toggle.AccessibleName = toggle.Text.Replace("&", "");
                message.Text = running ? "Pronto para receber" : starting ? "Conferindo arquivos do motor…" : receiver.State == ReceiverState.Error ? "O receptor precisa de atenção" : "Receptor desligado";
                message.ForeColor = receiver.State == ReceiverState.Error ? DesignTokens.Danger : DesignTokens.Text;
                elapsed.Text = running && settings.RequirePin ? "Código AirPlay: " + receiver.SessionPin + " · digite no iPhone ao conectar." : receiver.State == ReceiverState.Stopped ? (settings.RequirePin ? "Código de conexão ativado · use uma rede privada." : "Sem código de conexão · use apenas uma rede de confiança.") : receiver.StatusMessage;
            }
            footer.Text = running ? "Receptor ativo · disponibilidade não confirma um iPhone conectado" : receiver.HasOwnedProcess ? "Atenção: confira o encerramento do motor antes de sair." : "AirPlay para Windows · recepção sob seu controle";
            if (currentPage == "Preferências") ShowPreferences();
        }
        private void TickClock(object sender, EventArgs e)
        {
            if (!startedAt.HasValue) return;
            TimeSpan duration = DateTime.UtcNow - startedAt.Value;
            if (elapsed != null) elapsed.Text = (settings.RequirePin ? "Código AirPlay: " + receiver.SessionPin + " · " : "Sem PIN · ") + "Ativo há " + duration.ToString(@"hh\:mm\:ss") + " · " + (settings.SessionLimitMinutes == 0 ? "sem limite" : "limite de " + settings.SessionLimitMinutes + " min");
        }
        private void CopyName()
        {
            try { Clipboard.SetText(settings.ReceiverName); Notify("Nome do receptor copiado."); }
            catch (System.Runtime.InteropServices.ExternalException) { Notify("A área de transferência está ocupada. Tente novamente."); }
        }
        private void Notify(string text) { footer.Text = text; }

        private void ShowDiagnostics()
        {
            ClearPage();
            Place(content, TextLabel("Vamos conferir a conexão.", DesignTokens.Heading, DesignTokens.Text, 700, 48), 24, 4);
            Place(content, TextLabel("Verificações locais. Nenhuma informação é enviada.", DesignTokens.Body, DesignTokens.Muted, 700, 32), 24, 60);
            SurfacePanel card = Card(276); Place(content, card, 24, 112);
            Label report = TextLabel("Conferindo motor, rede e descoberta…", DesignTokens.Body, DesignTokens.Text, 700, 230); Place(card, report, 20, 20);
            ActionButton refresh = Button("Verificar de novo", false, 190, delegate { ShowDiagnostics(); }); refresh.Enabled = false; Place(content, refresh, 24, 408);
            Place(content, Button("Configurar firewall…", false, 220, ConfigureFirewall), 230, 408);
            Place(content, TextLabel("O firewall aceita apenas rede Privada e sub-rede local. A configuração pede\npermissão de administrador. O aplicativo e o diagnóstico não precisam dela.\n\nSe o PC não aparecer: confira a mesma rede, o isolamento de clientes no Wi-Fi,\na descoberta Bonjour e se a VPN permite acesso à rede local.", DesignTokens.Body, DesignTokens.Muted, 750, 152), 24, 476);
            int generation = diagnosticGeneration;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs args) { args.Result = DiagnosticService.Inspect(appDirectory); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args)
            {
                worker.Dispose();
                if (disposed || generation != diagnosticGeneration || report.IsDisposed) return;
                refresh.Enabled = true;
                if (args.Error != null) { report.Text = "Não foi possível concluir a verificação. Tente novamente."; return; }
                DiagnosticReport result = (DiagnosticReport)args.Result;
                report.Text = "MOTOR AIRPLAY\n" + result.Runtime + "\n\nREDE LOCAL\n" + result.Network + "\n\nDESCOBERTA BONJOUR\n" + result.Bonjour;
            };
            worker.RunWorkerAsync();
        }
        private void ConfigureFirewall(object sender, EventArgs e)
        {
            string script = Path.Combine(appDirectory, "scripts\\setup_firewall.ps1");
            if (!File.Exists(script)) { Notify("Script de firewall ausente. Reinstale a partir de uma versão verificada."); return; }
            if (MessageBox.Show(this, "Permitir que este receptor receba conexões de dispositivos da sub-rede local, apenas em redes marcadas como Privadas no Windows?", "Configurar firewall", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            try
            {
                string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
                Process operation = new Process();
                operation.StartInfo = new ProcessStartInfo(powershell, "-NoProfile -ExecutionPolicy RemoteSigned -File \"" + script + "\"") { UseShellExecute = true, Verb = "runas", WorkingDirectory = appDirectory };
                operation.EnableRaisingEvents = true;
                operation.Exited += delegate
                {
                    int exitCode = operation.ExitCode; operation.Dispose();
                    if (disposed || !IsHandleCreated) return;
                    try { BeginInvoke(new Action(delegate { Notify(exitCode == 0 ? "Regras de rede privada configuradas. Confira o Diagnóstico." : "A configuração do firewall falhou. Execute configurar_firewall.bat para ver os detalhes."); })); }
                    catch (InvalidOperationException) { }
                };
                try { operation.Start(); } catch { operation.Dispose(); throw; }
                Notify("Aguardando a configuração do firewall…");
            }
            catch (Win32Exception) { Notify("A configuração não foi iniciada. Você pode tentar novamente quando precisar."); }
        }
        private void ShowPreferences()
        {
            ClearPage();
            Place(content, TextLabel("Do seu jeito.", DesignTokens.Heading, DesignTokens.Text, 700, 48), 24, 4);
            Place(content, TextLabel("Preferências salvas somente neste usuário do Windows.", DesignTokens.Body, DesignTokens.Muted, 700, 32), 24, 60);
            SurfacePanel card = Card(476); Place(content, card, 24, 108);
            bool busy = receiver.HasOwnedProcess || receiver.State == ReceiverState.Starting;
            Place(card, TextLabel("Nome que aparece no iPhone", DesignTokens.Step, DesignTokens.Text, 600, 28), 20, 20);
            TextBox name = new TextBox { Text = settings.ReceiverName, MaxLength = 48, Width = 460, BackColor = DesignTokens.Raised, ForeColor = DesignTokens.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = "Nome do receptor" }; Place(card, name, 20, 56);
            Place(card, TextLabel("Use um nome de 1 a 48 caracteres. Evite dados pessoais.", DesignTokens.Small, DesignTokens.Muted, 660, 32), 20, 92);
            CheckBox pin = Preference("Exigir código AirPlay de sessão ao conectar", settings.RequirePin); Place(card, pin, 20, 140);
            CheckBox mute = Preference("Silenciar o áudio recebido", settings.Mute); Place(card, mute, 20, 184);
            CheckBox minimize = Preference("Continuar na bandeja ao fechar a janela", settings.MinimizeToTray); Place(card, minimize, 20, 228);
            CheckBox animation = Preference("Animar o radar ao abrir o aplicativo", settings.EnableAnimation); Place(card, animation, 20, 272);
            Place(card, TextLabel("Desligar automaticamente após", DesignTokens.Body, DesignTokens.Text, 400, 28), 20, 336);
            ComboBox limit = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Limite de tempo da sessão" };
            limit.Items.AddRange(new object[] { "Sem limite", "15 minutos", "30 minutos", "60 minutos" });
            int[] values = { 0, 15, 30, 60 }; limit.SelectedIndex = Array.IndexOf(values, settings.SessionLimitMinutes); Place(card, limit, 20, 372);
            Label feedback = TextLabel(busy ? "Pare o receptor para alterar as preferências." : "As alterações serão usadas na próxima sessão.", DesignTokens.Small, DesignTokens.Muted, 700, 32); Place(card, feedback, 20, 428);
            name.Enabled = pin.Enabled = mute.Enabled = minimize.Enabled = animation.Enabled = limit.Enabled = !busy;
            ActionButton save = Button("&Salvar preferências", true, 228, delegate
            {
                AppSettings updated = new AppSettings { ReceiverName = name.Text.Trim(), RequirePin = pin.Checked, Mute = mute.Checked, MinimizeToTray = minimize.Checked, EnableAnimation = animation.Checked, SessionLimitMinutes = values[limit.SelectedIndex] };
                string error;
                if (!updated.Validate(out error)) { feedback.Text = error; feedback.ForeColor = DesignTokens.Danger; name.Focus(); return; }
                if (store.Save(updated)) { settings = updated; feedback.Text = "Preferências salvas."; feedback.ForeColor = DesignTokens.Success; }
                else { feedback.Text = store.LastError; feedback.ForeColor = DesignTokens.Danger; }
            });
            save.Enabled = !busy; Place(content, save, 24, 604);
        }
        private static CheckBox Preference(string text, bool value)
        {
            return new CheckBox { Text = text, Checked = value, Width = 670, Height = 36, ForeColor = DesignTokens.Text, AccessibleName = text };
        }
        private void ClosingRequested(object sender, FormClosingEventArgs e)
        {
            if (!forceClose && settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; Hide(); tray.ShowBalloonTip(2500, "LetMeSee", "O aplicativo continua na bandeja. Use Sair para encerrá-lo.", ToolTipIcon.Info); return;
            }
            receiver.Stop();
            if (receiver.HasOwnedProcess || receiver.State == ReceiverState.Starting)
            { e.Cancel = true; Show(); Notify("Não foi possível parar o motor. Tente novamente antes de sair."); }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                disposed = true; receiver.StateChanged -= ReceiverChanged; receiver.Dispose(); clock.Dispose(); tips.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); }
                if (trayMenu != null) trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
