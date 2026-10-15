import Foundation

@main struct DomainTests {
    static var assertions = 0
    static func expect(_ condition: @autoclosure () -> Bool, _ message: String) {
        assertions += 1
        if !condition() { fatalError("FAIL: \(message)") }
    }
    static func main() throws {
        var configuration = Configuration()
        expect(configuration.method == .androidUSB, "Android recomendado usa USB")
        expect(!configuration.includeAudio, "áudio exige opt-in")
        configuration.connection = .wifi
        expect(configuration.method == .androidWiFi, "Android Wi-Fi é escolha explícita")
        configuration.device = .iPhone
        expect(configuration.method == .airPlay, "iPhone Wi-Fi usa receptor do macOS")
        configuration.connection = .automatic
        expect(configuration.method == .airPlay, "iPhone recomendado usa AirPlay com gate de compatibilidade")
        configuration.connection = .usb
        expect(configuration.method == .quickTime, "iPhone USB usa QuickTime")

        let parsed = AndroidParser.devices("""
        * daemon started successfully
        List of devices attached
        USB123 device product:panther model:Pixel_7 device:panther transport_id:1
        USB456 unauthorized usb:1-2
        USB789 offline
        192.168.1.2:38991 device model:Pixel_9
        abc._adb-tls-connect._tcp device model:Pixel_8
        emulator-5554 device model:sdk
        -injected device
        bad;argument device
        DUP device
        DUP device
        """)
        expect(parsed.count == 5, "somente seriais únicos, físicos e válidos")
        expect(parsed[0].model == "Pixel 7", "modelo legível")
        expect(parsed[1].state == .unauthorized, "não autorizado permanece bloqueado")
        expect(parsed[2].state == .offline, "offline permanece bloqueado")
        expect(parsed[3].isWireless && parsed[4].isWireless, "IP e mDNS identificam Wi-Fi")
        expect(!parsed[0].isWireless, "serial USB não se torna Wi-Fi")
        expect(AndroidParser.devices("random garbage\n\n").isEmpty, "logs inválidos não simulam aparelhos")
        expect(!AndroidParser.validSerial(String(repeating: "A", count: 257)), "serial exagerado rejeitado")
        expect(!AndroidParser.validSerial("A\nB"), "newline rejeitado")
        expect(ScrcpyArguments.make(serial: "USB123", includeAudio: false) == ["--serial", "USB123", "--no-control", "--no-clipboard-autosync", "--no-audio"], "visualização USB sem controle/clipboard/áudio")
        expect(ScrcpyArguments.make(serial: "192.168.1.2:38991", includeAudio: true) == ["--serial", "192.168.1.2:38991", "--no-control", "--no-clipboard-autosync"], "áudio opt-in mantém controle e clipboard desativados")
        expect(ScrcpyArguments.make(serial: "--tcpip", includeAudio: true) == nil, "serial não injeta argumento")

        var facts = ReadinessFacts()
        for device in DeviceKind.allCases {
            for connection in ConnectionChoice.allCases {
                for experience in Experience.allCases {
                    configuration = Configuration(device: device, connection: connection, experience: experience)
                    expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: []), "novo ambiente bloqueado: \(device)/\(connection)/\(experience)")
                }
            }
        }
        configuration = Configuration()
        facts.toolsVerified = true
        facts.devices = [parsed[0]]
        let usbConfirms: Set<String> = ["privacy", "usbDebug"]
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "mesmo um aparelho exige seleção explícita")
        facts.selectedSerial = "USB123"
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "Android USB autorizado e confirmado libera")
        facts.toolsVerified = false
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "ferramenta não aprovada bloqueia")
        facts.toolsVerified = true
        facts.devices = [parsed[1]]; facts.selectedSerial = "USB456"
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "RSA não autorizado bloqueia")
        facts.devices = [parsed[2]]; facts.selectedSerial = "USB789"
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "offline bloqueia")
        facts.devices = [parsed[0], parsed[3]]; facts.selectedSerial = "USB123"
        configuration.connection = .wifi
        let wifiConfirms: Set<String> = ["privacy", "wirelessPaired", "privateNetwork"]
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: wifiConfirms), "Wi-Fi não aceita serial USB")
        facts.selectedSerial = parsed[3].serial
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: wifiConfirms), "Wi-Fi já pareado selecionado libera")
        facts.diagnosticsRunning = true
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: wifiConfirms), "diagnóstico pendente bloqueia")
        facts.diagnosticsRunning = false
        configuration.connection = .usb
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: usbConfirms), "USB não aceita serial Wi-Fi")

        configuration = Configuration(device: .iPhone)
        facts = ReadinessFacts()
        let airPlayConfirms: Set<String> = ["privacy", "airPlayReceiver", "privateNetwork", "nativeHandoff"]
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: airPlayConfirms), "AirPlay depende das confirmações explícitas, não de tools Android")
        for id in airPlayConfirms {
            expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: airPlayConfirms.subtracting([id])), "AirPlay exige \(id)")
        }
        configuration.connection = .usb
        let quickTimeConfirms: Set<String> = ["privacy", "trustedCable", "nativeHandoff"]
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: quickTimeConfirms), "QuickTime ausente bloqueia")
        facts.quickTimeAvailable = true
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: quickTimeConfirms), "QuickTime disponível e confirmação do cabo liberam orientação")
        expect(!ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: airPlayConfirms), "confirmações de AirPlay não liberam USB")

        let encoded = try JSONEncoder().encode(configuration)
        let text = String(decoding: encoded, as: UTF8.self)
        expect(!text.contains("privacy") && !text.contains("nativeHandoff") && !text.contains("serial"), "preferências não persistem confirmações nem dispositivo")
        let decoded = try JSONDecoder().decode(Configuration.self, from: encoded)
        expect(decoded == configuration, "configuração tem round trip")
        expect(PreparationLifetime.isFresh(verifiedAt: 100, now: 399), "preparação recente válida")
        expect(!PreparationLifetime.isFresh(verifiedAt: 100, now: 400), "preparação expira no limite")
        expect(!PreparationLifetime.isFresh(verifiedAt: 100, now: 99), "relógio invertido não prolonga preparação")
        configuration = Configuration()
        facts = ReadinessFacts()
        facts.toolsVerified = true
        facts.devices = [parsed[0], AndroidDevice(serial: "OTHERUSB", state: .ready, model: "Other Android")]
        facts.selectedSerial = "USB123"
        var contextualConfirms = usbConfirms
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: contextualConfirms), "A preparado antes da troca")
        facts.selectDevice("OTHERUSB", confirmations: &contextualConfirms)
        expect(facts.selectedSerial == "OTHERUSB" && facts.devices.count == 2 && facts.toolsVerified, "troca preserva seleção B, lista e diagnósticos")
        expect(contextualConfirms.isEmpty && !ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: contextualConfirms), "troca de aparelho invalida privacidade e autorização contextual")
        contextualConfirms = usbConfirms
        expect(ChecklistEngine.ready(configuration: configuration, facts: facts, confirmations: contextualConfirms), "B pode ser confirmado sem apagar os diagnósticos")
        facts.selectDevice("OTHERUSB", confirmations: &contextualConfirms)
        expect(contextualConfirms == usbConfirms, "seleção idêntica não limpa confirmações")
        print("PASS · \(assertions) verificações macOS: matriz de ambientes, gates, parser e argumentos")
    }
}
