using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace LetMeSee
{
    public enum DeviceFamily { IOS, Android }
    public enum HostPlatform { Windows, MacOS, Unsupported }
    public enum MirrorMethod { AirPlay, AndroidUsb, AndroidWireless, MacNativeAirPlay, MacQuickTime }
    public enum ExperienceLevel { Guided, Technical }
    public enum CheckStatus { Ready, ActionRequired, Blocked, Warning }
    public enum AndroidDeviceState { Device, Unauthorized, Offline, Unavailable }

    public sealed class PreparationSelection
    {
        public DeviceFamily Device { get; set; }
        public MirrorMethod Method { get; set; }
        public ExperienceLevel Experience { get; set; }
        public string AndroidSerial { get; set; }
        // Only an explicitly selected, fully manifested directory is accepted; PATH is never searched.
        public string AndroidRuntimeDirectory { get; set; }

        public PreparationSelection() { Device = DeviceFamily.IOS; Method = MirrorMethod.AirPlay; Experience = ExperienceLevel.Guided; }
        public PreparationSelection Clone() { return (PreparationSelection)MemberwiseClone(); }
        public bool Validate(out string error)
        {
            error = null;
            if (!Enum.IsDefined(typeof(DeviceFamily), Device) || !Enum.IsDefined(typeof(MirrorMethod), Method) || !Enum.IsDefined(typeof(ExperienceLevel), Experience))
                error = "Escolha um ambiente valido.";
            else if ((Device == DeviceFamily.Android) != (Method == MirrorMethod.AndroidUsb || Method == MirrorMethod.AndroidWireless))
                error = "O metodo escolhido nao pertence a este dispositivo.";
            else if (!String.IsNullOrEmpty(AndroidSerial) && !AndroidDevice.IsSafeSerial(AndroidSerial))
                error = "Selecione novamente o Android na lista de dispositivos.";
            else if (!String.IsNullOrEmpty(AndroidRuntimeDirectory))
            {
                try
                {
                    if (!Path.IsPathRooted(AndroidRuntimeDirectory) || AndroidRuntimeDirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        error = "Selecione uma pasta Android local por seu caminho completo.";
                }
                catch (ArgumentException) { error = "A pasta Android selecionada e invalida."; }
            }
            return error == null;
        }
    }

    public sealed class PreparationConfirmations
    {
        public bool TrustedNetwork { get; set; }
        public bool AirPlayReady { get; set; }
        public bool UsbDebuggingReady { get; set; }
        public bool WirelessPaired { get; set; }
        public bool PrivacyReady { get; set; }
        public bool FirewallReviewed { get; set; }
        public bool BonjourReviewed { get; set; }
        public PreparationConfirmations Clone() { return (PreparationConfirmations)MemberwiseClone(); }
    }

    public sealed class AndroidDevice
    {
        public string Serial { get; private set; }
        public string Model { get; private set; }
        public AndroidDeviceState State { get; private set; }
        public bool IsWireless { get; private set; }
        public bool IsEmulator { get { return Serial.StartsWith("emulator-", StringComparison.Ordinal); } }
        public AndroidDevice(string serial, string model, AndroidDeviceState state, bool wireless)
        {
            if (!IsSafeSerial(serial)) throw new ArgumentException("Serial Android invalido.", "serial");
            Serial = serial; Model = String.IsNullOrWhiteSpace(model) ? "Android" : model; State = state; IsWireless = wireless;
        }
        public override string ToString() { return Model.Replace('_', ' ') + " — " + Serial + " (" + State + ")"; }
        public static bool IsSafeSerial(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 255 || value[0] == '-') return false;
            foreach (char character in value)
                if (!(character >= 'a' && character <= 'z') && !(character >= 'A' && character <= 'Z') && !(character >= '0' && character <= '9') && "-_.:[]%".IndexOf(character) < 0) return false;
            return true;
        }
    }

    public sealed class PreparationSnapshot
    {
        public HostPlatform Host { get; set; }
        public bool AirPlayRuntimeReady { get; set; }
        public string AirPlayRuntimeMessage { get; set; }
        public bool AndroidRuntimeReady { get; set; }
        public string AndroidRuntimeMessage { get; set; }
        public bool HasNetwork { get; set; }
        public bool HasVpnOrTunnel { get; set; }
        public bool NetworkProfileKnown { get; set; }
        public bool HasPublicNetwork { get; set; }
        public bool HasDomainNetwork { get; set; }
        public bool FirewallStatusKnown { get; set; }
        public bool FirewallReady { get; set; }
        public bool BonjourStatusKnown { get; set; }
        public bool BonjourReady { get; set; }
        public bool AirPlayPortsKnown { get; set; }
        public bool AirPlayPortsAvailable { get; set; }
        public string ProbeMessage { get; set; }
        public IList<AndroidDevice> AndroidDevices { get; set; }
        public PreparationSnapshot() { AndroidDevices = new List<AndroidDevice>(); }
    }

    public sealed class PreparationCheck
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Detail { get; private set; }
        public CheckStatus Status { get; private set; }
        public PreparationCheck(string id, string title, CheckStatus status, string detail)
        { Id = id; Title = title; Status = status; Detail = detail; }
    }

    public sealed class PreparationReport
    {
        public IList<PreparationCheck> Checks { get; private set; }
        public string SelectedAndroidSerial { get; private set; }
        public bool CanStart
        {
            get
            {
                foreach (PreparationCheck check in Checks)
                    if (check.Status == CheckStatus.ActionRequired || check.Status == CheckStatus.Blocked) return false;
                return Checks.Count > 0;
            }
        }
        internal PreparationReport(List<PreparationCheck> checks, string serial)
        { Checks = new ReadOnlyCollection<PreparationCheck>(checks); SelectedAndroidSerial = serial; }
    }

    // Pure decision logic: user confirmations never replace evidence from a fresh probe.
    public static class PreparationChecklist
    {
        public static PreparationReport Evaluate(PreparationSelection selection, PreparationConfirmations confirmations, PreparationSnapshot snapshot)
        {
            if (selection == null || confirmations == null || snapshot == null) throw new ArgumentNullException("selection, confirmations or snapshot");
            List<PreparationCheck> checks = new List<PreparationCheck>();
            string error;
            if (!selection.Validate(out error))
            {
                Add(checks, "selection", "Ambiente", CheckStatus.Blocked, error);
                return new PreparationReport(checks, null);
            }
            bool nativeMac = selection.Method == MirrorMethod.MacNativeAirPlay || selection.Method == MirrorMethod.MacQuickTime;
            if (snapshot.Host != HostPlatform.Windows || nativeMac)
            {
                string message = nativeMac
                    ? "No Mac, use o guia nativo de AirPlay ou QuickTime. Este executavel .NET/WinForms inicia espelhamento apenas no Windows."
                    : "Este aplicativo requer Windows 10/11. No macOS, consulte o guia dos metodos nativos; nao execute o pacote Windows via emulacao.";
                Add(checks, "host", "Sistema compativel", CheckStatus.Blocked, message);
            }
            else Add(checks, "host", "Sistema compativel", CheckStatus.Ready, "Windows detectado. Nenhum privilegio de administrador e necessario para espelhar.");

            bool network = selection.Method == MirrorMethod.AirPlay || selection.Method == MirrorMethod.AndroidWireless || selection.Method == MirrorMethod.MacNativeAirPlay;
            if (network)
            {
                Add(checks, "network", "Conexao local", snapshot.HasNetwork ? CheckStatus.Ready : CheckStatus.Blocked,
                    snapshot.HasNetwork ? "Conexao de rede ativa detectada; o aplicativo nao consegue confirmar o Wi-Fi do celular nem o isolamento do roteador." : "Conecte computador e celular a mesma rede confiavel. Sem internet funciona; sem rede local, use Android por USB.");
                Confirm(checks, "trusted-network", "Mesma rede confiavel", confirmations.TrustedNetwork,
                    "Confirme celular e computador na mesma rede privada e sem isolamento de clientes. Em rede publica/corporativa, prefira cabo ou consulte o administrador; nao desative o firewall.");
                if (snapshot.HasPublicNetwork) Add(checks, "network-profile", "Rede publica ativa", CheckStatus.Blocked,
                    "Uma rede com perfil Publico esta ativa. Use uma rede Privada autorizada ou Android USB. O aplicativo nao troca o perfil nem abre o firewall em redes publicas.");
                if (snapshot.HasDomainNetwork) Add(checks, "managed-network", "Rede gerenciada ativa", selection.Method == MirrorMethod.AirPlay ? CheckStatus.Blocked : CheckStatus.Warning,
                    selection.Method == MirrorMethod.AirPlay
                    ? "A rede usa perfil Dominio. As regras Privadas do LetMeSee nao se aplicam aqui. Use uma rede Privada autorizada ou consulte TI; o aplicativo nao altera politicas da organizacao."
                    : "A rede e gerenciada pela organizacao. Use depuracao sem fio somente com permissao de TI; se nao for permitido ou a descoberta falhar, use USB.");
                else if (!snapshot.NetworkProfileKnown) Add(checks, "network-profile", "Perfil da rede nao confirmado", CheckStatus.Warning,
                    "Nao foi possivel consultar o perfil da rede. Confira manualmente que a rede e Privada e permitida pela sua organizacao antes de confirmar.");
                if (snapshot.HasVpnOrTunnel) Add(checks, "vpn", "VPN ou tunel detectado", CheckStatus.Warning, "A VPN pode impedir descoberta local. Ajuste o acesso a rede local apenas se a politica da sua organizacao permitir.");
            }
            string selectedSerial = null;
            if (selection.Device == DeviceFamily.IOS)
            {
                if (!nativeMac) Add(checks, "runtime", "Motor AirPlay verificado", snapshot.AirPlayRuntimeReady ? CheckStatus.Ready : CheckStatus.Blocked,
                    snapshot.AirPlayRuntimeMessage ?? "Instale o pacote AirPlay verificado antes de continuar.");
                if (!nativeMac)
                {
                    Add(checks, "bonjour", "Descoberta Bonjour", !snapshot.BonjourStatusKnown ? CheckStatus.Warning : snapshot.BonjourReady ? CheckStatus.Ready : CheckStatus.ActionRequired,
                        !snapshot.BonjourStatusKnown ? "Nao foi possivel consultar o Bonjour. Confira o servico Apple Bonjour no Windows."
                        : snapshot.BonjourReady ? "Servico Bonjour em execucao detectado. A descoberta ainda depende do roteador e do firewall."
                        : "Bonjour ausente ou parado. Obtenha o componente do publicador oficial e inicie seu servico; nao baixe instaladores de sites desconhecidos.");
                    Add(checks, "firewall", "Acesso local no firewall", !snapshot.FirewallStatusKnown ? CheckStatus.Warning : snapshot.FirewallReady ? CheckStatus.Ready : CheckStatus.ActionRequired,
                        !snapshot.FirewallStatusKnown ? "Nao foi possivel consultar as regras do firewall. Confira as regras locais Privadas ou solicite ajuda ao administrador."
                        : snapshot.FirewallReady ? "Regras LetMeSee ativas para o programa atual, perfil Privado e sub-rede local detectadas. Politicas da organizacao ainda podem bloquear o acesso."
                        : "Faltam regras locais Privadas para este pacote. Use a configuracao de firewall opcional do LetMeSee (solicita elevacao) ou consulte o administrador; nunca desative o firewall.");
                    if (!snapshot.BonjourStatusKnown) Confirm(checks, "bonjour-manual", "Bonjour conferido manualmente", confirmations.BonjourReviewed,
                        "A consulta automatica falhou. Confirme nos Servicos do Windows que o Apple Bonjour oficial esta instalado e em execucao, ou peca ajuda ao responsavel de TI.");
                    if (!snapshot.FirewallStatusKnown) Confirm(checks, "firewall-manual", "Firewall conferido manualmente", confirmations.FirewallReviewed,
                        "A consulta automatica falhou. Confirme com o responsavel de TI as regras do programa atual em rede Privada/sub-rede local. Nao desative a protecao para continuar.");
                    Add(checks, "ports", "Portas AirPlay disponiveis", !snapshot.AirPlayPortsKnown ? CheckStatus.Warning : snapshot.AirPlayPortsAvailable ? CheckStatus.Ready : CheckStatus.Blocked,
                        !snapshot.AirPlayPortsKnown ? "Nao foi possivel consultar as portas locais. A inicializacao ainda pode falhar por conflito."
                        : snapshot.AirPlayPortsAvailable ? "Nenhum conflito encontrado nas portas locais TCP 7000/7001/7100 e UDP 6000/6001/7011."
                        : "Outro programa ocupa uma porta AirPlay. Encerre somente sua outra sessao ou reinicie sua aplicacao; o LetMeSee nao encerra processos de terceiros.");
                }
                Confirm(checks, "airplay", "iPhone ou iPad preparado", confirmations.AirPlayReady,
                    "Desbloqueie o aparelho. Apos comecar, abra a Central de Controle > Espelhar a Tela e escolha o nome deste receptor. Digite o PIN exibido. Se o receptor nao aparecer, confira rede privada, Bonjour e regra local do firewall.");
            }
            else
            {
                Add(checks, "runtime", "Motor Android verificado", snapshot.AndroidRuntimeReady ? CheckStatus.Ready : CheckStatus.Blocked,
                    snapshot.AndroidRuntimeMessage ?? "Prepare scrcpy + adb oficiais com manifesto SHA-256 completo; o aplicativo nao baixa nem executa ferramentas do PATH.");
                if (selection.Method == MirrorMethod.AndroidUsb)
                    Confirm(checks, "usb-debugging", "Cabo e autorizacao do celular", confirmations.UsbDebuggingReady,
                        "Use cabo de dados, desbloqueie o Android, ative Depuracao USB manualmente e aceite a chave RSA deste computador. Nao exige root. Desative a depuracao e revogue esta autorizacao quando nao precisar mais.");
                else
                    Confirm(checks, "wireless-pairing", "Depuracao sem fio pareada", confirmations.WirelessPaired,
                        "Android 11+ exige Depuracao sem fio e pareamento oficial por codigo/QR (adb pair). O app usa somente dispositivos ja conectados e autorizados; nao abre a porta 5555. Se a descoberta falhar, use USB. Desative a depuracao sem fio ao terminar.");
                selectedSerial = EvaluateAndroidDevice(checks, selection, snapshot);
                Add(checks, "android-limitations", "Audio e conteudo protegido", CheckStatus.Warning,
                    "Alguns apps protegidos mostram tela preta. Audio depende da versao do Android e do app; Android 12+ tem suporte nativo do scrcpy, Android 11 pode exigir desbloqueio. Sem som, tente espelhar com audio desativado.");
            }
            Confirm(checks, "privacy", "Tela pronta para compartilhar", confirmations.PrivacyReady,
                "Ative Nao Perturbe/Foco, oculte notificacoes, feche senhas e dados pessoais e confirme que tem permissao para compartilhar esta tela. O app nao grava e Android inicia somente visualizacao, sem controle ou sincronizacao de clipboard.");
            if (!String.IsNullOrEmpty(snapshot.ProbeMessage)) Add(checks, "probe", "Verificacao local", CheckStatus.Warning, snapshot.ProbeMessage);
            return new PreparationReport(checks, selectedSerial);
        }

        private static string EvaluateAndroidDevice(List<PreparationCheck> checks, PreparationSelection selection, PreparationSnapshot snapshot)
        {
            List<AndroidDevice> candidates = new List<AndroidDevice>();
            bool wireless = selection.Method == MirrorMethod.AndroidWireless;
            foreach (AndroidDevice device in snapshot.AndroidDevices ?? new List<AndroidDevice>())
                if (device.IsWireless == wireless && !device.IsEmulator) candidates.Add(device);
            AndroidDevice selected = null;
            if (!String.IsNullOrEmpty(selection.AndroidSerial))
            {
                foreach (AndroidDevice device in candidates) if (device.Serial == selection.AndroidSerial) selected = device;
                if (selected == null)
                {
                    Add(checks, "device", "Android selecionado", CheckStatus.Blocked, "O Android selecionado foi desconectado ou usa outro metodo. Reconecte e selecione novamente; nao escolheremos outro aparelho automaticamente.");
                    return null;
                }
            }
            else if (candidates.Count == 1) selected = candidates[0];
            else if (candidates.Count > 1)
            {
                Add(checks, "device", "Escolha o Android correto", CheckStatus.ActionRequired, "Mais de um Android foi encontrado. Selecione explicitamente o aparelho por modelo e serial no checklist.");
                return null;
            }
            if (selected == null)
            {
                Add(checks, "device", "Android conectado", CheckStatus.Blocked, wireless
                    ? "Nenhum Android sem fio autorizado foi encontrado. Conclua o pareamento oficial e mantenha a tela desbloqueada. Rede de convidados, VPN ou isolamento podem impedir descoberta; use USB como alternativa."
                    : "Nenhum Android USB foi encontrado. Use cabo de dados, outra porta e confirme a depuracao USB. No Windows, alguns fabricantes exigem seu driver oficial; o app nao instala drivers automaticamente.");
                return null;
            }
            CheckStatus status = selected.State == AndroidDeviceState.Device ? CheckStatus.Ready : CheckStatus.Blocked;
            string detail = selected.State == AndroidDeviceState.Device ? selected.ToString() + ". A autorizacao sera verificada novamente antes de iniciar."
                : selected.State == AndroidDeviceState.Unauthorized ? "Desbloqueie o celular e aceite a chave RSA exibida nele. O aplicativo nao contorna esta autorizacao."
                : selected.State == AndroidDeviceState.Offline ? "Android offline. Reconecte o cabo ou o Wi-Fi e verifique novamente. O app nao reinicia o servidor ADB de outros programas."
                : "Android indisponivel para depuracao. Desbloqueie o aparelho e confira as politicas do dispositivo.";
            Add(checks, "device", "Android autorizado", status, detail);
            return status == CheckStatus.Ready ? selected.Serial : null;
        }
        private static void Confirm(List<PreparationCheck> checks, string id, string title, bool confirmed, string detail)
        { Add(checks, id, title, confirmed ? CheckStatus.Ready : CheckStatus.ActionRequired, detail); }
        private static void Add(List<PreparationCheck> checks, string id, string title, CheckStatus status, string detail)
        { checks.Add(new PreparationCheck(id, title, status, detail)); }
    }
}
