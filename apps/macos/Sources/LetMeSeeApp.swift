import AppKit
import SwiftUI

enum Palette {
    static let background = Color(red: 17 / 255, green: 23 / 255, blue: 23 / 255)
    static let surface = Color(red: 26 / 255, green: 33 / 255, blue: 32 / 255)
    static let text = Color(red: 245 / 255, green: 241 / 255, blue: 229 / 255)
    static let accent = Color(red: 235 / 255, green: 143 / 255, blue: 93 / 255)
    static let paper = Color(red: 233 / 255, green: 222 / 255, blue: 197 / 255)
    static let ink = Color(red: 38 / 255, green: 43 / 255, blue: 35 / 255)
    static let muted = Color(red: 183 / 255, green: 192 / 255, blue: 181 / 255)
}

struct CardboardTexture: View {
    var body: some View {
        Canvas { context, size in
            for index in 0..<1_000 {
                let x = CGFloat((index * 83 + 19) % 997) / 997 * size.width
                let y = CGFloat((index * 173 + 47) % 991) / 991 * size.height
                context.fill(Path(ellipseIn: CGRect(x: x, y: y, width: index % 3 == 0 ? 1.5 : 0.7, height: 0.7)), with: .color(Palette.paper.opacity(0.07)))
            }
        }
        .allowsHitTesting(false)
        .accessibilityHidden(true)
    }
}

struct PulseMark: View {
    var body: some View {
        ZStack {
            Circle().stroke(Palette.accent.opacity(0.18), lineWidth: 1).frame(width: 68, height: 68)
            Circle().stroke(Palette.accent.opacity(0.35), lineWidth: 1).frame(width: 48, height: 48)
            Image(systemName: "waveform.path.ecg").font(.system(size: 27, weight: .medium)).foregroundStyle(Palette.accent)
        }
        .frame(width: 76, height: 76)
        .accessibilityHidden(true)
    }
}

