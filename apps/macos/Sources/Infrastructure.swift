import AppKit
import CryptoKit
import Darwin
import Foundation
import Network

struct ToolIdentity: Equatable {
    let path: String
    let sha256: String
    var approval: ToolApproval { ToolApproval(path: path, sha256: sha256) }
    static func inspect(_ rawPath: String, requireExecutable: Bool = true) throws -> ToolIdentity {
        let url = URL(fileURLWithPath: rawPath).resolvingSymlinksInPath().standardizedFileURL
        let attributes = try FileManager.default.attributesOfItem(atPath: url.path)
        guard url.path.hasPrefix("/"), attributes[.type] as? FileAttributeType == .typeRegular,
              (!requireExecutable || FileManager.default.isExecutableFile(atPath: url.path)),
              (attributes[.size] as? NSNumber)?.int64Value ?? Int64.max < 200_000_000 else {
            throw LocalError.message("O arquivo precisa ser um executável local regular de até 200 MB.")
        }
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = SHA256()
        while let data = try handle.read(upToCount: 65_536), !data.isEmpty { hasher.update(data: data) }
        return ToolIdentity(path: url.path, sha256: hasher.finalize().map { String(format: "%02x", $0) }.joined())
    }
    static func discover(_ name: String) -> ToolIdentity? {
        for prefix in ["/opt/homebrew/bin", "/usr/local/bin"] {
            if let identity = try? inspect("\(prefix)/\(name)") { return identity }
        }
        return nil
    }
    func matches(_ approval: ToolApproval?) -> Bool { approval == self.approval }
}

enum SafeProcessEnvironment {
    static var base: [String: String] {
        ["HOME": FileManager.default.homeDirectoryForCurrentUser.path,
         "TMPDIR": FileManager.default.temporaryDirectory.path,
         "PATH": "/usr/bin:/bin:/usr/sbin:/sbin", "LANG": "en_US.UTF-8"]
    }
}

enum LocalError: LocalizedError {
    case message(String)
    var errorDescription: String? { switch self { case .message(let text): return text } }
}

enum OwnedProcess {
    /// Only terminate processes held by this app; never kill a name or a previously released PID.
    static func stop(_ process: Process) {
        guard process.isRunning else { return }
        process.terminate()
        let deadline = Date().addingTimeInterval(0.3)
        while process.isRunning && Date() < deadline { usleep(10_000) }
        if process.isRunning { Darwin.kill(process.processIdentifier, SIGKILL) }
    }
}

struct CommandResult { let code: Int32; let text: String }

enum CommandRunner {
    static func run(path: String, arguments: [String], environment: [String: String]? = nil, seconds: Double = 6) async throws -> CommandResult {
        try await Task.detached(priority: .utility) {
            let url = FileManager.default.temporaryDirectory.appendingPathComponent("letmesee-command-\(UUID().uuidString).log")
            guard FileManager.default.createFile(atPath: url.path, contents: nil, attributes: [.posixPermissions: 0o600]) else { throw LocalError.message("Não foi possível criar o diagnóstico temporário.") }
            defer { try? FileManager.default.removeItem(at: url) }
            let handle = try FileHandle(forWritingTo: url)
            defer { try? handle.close() }
            let process = Process()
            process.executableURL = URL(fileURLWithPath: path)
            process.arguments = arguments
            process.environment = environment ?? SafeProcessEnvironment.base
            process.standardOutput = handle
            process.standardError = handle
            process.standardInput = FileHandle.nullDevice
            try process.run()
            let deadline = Date().addingTimeInterval(seconds)
            while process.isRunning && Date() < deadline { usleep(20_000) }
            if process.isRunning {
                process.terminate()
                usleep(100_000)
                if process.isRunning { Darwin.kill(process.processIdentifier, SIGKILL) }
                process.waitUntilExit()
                throw LocalError.message("O diagnóstico excedeu o tempo limite. Verifique o cabo e as ferramentas.")
            }
            process.waitUntilExit()
            let readHandle = try FileHandle(forReadingFrom: url)
            defer { try? readHandle.close() }
            let data = try readHandle.read(upToCount: 65_536) ?? Data()
            return CommandResult(code: process.terminationStatus, text: String(decoding: data, as: UTF8.self))
        }.value
    }
}

