using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LetMeSee
{
    // Mo baile: semantic tokens, system fonts, 4px grid, state = symbol + text + color.
    internal static class DesignTokens
    {
        public static readonly bool HighContrast = SystemInformation.HighContrast;
        public static readonly Color Window = HighContrast ? SystemColors.Window : Color.FromArgb(17, 23, 23);
        public static readonly Color Surface = HighContrast ? SystemColors.Control : Color.FromArgb(26, 33, 32);
        public static readonly Color Raised = HighContrast ? SystemColors.Control : Color.FromArgb(34, 43, 41);
        public static readonly Color Border = HighContrast ? SystemColors.WindowText : Color.FromArgb(65, 77, 72);
        public static readonly Color Text = HighContrast ? SystemColors.WindowText : Color.FromArgb(245, 241, 229);
        public static readonly Color Muted = HighContrast ? SystemColors.WindowText : Color.FromArgb(177, 188, 179);
        public static readonly Color Accent = HighContrast ? SystemColors.Highlight : Color.FromArgb(235, 143, 93);
        public static readonly Color OnAccent = HighContrast ? SystemColors.HighlightText : Color.FromArgb(30, 24, 19);
        public static readonly Color Paper = HighContrast ? SystemColors.Window : Color.FromArgb(233, 222, 197);
        public static readonly Color Ink = HighContrast ? SystemColors.WindowText : Color.FromArgb(38, 43, 35);
        public static readonly Color Orange = HighContrast ? SystemColors.WindowText : Color.FromArgb(166, 66, 29);
        public static readonly Color Success = HighContrast ? SystemColors.WindowText : Color.FromArgb(170, 211, 153);
        public static readonly Color Danger = HighContrast ? SystemColors.WindowText : Color.FromArgb(255, 166, 150);
        public static bool MotionEnabled
        {
            get
            {
                bool enabled;
                return !HighContrast && Environment.OSVersion.Platform == PlatformID.Win32NT &&
                    SystemParametersInfo(0x1042, 0, out enabled, 0) && enabled;
            }
        }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool enabled, uint flags);

        public const int Space = 4;
        public const int CardRadius = 12;
        public const int ControlHeight = 40;
        public static readonly Font Body = new Font("Segoe UI", 10f);
        public static readonly Font Small = new Font("Segoe UI", 9f);
        public static readonly Font Heading = new Font("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 10f);
        public static readonly Font Brand = new Font("Segoe UI", 17f, FontStyle.Bold);
        public static readonly Font NavigationTitle = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font HeroTitle = new Font("Segoe UI", 28f, FontStyle.Bold);
        public static readonly Font CardTitle = new Font("Segoe UI", 13f, FontStyle.Bold);
        public static readonly Font Name = new Font("Segoe UI", 15f, FontStyle.Bold);
        public static readonly Font Step = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font Action = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font Caption = new Font("Consolas", 9f);
        public static readonly Font CaptionStrong = new Font("Consolas", 9f, FontStyle.Bold);
        public static readonly Font Fine = new Font("Consolas", 8.5f);
    }
}