struct MainView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        ZStack {
            Palette.background.ignoresSafeArea()
            CardboardTexture()
            VStack(alignment: .leading, spacing: 28) {
                HStack(alignment: .center, spacing: 12) {
                    PulseMark()
                    VStack(alignment: .leading, spacing: 5) {
                        Text("LetMeSee").font(.system(size: 27, weight: .semibold, design: .rounded)).tracking(-0.6)
                        Text("SUA TELA, MAIS PERTO").font(.system(size: 10, weight: .semibold)).tracking(2).foregroundStyle(Palette.muted)
                    }
                    Spacer()
                    Text("macOS").font(.system(size: 12, weight: .medium)).foregroundStyle(Palette.muted)
                }
                VStack(alignment: .leading, spacing: 12) {
                    Text(model.active ? "ORIENTAÇÃO / SESSÃO" : "SEU ESPAÇO DE ESPELHAMENTO").font(.system(size: 10, weight: .semibold)).tracking(1.8).foregroundStyle(Palette.accent)
                    Text(model.statusTitle).font(.system(size: 32, weight: .semibold, design: .rounded)).tracking(-0.8).fixedSize(horizontal: false, vertical: true)
                    Text(model.message).font(.system(size: 14)).lineSpacing(5).foregroundStyle(Palette.muted).fixedSize(horizontal: false, vertical: true)
                }
                .padding(28)
                .frame(maxWidth: .infinity, minHeight: 208, alignment: .topLeading)
                .background(Palette.surface, in: RoundedRectangle(cornerRadius: 12))
                .overlay(RoundedRectangle(cornerRadius: 12).stroke(Palette.paper.opacity(0.09), lineWidth: 1))
                HStack(alignment: .center, spacing: 16) {
                    Label(model.configuration.method.title, systemImage: model.configuration.device == .android ? "smartphone" : "iphone").font(.system(size: 12, weight: .medium)).foregroundStyle(Palette.muted)
                    Spacer()
                    Text(model.ready ? "Checklist conferido" : "Checklist necessário").font(.system(size: 11)).foregroundStyle(Palette.muted)
                }
                Spacer(minLength: 0)
                HStack(spacing: 12) {
                    Button { model.editEnvironment() } label: {
                        HStack { Image(systemName: "slider.horizontal.3"); Text("Ambiente"); Image(systemName: "chevron.down").font(.system(size: 10, weight: .semibold)) }
                            .font(.system(size: 14, weight: .semibold)).frame(minWidth: 144, minHeight: 48)
                            .background(Palette.paper, in: RoundedRectangle(cornerRadius: 12)).foregroundStyle(Palette.ink)
                    }
                    .buttonStyle(.plain)
                    .disabled(model.active || model.preparing)
                    .opacity(model.active || model.preparing ? 0.45 : 1)
                    .help("Escolher o celular, conexão e revisar o checklist")
                    Button { model.primaryAction() } label: {
                        HStack(spacing: 9) {
                            if model.preparing { ProgressView().controlSize(.small) }
                            else { Image(systemName: model.active ? "stop.fill" : "play.fill") }
                            Text(model.primaryTitle)
                        }
                        .font(.system(size: 14, weight: .semibold))
                        .frame(maxWidth: .infinity, minHeight: 48)
                        .background(Palette.accent, in: RoundedRectangle(cornerRadius: 12)).foregroundStyle(Palette.ink)
                    }
                    .buttonStyle(.plain)
                    .disabled(model.preparing || (!model.active && !model.ready))
                    .opacity(model.preparing || (!model.active && !model.ready) ? 0.45 : 1)
                    .keyboardShortcut(.return, modifiers: [])
                }
                Text("Local por padrão. Sem downloads automáticos. Você escolhe quando começar.")
                    .font(.system(size: 10)).foregroundStyle(Palette.muted).frame(maxWidth: .infinity)
            }
            .padding(36)
            .foregroundStyle(Palette.text)
        }
        .frame(minWidth: 700, minHeight: 580)
        .sheet(isPresented: $model.showChecklist) { ChecklistView(model: model) }
        .task { await model.refresh() }
        .onReceive(Timer.publish(every: 20, on: .main, in: .common).autoconnect()) { _ in model.expirePreparationIfNeeded() }
    }
}