/// This app owns this foreground server. It never starts, replaces or stops the global adb server.
@MainActor final class PrivateADB {
    private var process: Process?
    private(set) var port: UInt16 = 0
    var running: Bool { process?.isRunning == true }
    var socket: String { "tcp:127.0.0.1:\(port)" }
    var clientArguments: [String] { ["-H", "127.0.0.1", "-P", String(port)] }
    func start(path: String) async throws {
        if running { return }
        stop()
        let fd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard fd >= 0 else { throw LocalError.message("Não foi possível reservar a conexão local do Android.") }
        var address = sockaddr_in()
        address.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = 0
        address.sin_addr.s_addr = inet_addr("127.0.0.1")
        let bindResult = withUnsafePointer(to: &address) { pointer in pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_in>.size)) } }
        guard bindResult == 0 else { Darwin.close(fd); throw LocalError.message("Não foi possível reservar uma porta local.") }
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        let nameResult = withUnsafeMutablePointer(to: &address) { pointer in pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { getsockname(fd, $0, &length) } }
        Darwin.close(fd)
        guard nameResult == 0 else { throw LocalError.message("Não foi possível ler a porta local.") }
        port = UInt16(bigEndian: address.sin_port)
        let server = Process()
        server.executableURL = URL(fileURLWithPath: path)
        server.arguments = ["-L", socket, "server", "nodaemon"]
        server.environment = SafeProcessEnvironment.base
        server.standardInput = FileHandle.nullDevice
        server.standardOutput = FileHandle.nullDevice
        server.standardError = FileHandle.nullDevice
        try server.run()
        process = server
        try await Task.sleep(nanoseconds: 300_000_000)
        guard server.isRunning else { process = nil; throw LocalError.message("O servidor Android privado não iniciou. Outro programa pode estar usando o aparelho. Feche esse programa e tente novamente.") }
    }
    func stop() {
        guard let server = process else { return }
        process = nil
        OwnedProcess.stop(server)
        port = 0
    }
}

enum SettingsStore {
    static var url: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        return base.appendingPathComponent("LetMeSee", isDirectory: true).appendingPathComponent("settings.json")
    }
    static func load() -> Configuration {
        guard let data = try? Data(contentsOf: url), data.count < 64_000,
              let configuration = try? JSONDecoder().decode(Configuration.self, from: data) else { return Configuration() }
        return configuration
    }
    static func save(_ configuration: Configuration) throws {
        let directory = url.deletingLastPathComponent()
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        let data = try JSONEncoder().encode(configuration)
        try data.write(to: url, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
    }
}

@MainActor final class AppModel: ObservableObject {
    @Published var configuration = SettingsStore.load() {
        didSet { if oldValue != configuration { invalidate(); adb.stop() } }
    }
    @Published var facts = ReadinessFacts()
    @Published var confirmations: Set<String> = []
    @Published var adbIdentity: ToolIdentity?
    @Published var scrcpyIdentity: ToolIdentity?
    @Published var serverIdentity: ToolIdentity?
    @Published var showChecklist = true
    @Published var preparing = false
    @Published var message = "Confira seu ambiente antes de começar."
    @Published var activity: Activity = .idle
    @Published var settingsError: String?
    private let adb = PrivateADB()
    private var scrcpyProcess: Process?
    private var diagnosticGeneration = UUID()
    private var disposed = false
    private var verifiedAt: TimeInterval?
    private var observers: [NSObjectProtocol] = []
    private let pathMonitor = NWPathMonitor()
    private var observedFirstPath = false

