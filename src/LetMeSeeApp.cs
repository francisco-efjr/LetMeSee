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
        // Paleta de Cores Tática (Inspirada no Ícone Oficial)
        private static readonly Color BgDark = Color.FromArgb(18, 20, 24);         // #121418 - Fundo principal grafite
        private static readonly Color HeaderDark = Color.FromArgb(12, 13, 16);     // #0C0D10 - Barra superior
        private static readonly Color CardBg = Color.FromArgb(26, 29, 35);         // #1A1D23 - Superficie do card
        private static readonly Color CardBorder = Color.FromArgb(55, 59, 68);     // #373B44 - Borda sutil
        private static readonly Color TacticalOrange = Color.FromArgb(215, 84, 43); // #D7542B - Laranja do icone (Radar)
        private static readonly Color OrangeHover = Color.FromArgb(235, 99, 56);   // #EB6338 - Laranja destaque
        private static readonly Color SonarGreen = Color.FromArgb(0, 230, 118);    // #00E676 - Verde radar ativo
        private static readonly Color StatusOff = Color.FromArgb(120, 125, 135);   // Cinza inativo
        private static readonly Color TextPrimary = Color.FromArgb(245, 246, 248); // Branco suave
        private static readonly Color TextSecondary = Color.FromArgb(155, 160, 170); // Cinza texto

        // Ícone oficial embutido em Base64
        private const string IconBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAANn0lEQVR4nO1ZCVhV17Ve+0x3OHAvMyJWlFBFDZMaiYlhsCWDIRpR" +
            "0xh52q9pML5nTPQ1DjGKphjnVBqSGp9orZK0ahpArL4vEKmCDAoyI8MFBGMAQeByudMZ9vv24ZL0aVTUNH0vX9b3HS737nP2Xv9a" +
            "/1p77XUAfpQf5YEEPeD49yUYfqiCbjeAMf6/Yn1FEELf6gXmpu9EaUyUP3z48CkAGCsIggwAFPxrRGZZlqzdjDF+xgFC0fF2AL4W" +
            "u90+QaVSjSb/I4QIrH+Khvg2v5M1McbKp81mU9/u+dsCQAiZifVFUVQ8gBH1nSgrY/KJAQECGgFQyGGgoRsGOQCyTJYFGSFEEV3u" +
            "GYCDNkNaU6zdAug+kwF5SsIYGIRAy1DAUgjLgGSLhPGAKIM4qOwQfwkkilWrCbAhHaj7AaBYglieFawwuegTICAwou8pqxHFVTQF" +
            "PEtho12SS68PoNyOfqq6x0pfs0rQa5dBJPQkaxGTSxLwPA/rN2wEtUYjyLJMUxSF7w/AN0gUEKwwfAASBmAQgI5j8NV+m7zf0E2n" +
            "t/bRzRYMwKl7EaIqnXlNvccIl06j0XjdRadz7x8YcDOZTAGcRhuMEPJ2cnLiSBxYLBb6AQGA4omh6w6hp/CbKO+iosEkStJ7VR30" +
            "wYYeuktmzF4jfpI5Y8xPPj6ckpIXPOPxnp7ODs3evXvd2tvbnQHAumnTphsuLi7G1tZWwibvzMzMp/v6+uJpmh7poJF03wCGIxKW" +
            "gaEQuHA0nG7rk7aWt9MNViSP8hmVujzq0ff2fXz88rm2Fli6evWLTs667TzPewOASjEQxrBz505Ro9F0+Pr6XlSpVNksy55sbm7+" +
            "o91u1+Xm5g7tS/ifAkCUMTizNFglWX6zsA2ltfTROjf3M3NmhK7K+u+cspRDzV+vyTBMtl6v32M0Grf19/cr2AGA0IQxm82+AODL" +
            "MMwctVr9vru7e3lQUNC7VVVVRx1ekL9zACQI3dUMVN+wSCsKWulaK4VDH5741qXyyp0ZGRleN/qt22maZjHGZ3NzczMQQl0A8LtF" +
            "ixYVZGdnn+zo6HBDCMkkA5HNiogoithkMoHJZArhef4phNBRjPEtAB4ouZPkQfK6h5qBz5p7xOezDXQr0rbMi4mMqayq2brol4vC" +
            "09LSXo6Pj9/MMMzHGONJU6dOfX/ixImrpk+fHpGWllYYGRn5rF6vtxIKDc6pKKl4gwQVy7LY3d09k4xHRkbeosN9AyDpkaYAnDka" +
            "v1t6TfqPgquMzmvkqb0bNzz2adbpHEmWobai1rm2tla9dOlSc3Z29sWzZ89u4Xk+o6OjY3dXV9cuhmHg6NGjhYGBgf/JcdzN1iWI" +
            "aJVKZYuJiSklP0RFRf0v6xNBt6uFDhw4cBkBjLNLsswKFmpa3iFg7WYljYpYBp6hwCZheWVBK3W63QqTAvy3Ga60rLNabeDn5xcX" +
            "ERHxy7Fjx06lKIrv6uqqLi4uTi8uLv6AZdmBhx566NcqleqZ0aNHbzxx4kQNx3HY09Pz4pdffjllKB4QQhLGmPb09Czo7Ox83LFb" +
            "3zV/D96FMUpNTa07kJqK9+77Lyn1g9/jyoVT8OV5E3BF3CR8dWEwzosdL07UqzBSaweiH3/0JZoeTNWPPPLInk8//RRbrYQV30hF" +
            "RQWOj48vAoAR5L7nnntubHh4+P7ExEQlDh977LF4AoSElENRgcSDv7//BjI+ZcoU9tsUHjaFlJllDJ4aBs53msS5nzfSTZKq+aVn" +
            "ZkadyS/8WJIkCA4OXpuSkvJ6XFycoFKpiCUVIIQaQUFB9o8++mjaiy+++BkA8CdOnGhmGObKuXPnosn8CxcuPOXk5NTnsL5CH41G" +
            "A+7u7n9tamoKWb9+ve6+AZDZsCNYj9R3iS990cxIzu75f9ry2/C0z7IuLFmyhFSLIxcsWLBx2rRphKd0cXExvX37dpScnIzS0tIo" +
            "QRA4rVYrrFy58tGgoKAFCQkJrFqtPieKYgTxwvLly408z1c7liReQDzP15SWllbn5+f/G8dx8rfRnhqO8uQmEqy/Lf1KevPiV8wo" +
            "P7/jvd3XZ/zp88/Hx8bGPnLo0CGrv7//o3FxcRrySEtLC3X69GlISEiANWvWKPNkZWUpi4eFhcmBgYEx+/btE4KDg4s4jtNu3rxZ" +
            "RAgJoiiWOcpoiVBSq9UeEEXRxW63P8nzvLJh3CzD2Acw8Sm8lt+CTzb10hPHB+xsar6y+pWEhMXl5eWv8Tx/44UXXjA0NDSEZWVl" +
            "4fHjx6OamhqSMcDV1RVKSkoIvyEnJ4dMRrMsi9RqdVRMTMwfDAYD09PTM2327Nl7Cf+rqqqmt7e3E5uxWq1W9PDwOHTs2LF44t3o" +
            "6GjxngGQmRiKwj1WQf57t0CNe2jsvxuar/whLCxM4+/vX1BcXOyTnJy8LSwsDE6ePKnU8DRNIz8/P8XiERER0NjYCEuXLiXBDTqd" +
            "DoWGhsL06dNHLlu27NWBgQFISUmBFStWBJOU+vbbb8OOHTtEiqIYvV7/t0uXLnX19vauoGnaMnTAuXcPYIxVajX90vOx8w598pe/" +
            "jhs3/jMvL6+1a9asqWMYJqezsxOzLCtYrVbmrbfeovz9/SEkJATq6upg27Zt4ObmRhSH7Oxs5SLfJ0yYgJ2cnKTRo0fDmDFjKI1G" +
            "o/C7vb1dqf1JOe3t7b05PT19cn19/U8tFkutLJOzza3n4rvGgCRJjEarNb+xel29SqXOMxgMz5vN5oDExETK1dX1cm5u7hXicsLZa" +
            "9euQXV1tXLCiouLgyVLlsDTTz8NycnJwLKsorzVaoXCwkK0ePFiZtasWczu3bupjIwMYkimqKhICTm9Xp9XUlJSXFZWtk0phG9zoB" +
            "8WAPKwIAhUbGzsOaPR+LjFYpEMBsO8d955R75+/bopJycnpa6uDi1YsEBqamqClpYWIAEsiiL4+PgAsfLcuXOBUKevr08BQiys1+u" +
            "B4zi4cOECqFQqxWPkImM+np4r8/LyQmma/rnZbCbFEXPfAMhpaGBgQN3R3u4yWEFgqru7O27dunU+ZLisrCwlKSmpxGazsSNHjhRe" +
            "f/11aGtrg61bt8L+/fvh4MGD8OGHHyrxQHhO4oRcFEVBb28vPPnkk4qXkpKSSJDSPj7eRy+UV1ysqa19n3Dmrvrd7QaHF8jiyuZC" +
            "qkaTyaQ/fvz4Noqi5MTEROHIkSNxq1ataujp6WEJt1955RW8YcMG8PDwwATM/PnzYdeuXYqiJHCJxcmni4sLpKamQmlpqXzkSBpy" +
            "c3U1eYTNWVa1ael8VwZmmO2CQN0Fw7DLaVnGQxsJJcuy1NbWtjg8PDx78+bNhzHGbQihiKtXr36wbNmyuNraWjxixAiYNWsWIpb2" +
            "8vIiZTE88cQTkJGRAUajEcgum5WVpYCMjo6WOJZhx0+alHB+y5iBqq3H37d6BMqI1d7VA8MGwDKDxY4jlckWi0Wurq4+MHPmzH6E" +
            "UDpJIpmZmfMaGxtn19fXH/Pw8OBqamr6u7q6nFtbWxUAgYGB8NRTTwH5np6eTjIQjomJERsbG9nAgIDdBXnnP6nfCpnOkm2EFSMJK" +
            "aX1A3pAliTE8062sBkR6yqLC+ZYzeZIs8XKipJELImLi4uPTp48eXVJScnvCb1qamoyZ86c+XMS7ElJSdyePXvOzJs3j1haCWCyw" +
            "bm4uOCGhgZp6tSpTGVlJTtx3E+Ta+oNv7n86s926o3tz3XKrEgBZqSbDi/fJncvJRDCDMhs9quzy24YTVH2qEV+vgHj1nt7elbonH" +
            "hkNg+wFRUVv/P19S2KjIyMxRizZ86cOVdQUHDezc2tMSAgQPbx8SEtQlLcif39/fLatWtRSEgIY2io750wKXhxTWPzG/XLfrbLuf" +
            "/6b/qsggiIGjYz7nij4jxEgWyzoLpjn3xxef7Dl9w8Wg+6pu48wI75xbt87MKxftWFcy1dnc+ajH0zKyvKT3h7e/WMGjUyV+ekuy" +
            "hIEltUVESRSpXsD4UFBXA+Px96e3s6Ro0ee0CXbthSHtLEGF5eks4br8/psdolCqF7OuYO62YMgGw2O6glS5i1Swi7/MbG5MsvPP" +
            "zWw+PaUf2upD+rJsS/Zx8wqcZEPBve21AWwWD6CZPZ/CuLxcLt2LGjlqRiUcJXBUkqdAma8oVsq/l7X3cn5Kc+81pDT9cmrWhzuy" +
            "FIEoVoGvAtnZMHB0CEAgxWUZKtkk1GgBlOQpHWr8yRnWs3fVAdF1jGvxr9uc8M7Sk4lbYf+KgtOmdnbLHbAVEUMDQDdrORBuh3g3" +
            "deDm27yu4zm4zPU91feiKbAP0YSTQCRXmkXPJ3DQCBwGlI24rCiKYwYGwnAUZSEgZGg3GoaDWHtnxlfdOy/G0QEW3MWzClm0KyCX" +
            "EqwIIdVbwc5U1JoqczGlQQAw2dmJYQx5H+rqPzhhQAZK3hvhy6MwBEJpRBZDi4FP6Lb7rHg3+H2n2kCT7YeMYYUVimaQQ6hkK6b9" +
            "rySGkCCDIGGVGSUttgTKr0W1uGjjXImoonSCvzDq39OwGQ/+ECgSVWuXtGI6nPTtS4uQBzvPEhred/eEmBbzeZg0ZDXJLvGQDGWEt" +
            "aHaT2+W5ecNzjGytEKz0isnfYbDbtPQPgOK4WY2x1UOP7f8WEB3d8sjbHcV/3JX9wL/n+3wt6wPHvS/C/WoEf5Qcr/wOwKYlsiTy" +
            "NnAAAAABJRU5ErkJggg==";

        private Process _mirrorProcess;
        private bool _isRunning = false;
        private string _deviceName;
        private string _localIp;

        private Panel _headerPanel;
        private PictureBox _logoBox;
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

        private static Icon GetEmbeddedIcon()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(IconBase64);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (Bitmap bmp = new Bitmap(ms))
                    {
                        return Icon.FromHandle(bmp.GetHicon());
                    }
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private static Image GetEmbeddedImage()
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(IconBase64);
                MemoryStream ms = new MemoryStream(bytes);
                return Image.FromStream(ms);
            }
            catch
            {
                return null;
            }
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
            this.Size = new Size(500, 430);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = BgDark;
            this.ForeColor = TextPrimary;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Icon = GetEmbeddedIcon();

            // Cabecalho Escuro
            _headerPanel = new Panel();
            _headerPanel.Location = new Point(0, 0);
            _headerPanel.Size = new Size(500, 78);
            _headerPanel.BackColor = HeaderDark;

            // Logo com o novo icone do radar
            _logoBox = new PictureBox();
            _logoBox.Location = new Point(20, 15);
            _logoBox.Size = new Size(46, 46);
            _logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            _logoBox.Image = GetEmbeddedImage();

            _titleLabel = new Label();
            _titleLabel.Text = "LETMESEE";
            _titleLabel.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            _titleLabel.ForeColor = TextPrimary;
            _titleLabel.Location = new Point(74, 15);
            _titleLabel.AutoSize = true;

            _subtitleLabel = new Label();
            _subtitleLabel.Text = "Receptor de Espelhamento iOS via AirPlay";
            _subtitleLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _subtitleLabel.ForeColor = TextSecondary;
            _subtitleLabel.Location = new Point(76, 44);
            _subtitleLabel.AutoSize = true;

            _headerPanel.Controls.Add(_logoBox);
            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_subtitleLabel);

            // Status Visual
            _statusDot = new Label();
            _statusDot.Text = "●";
            _statusDot.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            _statusDot.ForeColor = StatusOff;
            _statusDot.Location = new Point(22, 92);
            _statusDot.Size = new Size(20, 25);

            _statusLabel = new Label();
            _statusLabel.Text = "Servico Desligado";
            _statusLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _statusLabel.ForeColor = TextSecondary;
            _statusLabel.Location = new Point(46, 94);
            _statusLabel.Size = new Size(350, 25);

            // Botao Liga/Desliga Principal (Laranja Tático)
            _toggleButton = new Button();
            _toggleButton.Text = "▶  LIGAR ESPELHAMENTO";
            _toggleButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _toggleButton.BackColor = TacticalOrange;
            _toggleButton.ForeColor = Color.White;
            _toggleButton.FlatStyle = FlatStyle.Flat;
            _toggleButton.FlatAppearance.BorderSize = 0;
            _toggleButton.Location = new Point(22, 128);
            _toggleButton.Size = new Size(440, 48);
            _toggleButton.Cursor = Cursors.Hand;
            _toggleButton.Click += ToggleService;

            // Card com Informacoes de Conexao
            _cardPanel = new Panel();
            _cardPanel.Location = new Point(22, 192);
            _cardPanel.Size = new Size(440, 140);
            _cardPanel.BackColor = CardBg;
            _cardPanel.Paint += (s, e) =>
            {
                // Borda do card e detalhe laranja a esquerda
                using (Pen borderPen = new Pen(CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(borderPen, 0, 0, _cardPanel.Width - 1, _cardPanel.Height - 1);
                }
                using (SolidBrush orangeBar = new SolidBrush(TacticalOrange))
                {
                    e.Graphics.FillRectangle(orangeBar, 0, 0, 4, _cardPanel.Height);
                }
            };

            _deviceTitleLabel = new Label();
            _deviceTitleLabel.Text = "NOME QUE APARECE NO IPHONE:";
            _deviceTitleLabel.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _deviceTitleLabel.ForeColor = TextSecondary;
            _deviceTitleLabel.Location = new Point(16, 12);
            _deviceTitleLabel.AutoSize = true;

            _deviceValueLabel = new Label();
            _deviceValueLabel.Text = _deviceName;
            _deviceValueLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _deviceValueLabel.ForeColor = TacticalOrange;
            _deviceValueLabel.Location = new Point(16, 30);
            _deviceValueLabel.AutoSize = true;

            _copyDeviceButton = new Button();
            _copyDeviceButton.Text = "Copiar";
            _copyDeviceButton.Font = new Font("Segoe UI", 8f, FontStyle.Regular);
            _copyDeviceButton.BackColor = Color.FromArgb(42, 46, 56);
            _copyDeviceButton.ForeColor = Color.White;
            _copyDeviceButton.FlatStyle = FlatStyle.Flat;
            _copyDeviceButton.FlatAppearance.BorderColor = CardBorder;
            _copyDeviceButton.Location = new Point(356, 28);
            _copyDeviceButton.Size = new Size(68, 26);
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
            _ipTitleLabel.ForeColor = TextSecondary;
            _ipTitleLabel.Location = new Point(16, 64);
            _ipTitleLabel.AutoSize = true;

            _ipValueLabel = new Label();
            _ipValueLabel.Text = _localIp;
            _ipValueLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            _ipValueLabel.ForeColor = TextPrimary;
            _ipValueLabel.Location = new Point(16, 82);
            _ipValueLabel.AutoSize = true;

            _instructionLabel = new Label();
            _instructionLabel.Text = "💡 No iPhone: Central de Controle > [Espelhar Tela] > Escolha o nome acima.";
            _instructionLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _instructionLabel.ForeColor = Color.FromArgb(180, 185, 195);
            _instructionLabel.Location = new Point(16, 110);
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
            _trayCheckBox.ForeColor = TextSecondary;
            _trayCheckBox.Font = new Font("Segoe UI", 8.5f);
            _trayCheckBox.Location = new Point(24, 345);
            _trayCheckBox.Size = new Size(240, 24);
            _trayCheckBox.Checked = true;

            _firewallButton = new Button();
            _firewallButton.Text = "Configurar Firewall";
            _firewallButton.Font = new Font("Segoe UI", 8.5f);
            _firewallButton.BackColor = Color.FromArgb(42, 46, 56);
            _firewallButton.ForeColor = TextPrimary;
            _firewallButton.FlatStyle = FlatStyle.Flat;
            _firewallButton.FlatAppearance.BorderColor = CardBorder;
            _firewallButton.Location = new Point(312, 343);
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
            _trayIcon.Icon = GetEmbeddedIcon();
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

            if (!File.Exists(uxplayExe))
            {
                string downloadScript = Path.Combine(appDir, "scripts\\download_deps.ps1");
                if (File.Exists(downloadScript))
                {
                    DialogResult dr = MessageBox.Show(
                        "Os arquivos do motor de espelhamento ainda nao foram baixados.\n\nDeseja realizar o download automatico agora?",
                        "LetMeSee - Dependencias",
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
                            MessageBox.Show("O download nao foi concluido. Verifique sua conexao com a internet.", "LetMeSee", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                _statusDot.ForeColor = SonarGreen; // Verde Radar
                _statusLabel.Text = "Ativo - Aguardando conexao do iPhone";
                _statusLabel.ForeColor = SonarGreen;

                _toggleButton.Text = "⏹  DESLIGAR ESPELHAMENTO";
                _toggleButton.BackColor = Color.FromArgb(42, 46, 56);
                _toggleButton.ForeColor = Color.FromArgb(235, 99, 56);

                _trayIcon.Text = "LetMeSee - Ativo (" + _deviceName + ")";
            }
            else
            {
                _statusDot.ForeColor = StatusOff;
                _statusLabel.Text = "Servico Desligado";
                _statusLabel.ForeColor = TextSecondary;

                _toggleButton.Text = "▶  LIGAR ESPELHAMENTO";
                _toggleButton.BackColor = TacticalOrange;
                _toggleButton.ForeColor = Color.White;

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
