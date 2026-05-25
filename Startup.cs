using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SBQuickSwitch
{
    /// <summary>
    /// Manages the per-user Startup-folder shortcut that auto-launches the tray app at logon.
    /// </summary>
    internal static class Startup
    {
        private static string ShortcutPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "SBQuickSwitch.lnk");

        public static bool IsEnabled() => File.Exists(ShortcutPath);

        public static void Enable()
        {
            string exePath = Assembly.GetEntryAssembly().Location;
            CreateShortcut(ShortcutPath, exePath);
        }

        public static void Disable()
        {
            try { if (File.Exists(ShortcutPath)) File.Delete(ShortcutPath); }
            catch (IOException) { /* file in use; ignore */ }
        }

        // Build a .lnk via WScript.Shell COM (Windows-provided since XP) using late binding,
        // so we don't need a reference to the WSH interop assembly.
        private static void CreateShortcut(string lnkPath, string targetExe)
        {
            Type wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType == null)
                throw new InvalidOperationException("WScript.Shell COM class not available.");

            object wsh = Activator.CreateInstance(wshType);
            object shortcut = null;
            try
            {
                shortcut = wshType.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, wsh, new object[] { lnkPath });
                Type sType = shortcut.GetType();
                sType.InvokeMember("TargetPath",       BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
                sType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) });
                sType.InvokeMember("Description",      BindingFlags.SetProperty, null, shortcut, new object[] { "Sound Blaster AE-7 Quick Switch" });
                sType.InvokeMember("IconLocation",     BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });
                sType.InvokeMember("Save",             BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                Marshal.ReleaseComObject(wsh);
            }
        }
    }
}