    init() {
        for name in [NSWorkspace.willSleepNotification, NSWorkspace.didWakeNotification] {
            let token = NSWorkspace.shared.notificationCenter.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                Task { @MainActor [weak self] in self?.systemChanged("O Mac suspendeu ou retomou. Confira o ambiente novamente.") }
            }
            observers.append(token)
        }
        pathMonitor.pathUpdateHandler = { [weak self] _ in
            Task { @MainActor [weak self] in
                guard let self else { return }
                if !self.observedFirstPath { self.observedFirstPath = true; return }
                if self.configuration.method == .airPlay || self.configuration.method == .androidWiFi {
                    self.systemChanged("A conexão de rede mudou. Confira a rede e o checklist novamente.")
                }
            }
        }
        pathMonitor.start(queue: DispatchQueue(label: "com.letmesee.network-observer"))
    }

    enum Activity { case idle, android, airPlayGuidance, quickTimeGuidance }
    var active: Bool { activity != .idle }
    var items: [ChecklistItem] { ChecklistEngine.items(configuration: configuration, facts: facts, confirmations: confirmations) }
    var ready: Bool {
        guard let verifiedAt, PreparationLifetime.isFresh(verifiedAt: verifiedAt, now: ProcessInfo.processInfo.systemUptime) else { return false }
        return ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: confirmations)
    }
    var completed: Int { items.filter(\.passed).count }
    var primaryTitle: String {
        switch activity { case .idle: return "Começar a espelhar"; case .android: return "Parar espelhamento"; case .airPlayGuidance, .quickTimeGuidance: return "Concluir orientação" }
    }
    var statusTitle: String {
        switch activity { case .idle: return ready ? "Ambiente preparado" : "Preparação necessária"; case .android: return "Janela do espelhamento aberta"; case .airPlayGuidance: return "Continue no iPhone"; case .quickTimeGuidance: return "Continue no QuickTime" }
    }
    func invalidate() {
        diagnosticGeneration = UUID()
        facts = ReadinessFacts()
        confirmations = []
        verifiedAt = nil
        message = "O ambiente mudou. Confira o checklist novamente."
    }
    func editEnvironment() { guard !active else { return }; showChecklist = true }
    func selectDevice(_ serial: String?) {
        guard !active, !preparing else { return }
        facts.selectDevice(serial, confirmations: &confirmations)
    }
    func discoverTools() {
        adbIdentity = configuration.adbApproval.flatMap { try? ToolIdentity.inspect($0.path) } ?? ToolIdentity.discover("adb")
        scrcpyIdentity = configuration.scrcpyApproval.flatMap { try? ToolIdentity.inspect($0.path) } ?? ToolIdentity.discover("scrcpy")
        serverIdentity = configuration.serverApproval.flatMap { try? ToolIdentity.inspect($0.path, requireExecutable: false) }
        if serverIdentity == nil, let scrcpyIdentity {
            let root = URL(fileURLWithPath: scrcpyIdentity.path).deletingLastPathComponent().deletingLastPathComponent()
            for path in [root.appendingPathComponent("share/scrcpy/scrcpy-server").path, "/opt/homebrew/share/scrcpy/scrcpy-server", "/usr/local/share/scrcpy/scrcpy-server"] {
                if let identity = try? ToolIdentity.inspect(path, requireExecutable: false) { serverIdentity = identity; break }
            }
        }
    }
    var toolsApproved: Bool { adbIdentity?.matches(configuration.adbApproval) == true && scrcpyIdentity?.matches(configuration.scrcpyApproval) == true && serverIdentity?.matches(configuration.serverApproval) == true }
    func approveTools() {
        guard let adbIdentity, let scrcpyIdentity, let serverIdentity else { return }
        configuration.adbApproval = adbIdentity.approval
        configuration.scrcpyApproval = scrcpyIdentity.approval
        configuration.serverApproval = serverIdentity.approval
        Task { await refresh() }
    }
    func chooseTool(_ name: String) {
        let panel = NSOpenPanel()
        panel.title = "Selecionar \(name) instalado"
        panel.message = "Escolha o executável de uma instalação de confiança. Nenhum arquivo será baixado."
        panel.canChooseDirectories = false
        panel.allowsMultipleSelection = false
        if panel.runModal() == .OK, let url = panel.url {
            do { let identity = try ToolIdentity.inspect(url.path, requireExecutable: name != "scrcpy-server"); if name == "adb" { adbIdentity = identity } else if name == "scrcpy" { scrcpyIdentity = identity } else { serverIdentity = identity }; invalidate() }
            catch { message = error.localizedDescription }
        }
    }
    func refresh() async {
        guard !active, !disposed else { return }
        confirmations = []
        let generation = UUID()
        diagnosticGeneration = generation
        facts = ReadinessFacts()
        facts.diagnosticsRunning = true
        defer { if diagnosticGeneration == generation { facts.diagnosticsRunning = false; verifiedAt = ProcessInfo.processInfo.systemUptime } }
        facts.quickTimeAvailable = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.QuickTimePlayerX") != nil
        guard configuration.method.isAndroid else { return }
        discoverTools()
        guard toolsApproved, let adbIdentity, let scrcpyIdentity, let serverIdentity else {
            facts.toolDetails = "Instale adb e scrcpy por uma fonte de confiança e aprove os arquivos locais abaixo. O LetMeSee não instala ferramentas."
            return
        }
        do {
            let freshADB = try ToolIdentity.inspect(adbIdentity.path)
            let freshScrcpy = try ToolIdentity.inspect(scrcpyIdentity.path)
            let freshServer = try ToolIdentity.inspect(serverIdentity.path, requireExecutable: false)
            guard freshADB == adbIdentity, freshScrcpy == scrcpyIdentity, freshServer == serverIdentity else { throw LocalError.message("Uma ferramenta mudou. Confira e aprove o novo SHA-256.") }
            let adbVersion = try await CommandRunner.run(path: adbIdentity.path, arguments: ["version"])
            let scrcpyVersion = try await CommandRunner.run(path: scrcpyIdentity.path, arguments: ["--version"])
            guard diagnosticGeneration == generation else { return }
            guard adbVersion.code == 0, adbVersion.text.contains("Android Debug Bridge version"),
                  scrcpyVersion.code == 0, scrcpyVersion.text.range(of: #"scrcpy [2-9][0-9]*\."#, options: .regularExpression) != nil else {
                throw LocalError.message("Precisamos de Android Platform Tools atual e scrcpy 2 ou posterior. Confira as versões de sua instalação.")
            }
            try await adb.start(path: adbIdentity.path)
            guard diagnosticGeneration == generation else { adb.stop(); return }
            let devices = try await readDevices(path: adbIdentity.path)
            guard diagnosticGeneration == generation else { return }
            facts.devices = devices
            facts.toolsVerified = true
            facts.toolDetails = "SHA-256 aprovado e versões verificadas. ADB usa um servidor privado em loopback; o controle do celular fica desativado."
        } catch {
            guard diagnosticGeneration == generation else { return }
            facts.toolsVerified = false
            facts.toolDetails = error.localizedDescription
        }
    }
    private func readDevices(path: String) async throws -> [AndroidDevice] {
        guard adb.running else { throw LocalError.message("A conexão Android privada foi encerrada. Verifique novamente.") }
        let result = try await CommandRunner.run(path: path, arguments: adb.clientArguments + ["devices", "-l"])
        guard result.code == 0, adb.running else { throw LocalError.message("Não foi possível consultar o Android pelo servidor privado.") }
        return AndroidParser.devices(result.text)
    }
    func saveChecklist() {
        guard ready else { return }
        do { try SettingsStore.save(configuration); settingsError = nil; showChecklist = false; message = "Tudo conferido para este ambiente. O início ainda depende da etapa indicada no celular." }
        catch { settingsError = "Não foi possível salvar suas preferências: \(error.localizedDescription)" }
    }
    func primaryAction() {
        if active { stop(); return }
        Task { await start() }
    }
    private func start() async {
        guard ready, !preparing, !disposed else { showChecklist = true; return }
        preparing = true
        defer { preparing = false }
        do {
            if configuration.method.isAndroid {
                guard let approvedADB = configuration.adbApproval, let approvedScrcpy = configuration.scrcpyApproval, let approvedServer = configuration.serverApproval,
                      try ToolIdentity.inspect(approvedADB.path).approval == approvedADB,
                      try ToolIdentity.inspect(approvedScrcpy.path).approval == approvedScrcpy,
                      try ToolIdentity.inspect(approvedServer.path, requireExecutable: false).approval == approvedServer else { throw LocalError.message("As ferramentas mudaram desde a conferência. Revise o SHA-256 antes de iniciar.") }
                let devices = try await readDevices(path: approvedADB.path)
                let selected = facts.selectedSerial
                facts.devices = devices
                guard ready, let serial = selected, let arguments = ScrcpyArguments.make(serial: serial, includeAudio: configuration.includeAudio) else { throw LocalError.message("O aparelho mudou de estado. Desbloqueie-o, autorize a conexão e confira o checklist.") }
                let process = Process()
                process.executableURL = URL(fileURLWithPath: approvedScrcpy.path)
                process.arguments = arguments
                var environment = SafeProcessEnvironment.base
                environment["ADB"] = approvedADB.path
                environment["ADB_SERVER_SOCKET"] = adb.socket
                environment["SCRCPY_SERVER_PATH"] = approvedServer.path
                process.environment = environment
                process.standardOutput = FileHandle.nullDevice
                process.standardError = FileHandle.nullDevice
                process.standardInput = FileHandle.nullDevice
                process.terminationHandler = { [weak self] terminated in
                    Task { @MainActor [weak self] in
                        guard let self, self.scrcpyProcess === terminated else { return }
                        self.scrcpyProcess = nil
                        self.activity = .idle
                        self.confirmations = []
                        self.message = terminated.terminationStatus == 0 ? "A janela foi fechada. Confira o ambiente antes de outra sessão." : "O scrcpy foi encerrado. Verifique autorização, cabo e compatibilidade no checklist."
                    }
                }
                try process.run()
                scrcpyProcess = process
                activity = .android
                message = "O processo do scrcpy está aberto. A imagem aparece em sua própria janela; este status não confirma quadros recebidos. Para encerrar, use Parar espelhamento."
            } else if configuration.method == .airPlay {
                activity = .airPlayGuidance
                message = "No iPhone: Central de Controle → Espelhar a Tela → escolha este Mac. Para parar, volte a Espelhar a Tela → Parar Espelhamento no iPhone. O macOS controla a sessão; o LetMeSee não confirma conexão."
            } else {
                guard let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.apple.QuickTimePlayerX") else { throw LocalError.message("O QuickTime não está disponível. Confira o checklist.") }
                NSWorkspace.shared.openApplication(at: url, configuration: NSWorkspace.OpenConfiguration()) { [weak self] _, error in
                    if let error { Task { @MainActor [weak self] in self?.activity = .idle; self?.confirmations = []; self?.message = "Não foi possível abrir o QuickTime: \(error.localizedDescription)" } }
                }
                activity = .quickTimeGuidance
                message = "No QuickTime: Arquivo → Nova Gravação de Filme → menu ao lado de Gravar → escolha seu iPhone. A prévia dispensa gravação. Feche a prévia no QuickTime para parar; este app não detecta a sessão."
            }
        } catch {
            confirmations = []
            facts.toolsVerified = false
            message = error.localizedDescription
            showChecklist = true
        }
    }
    func stop() {
        if let process = scrcpyProcess {
            scrcpyProcess = nil
            process.terminationHandler = nil
            OwnedProcess.stop(process)
            message = "A janela do scrcpy foi encerrada. Revise o checklist antes de começar outra sessão."
        } else if activity == .airPlayGuidance { message = "Orientação concluída. Se ainda houver espelhamento, encerre no iPhone: Central de Controle → Espelhar a Tela → Parar Espelhamento." }
        else if activity == .quickTimeGuidance { message = "Orientação concluída. Feche a janela de prévia no QuickTime para encerrar o espelhamento." }
        activity = .idle
        confirmations = []
    }
    func expirePreparationIfNeeded() {
        guard !active, let verifiedAt, !PreparationLifetime.isFresh(verifiedAt: verifiedAt, now: ProcessInfo.processInfo.systemUptime) else { return }
        invalidate()
        adb.stop()
        message = "A preparação expirou após 5 minutos. Confira novamente antes de mostrar sua tela."
    }
    private func systemChanged(_ reason: String) {
        if activity == .android { stop() }
        invalidate()
        adb.stop()
        message = active ? "\(reason) Se a sessão do sistema continuar, encerre no iPhone ou no QuickTime." : reason
    }
    func shutdown() {
        disposed = true
        diagnosticGeneration = UUID()
        stop()
        invalidate()
        adb.stop()
        pathMonitor.cancel()
        for token in observers { NSWorkspace.shared.notificationCenter.removeObserver(token) }
        observers.removeAll()
    }
    func openAirPlaySettings() {
        if let url = URL(string: "x-apple.systempreferences:com.apple.AirDrop-Handoff-Settings.extension") { NSWorkspace.shared.open(url) }
    }
}
