using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LetMeSee
{
    public static class AndroidRuntimeVerifier
    {
        public static RuntimeVerificationResult Verify(string directory)
        {
            try { return VerifyFiles(directory); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is CryptographicException || ex is System.Security.SecurityException)) throw;
                return Failure("Nao foi possivel verificar o pacote Android. Importe novamente uma origem confiavel.");
            }
        }
        private static RuntimeVerificationResult VerifyFiles(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory)) return Failure("Motor Android ausente. Prepare o pacote oficial scrcpy + adb.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) return Failure("Motor Android ausente. Prepare o pacote oficial scrcpy + adb.");
            if (IsLink(root)) return Failure("O pacote Android nao pode usar links ou pontos de nova analise.");
            string manifest = Path.Combine(root, "runtime-files.sha256");
            if (!File.Exists(manifest) || IsLink(manifest) || new FileInfo(manifest).Length > 2097152) return Failure("Manifesto SHA-256 Android ausente ou invalido.");
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (StreamReader reader = new StreamReader(manifest, new UTF8Encoding(false, true), true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (line.Length < 67 || line.Substring(64, 2) != "  ") return Failure("Formato do manifesto Android invalido.");
                    string hash = line.Substring(0, 64);
                    foreach (char character in hash) if (!Uri.IsHexDigit(character)) return Failure("Hash Android invalido.");
                    string relative = line.Substring(66).Replace('\\', '/');
                    if (!SafePath(relative) || relative.Equals("runtime-files.sha256", StringComparison.OrdinalIgnoreCase) || expected.ContainsKey(relative) || expected.Count >= 20000)
                        return Failure("Caminho Android inseguro, duplicado ou excessivo no manifesto.");
                    expected.Add(relative, hash);
                }
            }
            foreach (string required in new[] { "scrcpy.exe", "adb.exe", "scrcpy-server" })
                if (!expected.ContainsKey(required)) return Failure("O manifesto Android precisa incluir scrcpy.exe, adb.exe e scrcpy-server.");
            HashSet<string> actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Queue<string> directories = new Queue<string>();
            directories.Enqueue(root);
            int visited = 0;
            while (directories.Count > 0)
            {
                foreach (string child in Directory.GetFileSystemEntries(directories.Dequeue()))
                {
                    if (++visited > 50000 || IsLink(child)) return Failure("Pacote Android excessivo ou contendo links.");
                    if (Directory.Exists(child)) { directories.Enqueue(child); continue; }
                    string relative = child.Substring(root.Length + 1).Replace('\\', '/');
                    if (!SafePath(relative) || !actual.Add(relative)) return Failure("Nome Android inseguro ou ambiguo.");
                    // Inventory every file, including the extensionless Android server and data libraries.
                    if (!relative.Equals("runtime-files.sha256", StringComparison.OrdinalIgnoreCase) && !expected.ContainsKey(relative))
                        return Failure("Arquivo Android fora do manifesto. O motor foi bloqueado.");
                }
            }
            foreach (KeyValuePair<string, string> entry in expected)
            {
                if (!actual.Contains(entry.Key)) return Failure("Arquivo Android ausente.");
                using (FileStream stream = new FileStream(Path.Combine(root, entry.Key.Replace('/', Path.DirectorySeparatorChar)), FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SHA256 hash = SHA256.Create())
                {
                    string actualHash = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                    if (!String.Equals(actualHash, entry.Value, StringComparison.OrdinalIgnoreCase)) return Failure("Integridade Android divergente. O motor foi bloqueado.");
                }
            }
            return new RuntimeVerificationResult(true, "Integridade do pacote Android conferida por SHA-256. A origem deve ser oficial e confiavel.", expected.Count);
        }
        private static RuntimeVerificationResult Failure(string message) { return new RuntimeVerificationResult(false, message, 0); }
        private static bool IsLink(string path) { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        private static bool SafePath(string value)
        {
            if (String.IsNullOrEmpty(value) || Path.IsPathRooted(value) || value.StartsWith("/", StringComparison.Ordinal)) return false;
            foreach (char character in value) if (Char.IsControl(character) || ":*?\"<>|".IndexOf(character) >= 0) return false;
            foreach (string segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal)) return false;
                string basename = segment.Split('.')[0].ToUpperInvariant();
                if (basename == "CON" || basename == "PRN" || basename == "AUX" || basename == "NUL") return false;
                if (basename.Length == 4 && (basename.StartsWith("COM", StringComparison.Ordinal) || basename.StartsWith("LPT", StringComparison.Ordinal)) && basename[3] >= '1' && basename[3] <= '9') return false;
            }
            return true;
        }
    }

    public static class AndroidDevicesParser
    {
        // Parse only documented adb list rows. Diagnostics, control codes and ambiguous duplicates cannot become arguments.
        public static IList<AndroidDevice> Parse(string output)
        {
            if (output == null || output.Length > 65536) throw new ArgumentException("Resposta Android ausente ou excessiva.", "output");
            List<AndroidDevice> devices = new List<AndroidDevice>();
            HashSet<string> serials = new HashSet<string>(StringComparer.Ordinal);
            bool header = false;
            using (StringReader reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim() == "List of devices attached") { header = true; continue; }
                    if (String.IsNullOrWhiteSpace(line)) continue;
                    if (!header) continue;
                    if (line.Length > 2048 || devices.Count >= 128) throw new ArgumentException("Lista Android excessiva.", "output");
                    string[] columns = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length < 2 || !AndroidDevice.IsSafeSerial(columns[0])) throw new ArgumentException("Linha Android invalida.", "output");
                    if (!serials.Add(columns[0])) throw new ArgumentException("Serial Android duplicado.", "output");
                    AndroidDeviceState state = columns[1] == "device" ? AndroidDeviceState.Device
                        : columns[1] == "unauthorized" ? AndroidDeviceState.Unauthorized
                        : columns[1] == "offline" ? AndroidDeviceState.Offline : AndroidDeviceState.Unavailable;
                    string model = "Android";
                    for (int index = 2; index < columns.Length; index++)
                        if (columns[index].StartsWith("model:", StringComparison.Ordinal)) model = SanitizeModel(columns[index].Substring(6));
                    bool wireless = columns[0].IndexOf(':') >= 0 || columns[0].EndsWith("._adb-tls-connect._tcp", StringComparison.OrdinalIgnoreCase);
                    devices.Add(new AndroidDevice(columns[0], model, state, wireless));
                }
            }
            if (!header) throw new ArgumentException("Resposta adb sem cabecalho esperado.", "output");
            return devices.AsReadOnly();
        }
        private static string SanitizeModel(string model)
        {
            StringBuilder safe = new StringBuilder();
            foreach (char character in model)
            {
                if (safe.Length >= 80) break;
                if (Char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == '.') safe.Append(character);
            }
            return safe.Length == 0 ? "Android" : safe.ToString();
        }
    }

    public static class AndroidArguments
    {
        public static string Build(string serial, bool mute)
        {
            if (!AndroidDevice.IsSafeSerial(serial)) throw new ArgumentException("Selecione um serial Android valido.", "serial");
            return "--serial " + UxPlayArguments.Quote(serial) + " --no-control --no-clipboard-autosync --window-title LetMeSee" + (mute ? " --no-audio" : "");
        }
    }
}
