import Foundation

enum DeviceKind: String, Codable, CaseIterable, Identifiable {
    case android, iPhone
    var id: String { rawValue }
    var label: String { self == .android ? "Android" : "iPhone / iPad" }
}

enum ConnectionChoice: String, Codable, CaseIterable, Identifiable {
    case automatic, usb, wifi
    var id: String { rawValue }
    var label: String {
        switch self { case .automatic: return "Recomendado"; case .usb: return "Cabo USB"; case .wifi: return "Sem fio" }
    }
}

enum Experience: String, Codable, CaseIterable, Identifiable {
    case guided, technical
    var id: String { rawValue }
    var label: String { self == .guided ? "Passo a passo" : "Técnico" }
}

enum MirrorMethod: String, Codable {
    case androidUSB, androidWiFi, airPlay, quickTime
    var title: String {
        switch self {
        case .androidUSB: return "Android · USB"
        case .androidWiFi: return "Android · Wi-Fi pareado"
        case .airPlay: return "iPhone / iPad · AirPlay do Mac"
        case .quickTime: return "iPhone / iPad · USB / QuickTime"
        }
    }
    var isAndroid: Bool { self == .androidUSB || self == .androidWiFi }
    var explanation: String {
        switch self {
        case .androidUSB: return "Recomendado para Android: cabo de dados e autorização de depuração. O scrcpy mostra a tela sem controlar o celular."
        case .androidWiFi: return "Android 11 ou posterior, já pareado pela depuração sem fio. O app usa apenas uma conexão que já existe; não abre a porta 5555."
        case .airPlay: return "O receptor é gerenciado pelo macOS em Macs compatíveis. No iPhone, abra Central de Controle → Espelhar a Tela e escolha este Mac."
        case .quickTime: return "O QuickTime permite uma prévia pelo cabo. Abra Arquivo → Nova Gravação de Filme e escolha seu iPhone no menu ao lado de Gravar. Não é preciso gravar."
        }
    }
}

struct ToolApproval: Codable, Equatable {
    var path: String
    var sha256: String
}

struct Configuration: Codable, Equatable {
    var device: DeviceKind = .android
    var connection: ConnectionChoice = .automatic
    var experience: Experience = .guided
    var includeAudio = false
    var adbApproval: ToolApproval?
    var scrcpyApproval: ToolApproval?
    var serverApproval: ToolApproval?
    var method: MirrorMethod {
        if device == .iPhone { return connection == .usb ? .quickTime : .airPlay }
        return connection == .wifi ? .androidWiFi : .androidUSB
    }
}

enum DeviceState: String { case ready, unauthorized, offline, unknown }

struct AndroidDevice: Equatable, Identifiable {
    let serial: String
    let state: DeviceState
    let model: String
    var id: String { serial }
    var isWireless: Bool { serial.contains(":") || serial.contains("._adb-tls-") }
    var label: String { "\(model.isEmpty ? "Android" : model) · \(serial) · \(stateLabel)" }
    var stateLabel: String {
        switch state { case .ready: return "autorizado"; case .unauthorized: return "autorize no celular"; case .offline: return "offline"; case .unknown: return "indisponível" }
    }
}

enum AndroidParser {
    static func devices(_ output: String) -> [AndroidDevice] {
        let parsed: [AndroidDevice] = output.split(whereSeparator: \.isNewline).compactMap { rawLine in
            let fields = rawLine.split(whereSeparator: \.isWhitespace).map(String.init)
            guard fields.count >= 2, fields[0] != "List", !fields[0].hasPrefix("*"),
                  ["device", "unauthorized", "offline", "no"].contains(fields[1]),
                  validSerial(fields[0]) else { return nil }
            let state: DeviceState = fields[1] == "device" ? .ready : fields[1] == "unauthorized" ? .unauthorized : fields[1] == "offline" ? .offline : .unknown
            let model = fields.first(where: { $0.hasPrefix("model:") }).map { String($0.dropFirst(6)).replacingOccurrences(of: "_", with: " ") } ?? "Android"
            return AndroidDevice(serial: fields[0], state: state, model: model)
        }
        let counts = Dictionary(grouping: parsed, by: \.serial)
        return parsed.filter { counts[$0.serial]?.count == 1 && !$0.serial.hasPrefix("emulator-") }
    }
    static func validSerial(_ serial: String) -> Bool {
        !serial.isEmpty && serial.utf8.count <= 256 && !serial.hasPrefix("-") &&
        serial.unicodeScalars.allSatisfy { CharacterSet(charactersIn: "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._:-").contains($0) }
    }
}

struct ReadinessFacts {
    var toolsVerified = false
    var toolDetails = "Verifique as ferramentas locais."
    var devices: [AndroidDevice] = []
    var selectedSerial: String?
    var quickTimeAvailable = false
    var diagnosticsRunning = false
    mutating func selectDevice(_ serial: String?, confirmations: inout Set<String>) {
        guard selectedSerial != serial else { return }
        selectedSerial = serial
        confirmations.removeAll()
    }
}

