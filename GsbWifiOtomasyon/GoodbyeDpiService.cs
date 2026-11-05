using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace GsbWifiOtomasyon
{
    public static class GoodbyeDpiService
    {
        public static bool IsRunning()
        {
            try
            {
                // 1. Hangi klasörden veya servisten başlatılmış olursa olsun Windows'ta çalışan goodbyedpi.exe var mı kontrol et
                if (Process.GetProcessesByName("goodbyedpi").Length > 0)
                    return true;

                // 2. Ek olarak GoodbyeDPI servis durumunu kontrol et
                string output = RunCommandSync("sc", "query \"GoodbyeDPI\"");
                if (!string.IsNullOrEmpty(output) && output.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch { }
            return false;
        }

        public static string GetExecutablePath()
        {
            string arch = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";
            string belgelerimDpiDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "GsbAccounts",
                "GoodbyeDpi",
                arch);
            string targetExe = Path.Combine(belgelerimDpiDir, "goodbyedpi.exe");

            try
            {
                if (!Directory.Exists(belgelerimDpiDir))
                    Directory.CreateDirectory(belgelerimDpiDir);

                string[] requiredFiles = arch == "x86_64"
                    ? new[] { "goodbyedpi.exe", "WinDivert.dll", "WinDivert64.sys" }
                    : new[] { "goodbyedpi.exe", "WinDivert.dll", "WinDivert32.sys", "WinDivert64.sys" };

                var assembly = Assembly.GetExecutingAssembly();
                foreach (string fileName in requiredFiles)
                {
                    string destFile = Path.Combine(belgelerimDpiDir, fileName);
                    if (!File.Exists(destFile))
                    {
                        string resourceName = $"GsbWifiOtomasyon.GoodbyeDpi.{arch}.{fileName}";
                        using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream != null)
                            {
                                using (FileStream fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                                {
                                    resourceStream.CopyTo(fileStream);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (File.Exists(targetExe))
                return targetExe;

            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoodbyeDpi", arch, "goodbyedpi.exe");
            if (File.Exists(localPath))
                return localPath;

            return Path.Combine(@"C:\Users\dmn\Downloads\GoodbyeDpi", arch, "goodbyedpi.exe");
        }

        public static async Task<bool> StartAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    string exePath = GetExecutablePath();
                    if (!File.Exists(exePath))
                        return false;

                    // service_install_dnsredir_turkey_alternative4_superonline.cmd ile birebir aynı komutlar
                    RunCommandSync("sc", "stop \"GoodbyeDPI\"");
                    RunCommandSync("sc", "delete \"GoodbyeDPI\"");
                    foreach (var proc in Process.GetProcessesByName("goodbyedpi"))
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }

                    string binPathArg = $"create \"GoodbyeDPI\" binPath= \"\\\"{exePath}\\\" -5 --dns-addr 77.88.8.8 --dns-port 1253 --dnsv6-addr 2a02:6b8::feed:0ff --dnsv6-port 1253\" start= \"auto\"";
                    RunCommandSync("sc", binPathArg);
                    RunCommandSync("sc", "description \"GoodbyeDPI\" \"Turkiye icin DNS zorlamasini kaldirir. Alternatif Metod 4\"");
                    RunCommandSync("sc", "start \"GoodbyeDPI\"");

                    await Task.Delay(400);
                    return IsRunning();
                }
                catch
                {
                    return false;
                }
            });
        }

        public static async Task<bool> StopAsync()
        {
            return await Task.Run(async () =>
            {
                try
                {
                    RunCommandSync("sc", "stop \"GoodbyeDPI\"");
                    RunCommandSync("sc", "delete \"GoodbyeDPI\"");
                    // WinDivert sürücüsünü kapatmadan önce goodbyedpi.exe işleminin tamamen kapandığından emin ol
                    foreach (var proc in Process.GetProcessesByName("goodbyedpi"))
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }
                    RunCommandSync("sc", "stop \"WinDivert\"");
                    RunCommandSync("sc", "delete \"WinDivert\"");
                    RunCommandSync("sc", "stop \"WinDivert14\"");
                    RunCommandSync("sc", "delete \"WinDivert14\"");
                    // GSB yerel DNS çözümlemesinin anında devreye girmesi için DNS önbelleğini temizle
                    RunCommandSync("ipconfig", "/flushdns");
                    await Task.Delay(600);
                    return !IsRunning();
                }
                catch
                {
                    return false;
                }
            });
        }

        private static string RunCommandSync(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);
                    return output;
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
