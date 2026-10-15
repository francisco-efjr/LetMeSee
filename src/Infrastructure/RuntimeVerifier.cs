using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LetMeSee
{
    public sealed class RuntimeVerificationResult
    {
        public bool IsValid { get; private set; }
        public string Message { get; private set; }
        public int VerifiedFileCount { get; private set; }
        internal RuntimeVerificationResult(bool valid, string message, int count)
        { IsValid = valid; Message = message; VerifiedFileCount = count; }
    }

    public static class RuntimeVerifier
    {
        private static readonly HashSet<string> ExecutableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".dll", ".com", ".scr", ".cpl", ".ocx", ".msi", ".msp", ".mst", ".bat", ".cmd", ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".pif", ".lnk", ".sys", ".sct", ".scf", ".chm" };

        public static RuntimeVerificationResult Verify(string binDirectory)
        {
            try { return VerifyFiles(binDirectory); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is CryptographicException || ex is System.Security.SecurityException)) throw;
                return Failure("Nao foi possivel verificar o motor local. Reinstale um pacote confiavel.");
            }
        }

        private static RuntimeVerificationResult VerifyFiles(string directory)
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) return Failure("Motor ausente. Instale o pacote de runtime verificado.");
            if (IsLink(root)) return Failure("O runtime nao pode usar links ou pontos de nova analise.");
            string manifest = Path.Combine(root, "runtime-files.sha256");
            if (!File.Exists(manifest)) return Failure("Manifesto SHA-256 ausente. O motor nao sera iniciado.");
            if (IsLink(manifest) || new FileInfo(manifest).Length > 2097152) return Failure("Manifesto de runtime invalido.");
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (StreamReader reader = new StreamReader(manifest, new UTF8Encoding(false, true), true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (line.Length < 67 || line.Substring(64, 2) != "  ") return Failure("Formato do manifesto SHA-256 invalido.");
                    string hash = line.Substring(0, 64);
                    foreach (char character in hash)
                        if (!Uri.IsHexDigit(character)) return Failure("Hash invalido no manifesto de runtime.");
                    string relative = line.Substring(66).Replace('\\', '/');
                    if (!IsSafeRelativePath(relative) || relative.Equals("runtime-files.sha256", StringComparison.OrdinalIgnoreCase))
                        return Failure("Caminho inseguro no manifesto de runtime.");
                    if (expected.ContainsKey(relative) || expected.Count >= 20000) return Failure("Entradas duplicadas ou excessivas no manifesto.");
                    expected.Add(relative, hash);
                }
            }
            if (!expected.ContainsKey("uxplay.exe")) return Failure("O manifesto precisa incluir uxplay.exe.");
            List<string> files = new List<string>();
            Queue<string> directories = new Queue<string>();
            directories.Enqueue(root);
            int visited = 0;
            while (directories.Count > 0)
            {
                string current = directories.Dequeue();
                foreach (string child in Directory.GetFileSystemEntries(current))
                {
                    if (++visited > 50000) return Failure("Runtime excede o limite de arquivos permitidos.");
                    if (IsLink(child)) return Failure("O runtime nao pode usar links ou pontos de nova analise.");
                    if (Directory.Exists(child)) { directories.Enqueue(child); continue; }
                    string relative = child.Substring(root.Length + 1).Replace('\\', '/');
                    if (!IsSafeRelativePath(relative)) return Failure("Nome de arquivo inseguro no runtime.");
                    if (ExecutableExtensions.Contains(Path.GetExtension(child)) && !expected.ContainsKey(relative))
                        return Failure("Arquivo executavel sem verificacao no runtime. Reinstale o pacote.");
                    files.Add(relative);
                }
            }
            HashSet<string> actual = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> entry in expected)
            {
                if (!actual.Contains(entry.Key)) return Failure("Um arquivo do runtime esta ausente.");
                string path = Path.Combine(root, entry.Key.Replace('/', Path.DirectorySeparatorChar));
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SHA256 algorithm = SHA256.Create())
                {
                    string hash = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
                    if (!String.Equals(hash, entry.Value, StringComparison.OrdinalIgnoreCase))
                        return Failure("Integridade do runtime divergente. O motor foi bloqueado.");
                }
            }
            return new RuntimeVerificationResult(true, "Runtime local verificado por SHA-256.", expected.Count);
        }

        private static bool IsLink(string path) { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        private static RuntimeVerificationResult Failure(string message) { return new RuntimeVerificationResult(false, message, 0); }
        private static bool IsSafeRelativePath(string value)
        {
            if (String.IsNullOrEmpty(value) || value.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(value)) return false;
            foreach (char character in value)
                if (Char.IsControl(character) || ":*?\"<>|".IndexOf(character) >= 0) return false;
            foreach (string segment in value.Split('/'))
                if (segment.Length == 0 || segment == "." || segment == ".." || segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal)) return false;
            return true;
        }
    }
}