struct ChecklistItem: Identifiable {
    let id: String
    let title: String
    let detail: String
    let passed: Bool
    let manual: Bool
}

enum ChecklistEngine {
    static func items(configuration: Configuration, facts: ReadinessFacts, confirmations: Set<String>) -> [ChecklistItem] {
        func manual(_ id: String, _ title: String, _ detail: String) -> ChecklistItem {
            ChecklistItem(id: id, title: title, detail: detail, passed: confirmations.contains(id), manual: true)
        }
        var result = [manual("privacy", "Prepare sua privacidade", "Feche conteúdo sensível, ative Não Perturbe e avise quem puder aparecer na tela. Conteúdo protegido pode ficar preto.")]
        switch configuration.method {
        case .androidUSB, .androidWiFi:
            result.append(ChecklistItem(id: "tools", title: "Ferramentas locais conferidas", detail: facts.toolDetails, passed: facts.toolsVerified && !facts.diagnosticsRunning, manual: false))
            if configuration.method == .androidUSB {
                result.append(manual("usbDebug", "Cabo e autorização do Android", "Use um cabo de dados, desbloqueie o Android, ative Depuração USB e aceite a chave RSA apenas neste computador de confiança."))
            } else {
                result.append(manual("wirelessPaired", "Android 11+ já pareado", "Faça o pareamento de depuração sem fio por um meio de confiança antes de usar. Este app não pareia nem habilita depuração pela rede."))
                result.append(manual("privateNetwork", "Mesma rede privada", "Confirme que os aparelhos estão na mesma rede de confiança. Redes de hóspedes, VPN e isolamento de clientes podem impedir a conexão."))
            }
            let selected = facts.devices.first(where: { $0.serial == facts.selectedSerial })
            let correctTransport = selected.map { $0.isWireless == (configuration.method == .androidWiFi) } ?? false
            let detail: String
            if let device = selected {
                detail = correctTransport ? device.label : "O dispositivo selecionado usa outro tipo de conexão. Escolha o transporte correto ou mude o ambiente."
            } else if facts.devices.isEmpty { detail = "Nenhum Android detectado. Conecte o aparelho, desbloqueie-o e verifique novamente." }
            else { detail = "Escolha explicitamente um dispositivo na lista. Nunca selecionamos o primeiro automaticamente." }
            result.append(ChecklistItem(id: "device", title: "Dispositivo escolhido e autorizado", detail: detail, passed: selected?.state == .ready && correctTransport && !facts.diagnosticsRunning, manual: false))
        case .airPlay:
            result.append(manual("airPlayReceiver", "Mac compatível e Receptor AirPlay habilitado", "Confira Ajustes do Sistema → Geral → AirDrop e Continuidade (ou AirDrop e Handoff). A versão do macOS, sozinha, não comprova suporte."))
            result.append(manual("privateNetwork", "Mesma rede privada e acesso permitido", "Confirme Wi-Fi e regras de acesso do Receptor AirPlay. Prefira acesso restrito à sua conta; não amplie para todos apenas para contornar uma falha."))
            result.append(manual("nativeHandoff", "Entendi a etapa no iPhone", "O LetMeSee orienta a conexão. O macOS recebe a imagem e o iPhone inicia e encerra o AirPlay; este app não detecta a sessão nem gera código."))
        case .quickTime:
            result.append(ChecklistItem(id: "quickTime", title: "QuickTime disponível", detail: facts.quickTimeAvailable ? "QuickTime Player encontrado no macOS." : "QuickTime Player não encontrado. Confira a instalação do macOS.", passed: facts.quickTimeAvailable, manual: false))
            result.append(manual("trustedCable", "Cabo de dados e iPhone autorizado", "Conecte o iPhone/iPad com cabo de dados, desbloqueie-o e confirme Confiar neste computador quando solicitado."))
            result.append(manual("nativeHandoff", "Entendi a seleção no QuickTime", "Escolha Arquivo → Nova Gravação de Filme e seu iPhone como câmera. A prévia não exige gravar. A seleção e o encerramento acontecem no QuickTime."))
        }
        return result
    }
    static func ready(configuration: Configuration, facts: ReadinessFacts, confirmations: Set<String>) -> Bool {
        !facts.diagnosticsRunning && items(configuration: configuration, facts: facts, confirmations: confirmations).allSatisfy(\.passed)
    }
}

enum ScrcpyArguments {
    static func make(serial: String, includeAudio: Bool) -> [String]? {
        guard AndroidParser.validSerial(serial) else { return nil }
        var arguments = ["--serial", serial, "--no-control", "--no-clipboard-autosync"]
        if !includeAudio { arguments.append("--no-audio") }
        return arguments
    }
}

enum PreparationLifetime {
    static func isFresh(verifiedAt: TimeInterval, now: TimeInterval) -> Bool {
        let elapsed = now - verifiedAt
        return elapsed >= 0 && elapsed < 300
    }
}