struct ChecklistView: View {
    @ObservedObject var model: AppModel
    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            VStack(alignment: .leading, spacing: 8) {
                Text("ANTES DE MOSTRAR SUA TELA").font(.system(size: 10, weight: .semibold)).tracking(1.8).foregroundStyle(Palette.accent)
                HStack {
                    Text("Prepare seu ambiente").font(.system(size: 26, weight: .semibold, design: .rounded))
                    Spacer()
                    Text("\(model.completed) / \(model.items.count)").font(.system(size: 13, weight: .medium, design: .monospaced)).foregroundStyle(Palette.muted)
                }
                Text("Só pedimos o que se aplica à sua conexão. A preparação vale por 5 minutos. Mudar o ambiente, a rede, suspender o Mac ou verificar novamente limpa as confirmações.").font(.system(size: 12)).foregroundStyle(Palette.muted).fixedSize(horizontal: false, vertical: true)
            }
            .padding(24)
            Divider().overlay(Palette.paper.opacity(0.12))
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    VStack(alignment: .leading, spacing: 14) {
                        HStack(spacing: 20) {
                            setupPicker("Celular ou tablet", selection: $model.configuration.device) { ForEach(DeviceKind.allCases) { Text($0.label).tag($0) } }
                            setupPicker("Como prefere conectar?", selection: $model.configuration.connection) { ForEach(ConnectionChoice.allCases) { Text($0.label).tag($0) } }
                        }
                        HStack(spacing: 20) {
                            setupPicker("Instruções", selection: $model.configuration.experience) { ForEach(Experience.allCases) { Text($0.label).tag($0) } }
                            if model.configuration.method.isAndroid {
                                Toggle("Incluir áudio do Android", isOn: $model.configuration.includeAudio).font(.system(size: 12)).frame(maxWidth: .infinity, alignment: .leading)
                            } else { Spacer().frame(maxWidth: .infinity) }
                        }
                    }
                    Text(model.configuration.method.explanation).font(.system(size: 12)).foregroundStyle(Palette.muted).lineSpacing(3).fixedSize(horizontal: false, vertical: true)
                    if model.configuration.method.isAndroid { toolsPanel }
                    if model.configuration.method == .airPlay {
                        Button("Abrir ajustes de AirPlay / Continuidade") { model.openAirPlaySettings() }.controlSize(.large)
                        Text("Se esse painel não existir, procure por “Receptor AirPlay” nos Ajustes. Se o Mac não oferecer o recurso, escolha Cabo USB acima.").font(.system(size: 11)).foregroundStyle(Palette.muted)
                    }
                    HStack {
                        Text("CHECKLIST").font(.system(size: 10, weight: .semibold)).tracking(1.6).foregroundStyle(Palette.accent)
                        Spacer()
                        Button { Task { await model.refresh() } } label: { Label("Verificar novamente", systemImage: "arrow.clockwise") }
                            .disabled(model.facts.diagnosticsRunning).help("Atualiza o diagnóstico e limpa confirmações anteriores")
                    }
                    if model.facts.diagnosticsRunning { HStack { ProgressView().controlSize(.small); Text("Conferindo recursos locais…").font(.system(size: 12)) } }
                    VStack(spacing: 0) {
                        ForEach(model.items) { item in
                            checklistRow(item)
                            if item.id != model.items.last?.id { Divider().overlay(Palette.paper.opacity(0.08)).padding(.horizontal, 16) }
                        }
                    }
                    .background(Palette.surface, in: RoundedRectangle(cornerRadius: 12))
                    if model.configuration.method.isAndroid && !model.facts.devices.isEmpty {
                        Picker("Escolha seu Android", selection: Binding(get: { model.facts.selectedSerial ?? "" }, set: { model.selectDevice($0.isEmpty ? nil : $0) })) {
                            Text("Selecione um dispositivo").tag("")
                            ForEach(model.facts.devices) { device in Text(device.label).tag(device.serial) }
                        }
                        .pickerStyle(.menu)
                        .font(.system(size: 12))
                        Text("Não autorizado: desbloqueie e aceite a chave RSA no Android. Offline: reconecte o cabo. Múltiplos aparelhos: escolha o correto. Outro app pode estar usando o USB.").font(.system(size: 11)).foregroundStyle(Palette.muted)
                    }
                    if model.configuration.experience == .technical {
                        Text("Nenhuma porta de rede de espelhamento é aberta pelo LetMeSee. Android usa adb privado em 127.0.0.1 e scrcpy --no-control. Não usamos adb kill-server, tcpip, login remoto nem alterações de firewall. QuickTime e AirPlay ficam sob controle do macOS.").font(.system(size: 11, design: .monospaced)).foregroundStyle(Palette.muted).lineSpacing(3)
                    }
                    if let error = model.settingsError { Text(error).font(.system(size: 12)).foregroundStyle(Palette.accent) }
                }
                .padding(24)
            }
            Divider().overlay(Palette.paper.opacity(0.12))
            HStack {
                Button("Voltar") { model.showChecklist = false }.keyboardShortcut(.cancelAction)
                Spacer()
                Text(model.ready ? "Tudo conferido" : "Conclua todos os itens para liberar o início").font(.system(size: 11)).foregroundStyle(Palette.muted)
                Button("Concluir preparação") { model.saveChecklist() }.buttonStyle(.borderedProminent).tint(Palette.accent).foregroundStyle(Palette.ink).disabled(!model.ready)
            }
            .padding(20)
        }
        .frame(width: min(740, (NSScreen.main?.visibleFrame.width ?? 900) - 64), height: min(740, (NSScreen.main?.visibleFrame.height ?? 900) - 80))
        .background(Palette.background)
        .foregroundStyle(Palette.text)
        .preferredColorScheme(.dark)
        .onChange(of: model.configuration) { _ in Task { await model.refresh() } }
    }
    private func setupPicker<Value: Hashable, Content: View>(_ label: String, selection: Binding<Value>, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(label).font(.system(size: 11, weight: .medium)).foregroundStyle(Palette.muted)
            Picker(label, selection: selection, content: content).labelsHidden().pickerStyle(.menu).frame(maxWidth: .infinity, alignment: .leading)
        }.frame(maxWidth: .infinity, alignment: .leading)
    }
    private func checklistRow(_ item: ChecklistItem) -> some View {
        HStack(alignment: .top, spacing: 12) {
            if item.manual {
                Toggle("Confirmar \(item.title)", isOn: Binding(get: { model.confirmations.contains(item.id) }, set: { checked in if checked { model.confirmations.insert(item.id) } else { model.confirmations.remove(item.id) } }))
                    .labelsHidden().toggleStyle(.checkbox).padding(.top, 2)
            } else { Image(systemName: item.passed ? "checkmark.circle.fill" : "circle.dashed").foregroundStyle(item.passed ? Palette.accent : Palette.muted).frame(width: 16).padding(.top, 1).accessibilityLabel(item.passed ? "Conferido" : "Pendente") }
            VStack(alignment: .leading, spacing: 5) {
                Text(item.title).font(.system(size: 13, weight: .semibold))
                Text(item.detail).font(.system(size: 11)).lineSpacing(3).foregroundStyle(Palette.muted).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 0)
        }
        .padding(16)
    }
    private var toolsPanel: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Ferramentas instaladas por você").font(.system(size: 13, weight: .semibold))
            Text("O hash identifica o arquivo local; não certifica a origem ou ausência de falhas. Confira a procedência antes de autorizar sua execução.").font(.system(size: 11)).foregroundStyle(Palette.muted)
            HStack(spacing: 16) {
                Link("Instruções oficiais · adb", destination: URL(string: "https://developer.android.com/tools/releases/platform-tools")!)
                Link("Instruções oficiais · scrcpy", destination: URL(string: "https://github.com/Genymobile/scrcpy/blob/master/doc/macos.md")!)
            }.font(.system(size: 11))
            toolRow("adb", identity: model.adbIdentity)
            toolRow("scrcpy", identity: model.scrcpyIdentity)
            toolRow("scrcpy-server", identity: model.serverIdentity)
            if !model.toolsApproved {
                Button("Confio nestes três arquivos · verificar versões") { model.approveTools() }.disabled(model.adbIdentity == nil || model.scrcpyIdentity == nil || model.serverIdentity == nil || model.facts.diagnosticsRunning)
            }
        }
        .padding(16)
        .background(Palette.surface, in: RoundedRectangle(cornerRadius: 12))
    }
    private func toolRow(_ name: String, identity: ToolIdentity?) -> some View {
        HStack(alignment: .top, spacing: 10) {
            VStack(alignment: .leading, spacing: 3) {
                Text(name).font(.system(size: 11, weight: .semibold, design: .monospaced))
                Text(identity?.path ?? "Não encontrado em /opt/homebrew/bin ou /usr/local/bin").font(.system(size: 10, design: .monospaced)).foregroundStyle(Palette.muted).textSelection(.enabled)
                if let identity { Text("SHA-256 \(identity.sha256)").font(.system(size: 9, design: .monospaced)).foregroundStyle(Palette.muted).textSelection(.enabled).fixedSize(horizontal: false, vertical: true) }
            }
            Spacer()
            Button("Selecionar…") { model.chooseTool(name) }.font(.system(size: 11)).disabled(model.facts.diagnosticsRunning)
        }
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    var onTerminate: (() -> Void)?
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationWillTerminate(_ notification: Notification) { onTerminate?() }
}

@main struct LetMeSeeApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @StateObject private var model = AppModel()
    var body: some Scene {
        WindowGroup("LetMeSee") {
            MainView(model: model)
                .onAppear { delegate.onTerminate = { model.shutdown() }; NSApplication.shared.activate(ignoringOtherApps: true) }
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 820, height: 640)
        .commands { CommandGroup(replacing: .newItem) {} }
    }
}
