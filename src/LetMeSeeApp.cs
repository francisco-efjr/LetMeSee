using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Forms;

namespace LetMeSee
{
    public class MainForm : Form
    {
        private Process _mirrorProcess;
        private bool _isRunning = false;
        private string _deviceName;
        private string _localIp;

        private Panel _headerPanel;
        private Label _titleLabel;
        private Label _subtitleLabel;
        private Label _statusDot;
        private Label _statusLabel;
        private Button _toggleButton;
        private Panel _cardPanel;
        private Label _deviceTitleLabel;
        private Label _deviceValueLabel;
        private Button _copyDeviceButton;
        private Label _ipTitleLabel;
        private Label _ipValueLabel;
        private Label _instructionLabel;
        private CheckBox _trayCheckBox;
        private Button _firewallButton;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _forceClose = false;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            InitializeData();
            InitializeComponents();
            InitializeTray();
        }

        private void InitializeData()
        {
            string user = Environment.UserName;
            if (string.IsNullOrEmpty(user))
            {
                user = Environment.MachineName;
            }
            _deviceName = "LetMeSee - " + user;
            _localIp = GetLocalIPv4();
        }

        private string GetLocalIPv4()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                    {
                        foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !ip.Address.ToString().StartsWith("127.") &&
                                !ip.Address.ToString().StartsWith("169.254."))
                            {
                                return ip.Address.ToString() + " (" + ni.Name + ")";
                            }
                        }
                    }
                }
            }
            catch { }
            return "Nao identificado";
        }

        private void InitializeComponents()
        {
            // Janela Principal
            this.Text = "LetMeSee - Espelho iOS";
            this.Size = new Size(500, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 37); // Tema escuro moderno
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Cabecalho
            _headerPanel = new Panel();
            _headerPanel.Location = new Point(0, 0);
            _headerPanel.Size = new Size(500, 75);
            _headerPanel.BackColor = Color.FromArgb(17, 17, 27);

            _titleLabel = new Label();
            _titleLabel.Text = "📱 LetMeSee";
            _titleLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _titleLabel.ForeColor = Color.FromArgb(137, 180, 250);
            _titleLabel.Location = new Point(24, 14);
            _titleLabel.AutoSize = true;

            _subtitleLabel = new Label();
            _subtitleLabel.Text = "Receptor de Espelhamento de Tela iOS via AirPlay";
            _subtitleLabel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            _subtitleLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _subtitleLabel.Location = new Point(28, 45);
            _subtitleLabel.AutoSize = true;

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);

            // Status Bar Visual
            _statusDot = new Label();
            _statusDot.Text = "●";
            _statusDot.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            _statusDot.ForeColor = Color.FromArgb(108, 112, 134);
            _statusDot.Location = new Point(24, 90);
            _statusDot.Size = new Size(20, 25);

            _statusLabel = new Label();
            _statusLabel.Text = "Servico Desligado";
            _statusLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _statusLabel.ForeColor = Color.FromArgb(166, 173, 200);
            _statusLabel.Location = new Point(48, 92);
            _statusLabel.Size = new Size(300, 25);

            // Botao Liga/Desliga Principal (Toggle)
            _toggleButton = new Button();
            _toggleButton.Text = "▶  LIGAR ESPELHAMENTO";
            _toggleButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _toggleButton.BackColor = Color.FromArgb(137, 180, 250);
            _toggleButton.ForeColor = Color.FromArgb(17, 17, 27);
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.FlatAppearance.BorderSize = 0;
            _toggleButton.Location = new Point(24, 125);
            _toggleButton.Size = new Size(436, 46);
            _toggleButton.Cursor = Cursors.Hand;
            _toggleButton.Click += ToggleService;

            // Card com Informacoes de Conexao
            _cardPanel = new Panel();
            _cardPanel.Location = new Point(24, 185);
            _cardPanel.Size = new Size(436, 135);
            _cardPanel.BackColor = Color.FromArgb(30, 30, 46);
            _cardPanel.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(49, 50, 68), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1);
                }
            };

            _deviceTitleLabel = new Label();
            _deviceTitleLabel.Text = "NOME QUE APARECE NO IPHONE:";
            _deviceTitleLabel.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _deviceTitleLabel.ForeColor = Color.FromArgb(147, 153, 178);
            _deviceTitleLabel.Location = new Point(14, 12);
            _deviceTitleLabel.AutoSize = true;

            _deviceValueLabel = new Label();
            _deviceValueLabel.Text = _deviceName;
            _deviceValueLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _deviceValueLabel.ForeColor = Color.FromArgb(249, 226, 175);
            _deviceValueLabel.Location = new Point(14, 30);
            _deviceValueLabel.AutoSize = true;

            _copyDeviceButton = new Button();
            _copyDeviceButton.Text = "Copiar";
            _copyDeviceButton.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            _copyDeviceButton.BackColor = Color.FromArgb(49, 50, 68);
            _copyDeviceButton.ForeColor = Color.White;
            _copyDeviceButton.FlatStyle = FlatStyle.Flat;
            _copyDeviceButton.FlatAppearance.BorderSize = 0;
            _copyDeviceButton.Location = new Point(356, 28);
            _copyDeviceButton.Size = new Size(65, 26);
            _copyDeviceButton.Cursor = Cursors.Hand;
            _copyDeviceButton.Click += (s, e) =>
            {
                Clipboard.SetText(_deviceName);
                _copyDeviceButton.Text = "Copiado!";
                Timer t = new Timer { Interval = 1500 };
                t.Tick += (ts, te) => { _copyDeviceButton.Text = "Copiar"; t.Stop(); };
                t.Start();
            };

            _ipTitleLabel = new Label();
            _ipTitleLabel.Text = "IP DO COMPUTADOR:";
            _ipTitleLabel.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _ipTitleLabel.ForeColor = Color.FromArgb(147, 153, 178);
            _ipTitleLabel.Location = new Point(14, 62);
            _ipTitleLabel.AutoSize = true;

            _ipValueLabel = new Label();
            _ipValueLabel.Text = _localIp;
            _ipValueLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            _ipValueLabel.ForeColor = Color.FromArgb(186, 194, 222);
            _ipValueLabel.Location = new Point(14, 80);
            _ipValueLabel.AutoSize = true;

            _instructionLabel = new Label();
            _instructionLabel.Text = "💡 No iPhone: Central de Controle > [Espelhar a Tela] > Escolha o nome acima.";
            _instructionLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _instructionLabel.ForeColor = Color.FromArgb(166, 227, 161);
            _instructionLabel.Location = new Point(14, 106);
            _instructionLabel.AutoSize = true;

            _cardPanel.Controls.Add(_deviceTitleLabel);
            _cardPanel.Controls.Add(_deviceValueLabel);
            _cardPanel.Controls.Add(_copyDeviceButton);
            _cardPanel.Controls.Add(_ipTitleLabel);
            _cardPanel.Controls.Add(_ipValueLabel);
            _cardPanel.Controls.Add(_instructionLabel);

            // Rodape
            _trayCheckBox = new CheckBox();
            _trayCheckBox.Text = "Minimizar para a bandeja ao fechar";
            _trayCheckBox.ForeColor = Color.FromArgb(166, 173, 200);
            _trayCheckBox.Font = new Font("Segoe UI", 8.5f);
            _trayCheckBox.Location = new Point(26, 332);
            _trayCheckBox.Size = new Size(240, 24);
            _trayCheckBox.Checked = true;

            _firewallButton = new Button();
            _firewallButton.Text = "Configurar Firewall";
            _firewallButton.Font = new Font("Segoe UI", 8.5f);
            _firewallButton.BackColor = Color.FromArgb(49, 50, 68);
            _firewallButton.ForeColor = Color.FromArgb(205, 214, 244);
            _firewallButton.FlatStyle = FlatStyle.Flat;
            _firewallButton.FlatAppearance.BorderSize = 0;
            _firewallButton.Location = new Point(310, 330);
            _firewallButton.Size = new Size(150, 28);
            _firewallButton.Cursor = Cursors.Hand;
            _firewallButton.Click += RunFirewallConfig;

            // Adicionar componentes a janela
            this.Controls.Add(_headerPanel);
            this.Controls.Add(_statusDot);
            this.Controls.Add(_statusLabel);
            this.Controls.Add(_toggleButton);
            this.Controls.Add(_cardPanel);
            this.Controls.Add(_trayCheckBox);
            this.Controls.Add(_firewallButton);

            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeTray()
        {
            _trayMenu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("Abrir LetMeSee", null, (s, e) => ShowWindow());
            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Ligar / Desligar", null, ToggleService);
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Sair do Programa", null, (s, e) => ForceExit());

            _trayMenu.Items.Add(openItem);
            _trayMenu.Items.Add(toggleItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "LetMeSee - Espelho iOS";
            _trayIcon.Icon = SystemIcons.Application;
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.Visible = true;
            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ToggleService(object sender, EventArgs e)
        {
            if (_isRunning)
            {
                StopService();
            }
            else
            {
                StartService();
            }
        }

        private void StartService()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string binDir = Path.Combine(appDir, "bin");
            string uxplayExe = Path.Combine(binDir, "uxplay.exe");

            // Validar existencia do executavel
            if (!File.Exists(uxplayExe))
            {
                string downloadScript = Path.Combine(appDir, "scripts\\download_deps.ps1");
                if (File.Exists(downloadScript))
                {
                    DialogResult dr = MessageBox.Show(
                        "Os arquivos do motor de espelhamento (UxPlay) ainda nao foram baixados.\n\nDeseja realizar o download automatico agora?",
                        "LetMeSee - Dependencias Necessarias",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (dr == DialogResult.Yes)
                    {
                        ProcessStartInfo psiDl = new ProcessStartInfo();
                        psiDl.FileName = "powershell.exe";
                        psiDl.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + downloadScript + "\"";
                        psiDl.UseShellExecute = true;
                        Process pDl = Process.Start(psiDl);
                        pDl.WaitForExit();

                        if (!File.Exists(uxplayExe))
                        {
                            MessageBox.Show("O download nao foi concluido. Tente novamente ou verifique sua conexao com a internet.", "LetMeSee", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("Executavel nao encontrado em:\n" + uxplayExe, "LetMeSee - Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = uxplayExe;
                psi.Arguments = "-n \"" + _deviceName + "\" -p -vs d3d11videosink";
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = binDir;

                // Configurar ambiente PATH e Plugins do GStreamer
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                string newPath = binDir + ";" + Path.Combine(binDir, "bin") + ";" + currentPath;
                psi.EnvironmentVariables["PATH"] = newPath;
                psi.EnvironmentVariables["GST_PLUGIN_PATH"] = Path.Combine(binDir, "lib\\gstreamer-1.0");

                _mirrorProcess = new Process();
                _mirrorProcess.StartInfo = psi;
                _mirrorProcess.EnableRaisingEvents = true;
                _mirrorProcess.Exited += (s, e) =>
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (_isRunning)
                        {
                            SetUIState(false);
                        }
                    }));
                };

                _mirrorProcess.Start();
                SetUIState(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Falha ao iniciar o servico:\n" + ex.Message, "LetMeSee - Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetUIState(false);
            }
        }

        private void StopService()
        {
            try
            {
                if (_mirrorProcess != null && !_mirrorProcess.HasExited)
                {
                    _mirrorProcess.Kill();
                    _mirrorProcess.WaitForExit(2000);
                }
            }
            catch { }
            finally
            {
                _mirrorProcess = null;
                SetUIState(false);
            }
        }

        private void SetUIState(bool running)
        {
            _isRunning = running;
            if (running)
            {
                _statusDot.ForeColor = Color.FromArgb(166, 227, 161); // Verde vivo
                _statusLabel.Text = "Ativo - Aguardando conexao do iPhone";
                _statusLabel.ForeColor = Color.FromArgb(166, 227, 161);

                _toggleButton.Text = "⏹  DESLIGAR ESPELHAMENTO";
                _toggleButton.BackColor = Color.FromArgb(243, 139, 168); // Vermelho pastel
                _toggleButton.ForeColor = Color.FromArgb(17, 17, 27);

                _trayIcon.Text = "LetMeSee - Ativo (" + _deviceName + ")";
            }
            else
            {
                _statusDot.ForeColor = Color.FromArgb(108, 112, 134); // Cinza
                _statusLabel.Text = "Servico Desligado";
                _statusLabel.ForeColor = Color.FromArgb(166, 173, 200);

                _toggleButton.Text = "▶  LIGAR ESPELHAMENTO";
                _toggleButton.BackColor = Color.FromArgb(137, 180, 250); // Azul
                _toggleButton.ForeColor = Color.FromArgb(17, 17, 27);

                _trayIcon.Text = "LetMeSee - Desligado";
            }
        }

        private void RunFirewallConfig(object sender, EventArgs e)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string bat = Path.Combine(appDir, "configurar_firewall.bat");
            if (File.Exists(bat))
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = bat;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                try
                {
                    Process.Start(psi);
                }
                catch { }
            }
            else
            {
                MessageBox.Show("Arquivo 'configurar_firewall.bat' nao encontrado.", "LetMeSee", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void ForceExit()
        {
            _forceClose = true;
            StopService();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_forceClose && _trayCheckBox.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                _trayIcon.ShowBalloonTip(2000, "LetMeSee", "O aplicativo continua ativo na bandeja do sistema.", ToolTipIcon.Info);
            }
            else
            {
                StopService();
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            }
        }
    }
}
