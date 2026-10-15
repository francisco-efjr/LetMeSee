using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace LetMeSee
{
    public sealed class AppSettings
    {
        public string ReceiverName { get; set; }
        public bool Mute { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool EnableAnimation { get; set; }
        public bool RequirePin { get; set; }
        public int SessionLimitMinutes { get; set; }

        public AppSettings()
        {
            ReceiverName = "LetMeSee";
            EnableAnimation = true;
            RequirePin = true;
        }

        public AppSettings Clone() { return (AppSettings)MemberwiseClone(); }

        public bool Validate(out string error)
        {
            error = null;
            if (String.IsNullOrWhiteSpace(ReceiverName) || ReceiverName.Length > 48)
                error = "Use um nome com 1 a 48 caracteres.";
            else if (ReceiverName[0] == '-' || ReceiverName != ReceiverName.Trim())
                error = "O nome nao pode iniciar com hifen ou ter espacos nas extremidades.";
            else
            {
                for (int i = 0; i < ReceiverName.Length; i++)
                {
                    char value = ReceiverName[i];
                    if (Char.IsControl(value) || value == '\"' || value == '\'' || value == '\\' ||
                        Char.GetUnicodeCategory(value) == UnicodeCategory.Format)
                    { error = "O nome nao pode conter controles, aspas ou barra invertida."; break; }
                    // Reject lone UTF-16 surrogates; UxPlay expects a valid UTF-8 name.
                    if (Char.IsSurrogate(value))
                    {
                        if (!Char.IsHighSurrogate(value) || i + 1 == ReceiverName.Length || !Char.IsLowSurrogate(ReceiverName[i + 1]))
                        { error = "O nome deve conter caracteres Unicode validos."; break; }
                        i++;
                    }
                }
            }
            if (error == null && SessionLimitMinutes != 0 && SessionLimitMinutes != 15 &&
                SessionLimitMinutes != 30 && SessionLimitMinutes != 60)
                error = "Escolha um limite de 15, 30, 60 minutos ou sem limite.";
            return error == null;
        }
    }

    public sealed class SettingsStore
    {
        public string SettingsPath { get; private set; }
        public string LastError { get; private set; }

        public SettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSee", "settings.xml")) { }
        public SettingsStore(string settingsPath) { SettingsPath = Path.GetFullPath(settingsPath); }

        public AppSettings Load()
        {
            LastError = null;
            if (!File.Exists(SettingsPath)) return new AppSettings();
            try
            {
                if (new FileInfo(SettingsPath).Length > 16384) throw new InvalidDataException();
                XmlReaderSettings options = new XmlReaderSettings();
                options.DtdProcessing = DtdProcessing.Prohibit;
                options.XmlResolver = null;
                options.MaxCharactersInDocument = 16384;
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                using (XmlReader reader = XmlReader.Create(SettingsPath, options)) document.Load(reader);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "LetMeSee" || root.GetAttribute("version") != "1") throw new InvalidDataException();
                AppSettings settings = new AppSettings();
                settings.ReceiverName = Read(root, "ReceiverName");
                settings.Mute = Boolean.Parse(Read(root, "Mute"));
                settings.MinimizeToTray = Boolean.Parse(Read(root, "MinimizeToTray"));
                settings.EnableAnimation = Boolean.Parse(Read(root, "EnableAnimation"));
                XmlNode pinPreference = root.SelectSingleNode("RequirePin");
                settings.RequirePin = pinPreference == null || Boolean.Parse(pinPreference.InnerText);
                settings.SessionLimitMinutes = Int32.Parse(Read(root, "SessionLimitMinutes"), CultureInfo.InvariantCulture);
                string error;
                if (!settings.Validate(out error)) throw new InvalidDataException();
                return settings;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is XmlException || ex is FormatException || ex is OverflowException || ex is System.Security.SecurityException)) throw;
                LastError = "Nao foi possivel ler as preferencias. Foram usados valores seguros.";
                return new AppSettings();
            }
        }

        public bool Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            string error;
            if (!settings.Validate(out error)) { LastError = error; return false; }
            string temporary = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                XmlWriterSettings options = new XmlWriterSettings();
                options.Encoding = new UTF8Encoding(false);
                options.Indent = true;
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (XmlWriter writer = XmlWriter.Create(stream, options))
                {
                    writer.WriteStartElement("LetMeSee");
                    writer.WriteAttributeString("version", "1");
                    writer.WriteElementString("ReceiverName", settings.ReceiverName);
                    writer.WriteElementString("Mute", settings.Mute.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("MinimizeToTray", settings.MinimizeToTray.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("EnableAnimation", settings.EnableAnimation.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("RequirePin", settings.RequirePin.ToString(CultureInfo.InvariantCulture));
                    writer.WriteElementString("SessionLimitMinutes", settings.SessionLimitMinutes.ToString(CultureInfo.InvariantCulture));
                    writer.WriteEndElement();
                }
                if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
                else File.Move(temporary, SettingsPath);
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException)) throw;
                LastError = "Nao foi possivel salvar as preferencias locais.";
                return false;
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }

        private static string Read(XmlElement root, string name)
        {
            XmlNode value = root.SelectSingleNode(name);
            if (value == null) throw new InvalidDataException();
            return value.InnerText;
        }
    }

    public static class UxPlayArguments
    {
        // Direct ProcessStartInfo invocation: this string is never passed to cmd.exe or PowerShell.
        // Official options for the bundled version: https://github.com/FDH2/UxPlay/blob/v1.73.6/uxplay.1
        public static string Build(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            string error;
            if (!settings.Validate(out error)) throw new ArgumentException(error, "settings");
            return "-n " + Quote(settings.ReceiverName) + " -nh -m -p -vs d3d11videosink" + (settings.Mute ? " -as 0" : "");
        }

        // Windows CRT command-line quoting, including trailing backslashes and embedded quotes.
        public static string Quote(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '\"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                slashes = 0;
                result.Append(character);
            }
            result.Append('\\', slashes * 2);
            return result.Append('\"').ToString();
        }
    }
}
