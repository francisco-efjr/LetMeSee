import Foundation

@main struct InfrastructureTests {
    static func require(_ condition: @autoclosure () -> Bool, _ message: String) {
        if !condition() { fatalError("FAIL: \(message)") }
    }
    static func main() async throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent("LetMeSee-test-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: directory) }
        let asset = directory.appendingPathComponent("server with spaces;$text")
        try Data("abc".utf8).write(to: asset)
        let identity = try ToolIdentity.inspect(asset.path, requireExecutable: false)
        require(identity.sha256 == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA-256 conhecido")
        do { _ = try ToolIdentity.inspect(asset.path); fatalError("arquivo sem permissão de execução aceito") } catch {}
        try Data("changed".utf8).write(to: asset)
        let changed = try ToolIdentity.inspect(asset.path, requireExecutable: false)
        require(!changed.matches(identity.approval), "mudança invalida aprovação")
        do { _ = try ToolIdentity.inspect(directory.path, requireExecutable: false); fatalError("diretório aceito") } catch {}
        let link = directory.appendingPathComponent("symlink")
        try FileManager.default.createSymbolicLink(at: link, withDestinationURL: asset)
        let canonical = try ToolIdentity.inspect(link.path, requireExecutable: false)
        require(canonical.path == asset.path, "caminho aprovado é canônico")
        let forbidden = ["SCRCPY_SERVER_PATH", "ADB_SERVER_SOCKET", "ANDROID_SERIAL", "DYLD_INSERT_LIBRARIES", "SDL_VIDEODRIVER"]
        require(forbidden.allSatisfy { SafeProcessEnvironment.base[$0] == nil }, "ambiente não herda overrides de executáveis")
        require(SafeProcessEnvironment.base["PATH"] == "/usr/bin:/bin:/usr/sbin:/sbin", "PATH restrito")
        let literal = "$(touch /tmp/letmesee-never-execute); `whoami`"
        let echo = try await CommandRunner.run(path: "/bin/echo", arguments: [literal])
        require(echo.code == 0 && echo.text.trimmingCharacters(in: .whitespacesAndNewlines) == literal, "argumentos executados sem shell")
        let start = Date()
        do { _ = try await CommandRunner.run(path: "/bin/sleep", arguments: ["3"], seconds: 0.15); fatalError("timeout não aplicado") } catch {}
        require(Date().timeIntervalSince(start) < 2, "timeout é limitado")
        print("PASS · infraestrutura macOS: hash, aprovação, arquivo, symlink, ambiente, argumentos literais e timeout")
    }
}
