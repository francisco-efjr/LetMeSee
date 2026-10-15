using System;
using System.IO;
using System.Xml;

namespace LetMeSee
{
    // Remember choices, never approvals, device serials, PINs or a completed checklist.
    internal static class EnvironmentStore
    {
        private static string FilePath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSee", "environment.xml"); } }
        public static PreparationSelection Load()
        {
            PreparationSelection result = new PreparationSelection();
            try
            {
                if (!File.Exists(FilePath) || new FileInfo(FilePath).Length > 4096) return result;
                XmlReaderSettings options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096 };
                using (XmlReader reader = XmlReader.Create(FilePath, options))
                {
                    reader.MoveToContent();
                    if (reader.Name != "Environment") return result;
                    DeviceFamily device; MirrorMethod method; ExperienceLevel experience;
                    if (Enum.TryParse(reader.GetAttribute("device"), out device) && Enum.TryParse(reader.GetAttribute("method"), out method) && Enum.TryParse(reader.GetAttribute("experience"), out experience))
                    {
                        PreparationSelection candidate = new PreparationSelection { Device = device, Method = method, Experience = experience };
                        string error; if (candidate.Validate(out error) && (method == MirrorMethod.AirPlay || method == MirrorMethod.AndroidUsb || method == MirrorMethod.AndroidWireless)) result = candidate;
                    }
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { } catch (XmlException) { }
            ApplyRuntimeDirectory(result); return result;
        }
        public static bool Save(PreparationSelection selection)
        {
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                using (XmlWriter writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                { writer.WriteStartElement("Environment"); writer.WriteAttributeString("device", selection.Device.ToString()); writer.WriteAttributeString("method", selection.Method.ToString()); writer.WriteAttributeString("experience", selection.Experience.ToString()); writer.WriteEndElement(); }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
                return true;
            }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        public static void ApplyRuntimeDirectory(PreparationSelection selection)
        {
            string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LetMeSee", "engines", "android");
            selection.AndroidRuntimeDirectory = Directory.Exists(local) ? local : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engines", "android");
        }
        public static string Describe(PreparationSelection selection)
        {
            return selection.Device == DeviceFamily.IOS ? "iPhone ou iPad · AirPlay" : selection.Method == MirrorMethod.AndroidUsb ? "Android · Cabo USB" : "Android · Wi-Fi pareado";
        }
    }
}
