using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steam4NET;

internal class Program
{
    public class VersionInfo
    {
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public class Config
    {
        public List<int> AppIds { get; set; } = new List<int>();
        public bool MinimizeToTray { get; set; }
        public bool Autorun { get; set; }
        public bool CheckForUpdates { get; set; }
        public bool DownloadUpdate { get; set; }
    }

    private static DateTime _startTime;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private static NotifyIcon notifyIcon;
    private static Task taskOperation;
    private static ContextMenu contextMenu;
    private static ISteamClient012 _steamClient012;
    private static ISteamApps001 _steamApps001;
    private const int _secondsBetweenChecks = 15;
    private static BackgroundWorker _bwg;
    private const string VersionUrl = "https://raw.githubusercontent.com/retrojan/SBGR/refs/heads/main/version.json";
    private const string CurrentVersion = "2.38";
    private static DateTime lastExitTime;
    private static Process gameProcess;

    private static Config LoadConfig(string filePath)
    {
        try
        {
            return JsonConvert.DeserializeObject<Config>(File.ReadAllText(filePath));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading config: " + ex.Message);
            return null;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void _bwg_DoWork(object sender, DoWorkEventArgs e)
    {
        if (e.Argument == null) return;

        Process parentProcess = Process.GetProcesses().FirstOrDefault(o => o.Id == (int)e.Argument);
        if (parentProcess == null)
        {
            Console.WriteLine($"Could not find parent ID. Exiting in {_secondsBetweenChecks} seconds.");
            Thread.Sleep(TimeSpan.FromSeconds(_secondsBetweenChecks));
        }
        else
        {
            while (!_bwg.CancellationPending && !parentProcess.HasExited)
            {
                Thread.Sleep(TimeSpan.FromSeconds(_secondsBetweenChecks));
            }
        }
        Environment.Exit(1);
    }

    private static void ShowErrorN(string str)
    {
        Console.WriteLine("ERROR: " + str + "\n");
        Thread.Sleep(Timeout.Infinite);
    }

    private static void ShowError(string str)
    {
        Console.WriteLine("ERROR: " + str + "\n");
        Thread.Sleep(1000);
        Console.Clear();
    }

    private static bool ConnectToSteam()
    {
        while (true)
        {
            try
            {
                if (!Steamworks.Load(true))
                {
                    ShowError("Steamworks failed to load.");
                    continue;
                }

                _steamClient012 = Steamworks.CreateInterface<ISteamClient012>();
                if (_steamClient012 == null)
                {
                    ShowError("Failed to create Steam Client interface.");
                    continue;
                }

                int pipe = _steamClient012.CreateSteamPipe();
                if (pipe == 0)
                {
                    ShowError("Failed to create Steam pipe.");
                    continue;
                }

                int user = _steamClient012.ConnectToGlobalUser(pipe);
                if (user == 0)
                {
                    ShowError("Failed to connect to Steam user.");
                    continue;
                }

                _steamApps001 = _steamClient012.GetISteamApps<ISteamApps001>(user, pipe);
                if (_steamApps001 == null)
                {
                    ShowError("Failed to create Steam Apps interface.");
                    continue;
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowError("Unexpected error: " + ex.Message);
            }
        }
    }

    private static bool IsSteamRunning()
    {
        return Process.GetProcesses().Any(p => p.ProcessName.Equals("steam", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAlreadyRunning()
    {
        return Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1;
    }

    private static void CheckForUpdates(Config config)
    {
        if (!config.CheckForUpdates) return;

        try
        {
            using (var client = new WebClient())
            {
                string versionJson;
                try
                {
                    versionJson = client.DownloadString(VersionUrl);
                    var versionInfo = JsonConvert.DeserializeObject<VersionInfo>(versionJson);

                    Version currentVersion = Version.Parse(CurrentVersion);
                    Version latestVersion = Version.Parse(versionInfo.Version);

                    Console.Write("Your version: ");
                    Console.ForegroundColor = currentVersion.Equals(latestVersion)
                        ? ConsoleColor.Green
                        : ConsoleColor.Red;
                    Console.Write(CurrentVersion);
                    Console.ResetColor();

                    Console.Write(" | Server version: ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(versionInfo.Version + "\n");
                    Console.ResetColor();

                    if (config.DownloadUpdate && currentVersion < latestVersion)
                    {
                        Console.WriteLine("New version available. Downloading update...");
                        // Логика загрузки обновления
                    }
                }
                catch
                {
                    Console.Write("Your version: ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write(CurrentVersion);
                    Console.ResetColor();

                    Console.Write(" | Server version: ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("???\n");
                    Console.ResetColor();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error checking for updates: " + ex.Message);
            Console.ResetColor();
        }
    }
    private static void MinimizeToTray()
    {
        FreeConsole();
    }

    private static void TaskOperation()
    {
        notifyIcon.ContextMenu = contextMenu;
        notifyIcon.Visible = true;
        notifyIcon.ShowBalloonTip(500, "Windows App", "Windows Tray App is Running...", ToolTipIcon.Info);
    }

    private static void MenuItem_Click(object sender, EventArgs e)
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        Environment.Exit(0);
    }

    private static string GetGameName(uint appId)
    {
        if (_steamApps001 == null) return "Unknown";

        StringBuilder gameNameBuilder = new StringBuilder(256);
        return _steamApps001.GetAppData(appId, "name", gameNameBuilder) > 0 && gameNameBuilder.Length > 0
            ? gameNameBuilder.ToString()
            : "Unknown";
    }

    private static string GetFormattedTime()
    {
        TimeSpan elapsed = DateTime.Now - _startTime;
        List<string> timeParts = new List<string>();

        if (elapsed.Hours > 0) timeParts.Add($"{elapsed.Hours}h");
        if (elapsed.Minutes > 0) timeParts.Add($"{elapsed.Minutes}m");
        if (elapsed.Seconds > 0) timeParts.Add($"{elapsed.Seconds}s");

        return string.Join(" ", timeParts);
    }

    private static bool IsGameRunning(uint appId)
    {
        try
        {
            return Process.GetProcesses()
                .Any(p => p.ProcessName.Equals($"game{appId}", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error while checking if the game is running: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
            return false;
        }
    }

    private static void PrintStatus(string parameterName, bool status)
    {
        Console.Write(parameterName + ": ");
        Console.ForegroundColor = status ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine(status ? "Enabled" : "Disabled");
        Console.ResetColor();
    }

    private static async Task<int> GetPlayerCount(uint appId)
    {
        string url = $"https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={appId}";

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to get player count");
                }

                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                return (int)json["response"]["player_count"];
            }
            catch
            {
                return -1;
            }
        }
    }

    private static ConsoleColor GetRandomConsoleColor(Random random)
    {
        ConsoleColor[] colors = (ConsoleColor[])Enum.GetValues(typeof(ConsoleColor));
        ConsoleColor randomColor;

        do
        {
            randomColor = colors[random.Next(colors.Length)];
        }
        while (randomColor == ConsoleColor.Black ||
               randomColor == ConsoleColor.Gray ||
               randomColor == ConsoleColor.DarkGray ||
               randomColor == ConsoleColor.White ||
               randomColor == Console.BackgroundColor);

        return randomColor;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        _startTime = DateTime.Now;
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        if (!File.Exists(configPath))
        {
            ShowErrorN("Configuration file 'config.json' not found.");
            return;
        }

        Config config = LoadConfig(configPath);
        if (config == null || config.AppIds.Count == 0)
        {
            ShowError("Invalid configuration.");
            return;
        }

        Console.WriteLine("");

        // Реализация автозагрузки через реестр
        string appPath = Process.GetCurrentProcess().MainModule.FileName;
        const string regKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";

        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(regKey, true))
        {
            if (config.Autorun)
            {
                // Добавляем в автозагрузку
                key.SetValue("SBGR", appPath);
            }
            else
            {
                // Удаляем из автозагрузки
                if (key.GetValue("SBGR") != null)
                {
                    key.DeleteValue("SBGR");
                }
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        foreach (int appId in config.AppIds)
        {
            Process.GetProcesses()
                .FirstOrDefault(p => p.ProcessName.Equals($"game{(uint)appId}", StringComparison.OrdinalIgnoreCase));

            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            bool shouldRestart;
            do
            {
                shouldRestart = false;
                try
                {
                    _bwg = new BackgroundWorker { WorkerSupportsCancellation = true };
                    _bwg.DoWork += _bwg_DoWork;

                    if (appId == 0) return;

                    while (!IsSteamRunning())
                    {
                        Console.WriteLine("Steam is not running. Waiting for Steam to launch...");
                        Thread.Sleep(1000);
                        Console.Clear();
                    }

                    if (ConnectToSteam())
                    {
                        int parentProcessId = -1;
                        if (args.Length >= 2 && int.TryParse(args[1], out parentProcessId) && parentProcessId != -1)
                        {
                            _bwg.RunWorkerAsync(parentProcessId);
                        }

                        if (config.MinimizeToTray)
                        {
                            contextMenu = new ContextMenu();
                            MenuItem menuItem = new MenuItem
                            {
                                Text = "Close"
                            };
                            menuItem.Click += MenuItem_Click;
                            contextMenu.MenuItems.Add(menuItem);

                            notifyIcon = new NotifyIcon
                            {
                                Text = GetGameName((uint)appId) ?? "",
                                Icon = new Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico")),
                                Visible = true
                            };

                            taskOperation = Task.Run(() => TaskOperation());
                            MinimizeToTray();
                        }
                        else
                        {
                            Console.Title = GetGameName((uint)appId);
                            Console.Clear();

                            Console.WriteLine(@"
                                  ▄█▀▀▀█▄████▀▀▀██▄   ▄▄█▀▀▀█▄█▀███▀▀▀██▄
                                 ▄██    ▀█ ██    ██  ██▀     ▀█  ██   ▀██▄
                                 ▀███▄     ██    ██ ██▀       ▀  ██   ▄██
                                   ▀█████▄ ██▀▀▀█▄▄ ██           ███████
                                 ▄     ▀██ ██    ▀█  ██    ▀████ ██  ██▄
                                 ██     ██ ██    ▄█  ██▄     ██  ██   ▀██▄
                                 █▀█████▀▄████████    ▀▀███████▄████▄ ▄███▄
                            ");

                            Console.ResetColor();
                            Console.Write("                                            ");
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.Write("");
                            Console.ResetColor();
                            Console.Write("    ");
                            Console.Write("Re");
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.Write("Tro");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("jan");
                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine();

                            Console.Write("Updates: ");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write("https://github.com/ReTrojan/SBGR\n");
                            Console.ResetColor();

                            if (config.CheckForUpdates)
                            {
                                CheckForUpdates(config);
                            }

                            Console.Write("\n");
                            Random random = new Random();

                            Console.Write("Game: ");
                            Console.ForegroundColor = GetRandomConsoleColor(random);
                            Console.Write(GetGameName((uint)appId));
                            Console.ResetColor();

                            Console.Write(" | AppID: ");
                            Console.ForegroundColor = GetRandomConsoleColor(random);
                            Console.Write((uint)appId);
                            Console.ResetColor();

                            Console.WriteLine();
                            Console.Write("Players in game: ");
                            Console.ForegroundColor = ConsoleColor.DarkCyan;
                            Console.Write(GetPlayerCount((uint)appId).GetAwaiter().GetResult());
                            Console.ResetColor();
                            Console.WriteLine();
                            Console.WriteLine();

                            PrintStatus("Minimize to Tray", config.MinimizeToTray);
                            PrintStatus("Autorun", config.Autorun);
                            PrintStatus("Check For Updates", config.CheckForUpdates);

                            Console.WriteLine();
                            Console.WriteLine();
                            Console.Write("");
                        }

                        Application.Run();
                        Thread.Sleep(Timeout.Infinite);

                        if (_bwg.IsBusy)
                        {
                            _bwg.CancelAsync();
                        }
                    }
                    else
                    {
                        ShowError("Failed to connect to Steam. Exiting.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message + ". Restarting...");
                    shouldRestart = true;
                    Thread.Sleep(TimeSpan.FromSeconds(10.0));
                }
            }
            while (shouldRestart);
        }
        Application.Run();
    }
}