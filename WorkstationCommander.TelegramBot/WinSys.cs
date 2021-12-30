using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

#pragma warning disable CS8604 // Possible null reference argument.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1416 // Validate platform compatibility

namespace WorkstationCommander.TelegramBot
{
    public static class WinSys
    {
        public static Func<bool, Task<bool>>? LockMessageFunc { get; set; }

        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation(); // https://stackoverflow.com/questions/1263047/lock-windows-workstation-programmatically-in-c-sharp

        [DllImport("User32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii); // https://social.msdn.microsoft.com/Forums/vstudio/en-US/58c5eeda-be1f-4f3e-befa-f2f235358fec/how-to-get-the-system-idle-time-in-c-windows-app

        public static bool lockState = false;

        // Sets up event listener. This is the only way to get lock status
        // https://stackoverflow.com/a/604042/3782147
        public static void LockEventSetup()
        {
            SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);

            void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
            {
                if (e.Reason == SessionSwitchReason.SessionLock)
                {
                    LockMessageFunc?.Invoke(true);
                    lockState = true;
                }
                else if (e.Reason == SessionSwitchReason.SessionUnlock)
                {
                    LockMessageFunc?.Invoke(false);
                    lockState = false;
                }
            }
        }

        // Get the system up time
        // https://codesnippets.fesslersoft.de/how-to-get-the-system-uptime-in-c-and-vb-net/
        public static string GetSystemUpTimeInfo()
        {
            try
            {
                var time = GetSystemUpTime();
                var upTime = string.Format("{0:D2}d:{1:D2}h:{2:D2}m:{3:D2}s:{4:D3}ms", time.Days, time.Hours, time.Minutes, time.Seconds, time.Milliseconds);

                return upTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSystemUpTimeInfo() Error: {ex.Message}");

                return string.Empty;
            }
        }

        // Get System running time
        private static TimeSpan GetSystemUpTime()
        {
            try
            {
                using (var uptime = new PerformanceCounter("System", "System Up Time"))
                {
                    uptime.NextValue();

                    return TimeSpan.FromSeconds(uptime.NextValue());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSystemUpTime() Error: {ex.Message}");

                return new TimeSpan(0, 0, 0, 0);
            }
        }

        // Get system idle time
        public static string GetIdleTime()
        {
            var lastInPut = new LASTINPUTINFO();
            lastInPut.cbSize = (uint)Marshal.SizeOf(lastInPut);
            _ = GetLastInputInfo(ref lastInPut);

            var time = TimeSpan.FromMilliseconds(Environment.TickCount - lastInPut.dwTime);
            var idleTime = string.Format("{0:D2}d:{1:D2}h:{2:D2}m:{3:D2}s:{4:D3}ms", time.Days, time.Hours, time.Minutes, time.Seconds, time.Milliseconds);

            return idleTime;
        }

        public static string GetAssemblyVersion()
        {
            return Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version.ToString();
        }

        // https://stackoverflow.com/a/27376368/3782147
        public static string GetLocalIpAddress()
        {
            var localIP = string.Empty;
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }
            return localIP;
        }

        // https://stackoverflow.com/posts/34192554/timeline
        public static string GetPublicIpAddress(string serviceUrl = "https://ipinfo.io/ip")
        {
            return IPAddress.Parse(new WebClient().DownloadString(serviceUrl)).ToString();
        }
    }
}

#pragma warning restore CA1416 // Validate platform compatibility
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
