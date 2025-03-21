using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
// No external TaskScheduler library needed

namespace HLLServerSeeder
{
    public partial class MainWindow : Window
    {
        // Configuration
        private string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "hll-seeder");
        private string configPath => Path.Combine(installDir, "config.json");
        private int seededThreshold = 75;
        private string hllGamePath = @"C:\Program Files (x86)\Steam\steamapps\common\Hell Let Loose\HLL-Win64-Shipping.exe";
        private string hllSteamUrl = "steam://run/686810";
        private bool sleepAfterSeeding = false;
        private int scheduleHour = 12;
        private int scheduleMinute = 0;
private bool isInitializing = true;
        // Server details
        private readonly List<ServerInfo> servers = new List<ServerInfo>();

        // HTTP client
        private readonly HttpClient httpClient = new HttpClient();

        // UI update timer
        private DispatcherTimer uiTimer;
        private Process gameProcess;
        private bool isSeeding = false;
        private ServerInfo currentServer;

        public MainWindow()
        {
            InitializeComponent();

            // Make sure we are not creating multiple HTTP clients
            httpClient = new HttpClient();

            // Configure HTTP client settings
            httpClient.Timeout = TimeSpan.FromSeconds(20);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) HLL-Server-Seeder/1.0");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");


            servers.Add(new ServerInfo
            {
                Name = "CTRL",
                DisplayName = "Ctrl Alt Defeat[Hellfire",
                RconUrl = "http://192.154.226.208:8010/api/get_public_info"
            });

            // Create default server list with real API endpoints
            servers.Add(new ServerInfo
            {
                Name = "ROTN",
                DisplayName = "=ROTN= | discord",
                RconUrl = "https://rotn-stats.crcon.cc/api/get_pubsclic_info"
            });

            servers.Add(new ServerInfo
            {
                Name = "SYN",
                DisplayName = "Syndicate | US East",
                RconUrl = "https://stats.syn.team/api/get_public_info"
            });



            // Initialize early non-UI settings
            LoadConfig();
            CreateInstallDirectory();

            // Wait for window to fully load before accessing UI elements
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Now UI is fully loaded, we can access all controls safely
            SetupUI();

            // Initial log message
            LogMessage("Application started");

            // Set up timer for UI updates - start immediately
            uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();

            // Run initial server check asynchronously
            _ = UpdateServerListReal();
        }


        private void UiTimer_Tick(object sender, EventArgs e)
        {
            // Run server update without awaiting to avoid UI freezes
            _ = UpdateServerListReal();
        }

        private void ScheduleTime_Changed(object sender, SelectionChangedEventArgs e)
        {
            // Skip if we're still initializing
            if (isInitializing) return;

            try
            {
                // Get selected values
                ComboBoxItem hourItem = cbScheduleHour?.SelectedItem as ComboBoxItem;
                ComboBoxItem minuteItem = cbScheduleMinute?.SelectedItem as ComboBoxItem;

                if (hourItem != null && minuteItem != null)
                {
                    // Parse the values
                    if (int.TryParse(hourItem.Content.ToString(), out int hour) &&
                        int.TryParse(minuteItem.Content.ToString(), out int minute))
                    {
                        scheduleHour = hour;
                        scheduleMinute = minute;
                        SaveConfig();

                        LogMessage($"Schedule time set to {scheduleHour:D2}:{scheduleMinute:D2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ScheduleTime_Changed: {ex.Message}");
            }
        }

        private void SetupUI()
        {
            isInitializing = true;

            tbGamePath.Text = hllGamePath;
            cbSleepAfterSeeding.IsChecked = sleepAfterSeeding;
            tbThreshold.Text = seededThreshold.ToString();

            // Set schedule time comboboxes
            if (cbScheduleHour != null && cbScheduleMinute != null)
            {
                // Set to closest valid indices
                cbScheduleHour.SelectedIndex = Math.Min(scheduleHour, 23);

                // Find the closest minute value (0, 15, 30, 45)
                int minuteIndex = 0;
                if (scheduleMinute >= 0 && scheduleMinute < 15) minuteIndex = 0;
                else if (scheduleMinute >= 15 && scheduleMinute < 30) minuteIndex = 1;
                else if (scheduleMinute >= 30 && scheduleMinute < 45) minuteIndex = 2;
                else minuteIndex = 3;

                cbScheduleMinute.SelectedIndex = minuteIndex;
            }

            isInitializing = false;
        }


        private void CreateInstallDirectory()
        {
            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
            }
        }

        private async Task QueryRealServerData(ServerInfo server)
        {
            try
            {
                // Add timeout to avoid hanging on unreachable servers
                using (var cts = new System.Threading.CancellationTokenSource(15000)) // 15 second timeout
                {
                    // Make HTTP GET request
                    HttpResponseMessage response = await httpClient.GetAsync(server.RconUrl, cts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonContent = await response.Content.ReadAsStringAsync();

                        // Log a sample of the response for debugging
                        string debugSample = jsonContent.Length > 100
                            ? jsonContent.Substring(0, 100) + "..."
                            : jsonContent;

                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"Received data from {server.Name}: {debugSample}");
                        });

                        // Parse the JSON with a more robust approach
                        using (JsonDocument jsonDoc = JsonDocument.Parse(jsonContent,
                               new JsonDocumentOptions { AllowTrailingCommas = true }))
                        {
                            var root = jsonDoc.RootElement;

                            if (root.TryGetProperty("result", out var result))
                            {
                                // Extract player count data
                                if (result.TryGetProperty("player_count", out var playerCount))
                                {
                                    server.TotalCount = playerCount.GetInt32();
                                }

                                // Extract player count by team
                                if (result.TryGetProperty("player_count_by_team", out var teamCounts))
                                {
                                    if (teamCounts.TryGetProperty("allied", out var allied))
                                    {
                                        server.AlliedCount = allied.GetInt32();
                                    }

                                    if (teamCounts.TryGetProperty("axis", out var axis))
                                    {
                                        server.AxisCount = axis.GetInt32();
                                    }
                                }

                                // Extract time remaining - handle both string and number types
                                if (result.TryGetProperty("time_remaining", out var timeRemaining))
                                {
                                    // Handle different types of values for time_remaining
                                    if (timeRemaining.ValueKind == JsonValueKind.String)
                                    {
                                        server.TimeRemaining = timeRemaining.GetString();
                                    }
                                    else if (timeRemaining.ValueKind == JsonValueKind.Number)
                                    {
                                        // Convert number to a time string HH:MM:SS
                                        double secondsDouble = timeRemaining.GetDouble();
                                        TimeSpan timeSpan = TimeSpan.FromSeconds(secondsDouble);
                                        server.TimeRemaining = timeSpan.ToString(@"hh\:mm\:ss");
                                    }
                                    else
                                    {
                                        server.TimeRemaining = "Unknown";
                                    }
                                }

                                // Update server status
                                server.IsOnline = true;

                                // Log status change if needed
                                if (!server.WasOnline)
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        LogMessage($"Server {server.Name} is now online with {server.TotalCount} players");
                                    });
                                }
                                server.WasOnline = true;
                            }
                            else
                            {
                                throw new Exception("Invalid JSON response format (missing 'result' property)");
                            }
                        }
                    }
                    else
                    {
                        throw new HttpRequestException($"Server returned error status: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"Error querying {server.Name}: {ex.Message}");
                });

                // Re-throw to be handled by the calling method
                throw;
            }
        }

        private async Task UpdateServerListReal()
        {
            try
            {
                // Process each server and update the UI
                foreach (var server in servers)
                {
                    try
                    {
                        await QueryRealServerData(server);

                        // Update the UI with the new server data
                        Dispatcher.Invoke(() =>
                        {
                            // Force ListView to update
                            var updatedItem = new ServerListItem
                            {
                                Name = server.Name,
                                AlliedCount = server.AlliedCount,
                                AxisCount = server.AxisCount,
                                TotalCount = server.TotalCount,
                                Status = server.GetStatusText(),
                                StatusColor = server.GetStatusColor()
                            };

                            // Clear and reload all items to ensure UI refreshes
                            bool found = false;
                            for (int i = 0; i < lvServers.Items.Count; i++)
                            {
                                var item = lvServers.Items[i];
                                if (item is ServerListItem existing && existing.Name == server.Name)
                                {
                                    // Replace the item
                                    lvServers.Items.RemoveAt(i);
                                    lvServers.Items.Insert(i, updatedItem);
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                lvServers.Items.Add(updatedItem);
                            }
                        });

                        // Add a small delay to avoid UI freezing
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"Error updating {server.Name}: {ex.Message}");

                            // Update server status to show it's unreachable
                            server.IsOnline = false;
                            server.WasOnline = false;

                            // Update UI to show unreachable status
                            var updatedItem = new ServerListItem
                            {
                                Name = server.Name,
                                AlliedCount = 0,
                                AxisCount = 0,
                                TotalCount = 0,
                                Status = "Unreachable",
                                StatusColor = Brushes.Red
                            };

                            // Find and update or add the item
                            bool found = false;
                            for (int i = 0; i < lvServers.Items.Count; i++)
                            {
                                var item = lvServers.Items[i];
                                if (item is ServerListItem existing && existing.Name == server.Name)
                                {
                                    // Replace the item
                                    lvServers.Items.RemoveAt(i);
                                    lvServers.Items.Insert(i, updatedItem);
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                lvServers.Items.Add(updatedItem);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"Error updating server list: {ex.Message}");
                });
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ConfigSettings>(json);

                    hllGamePath = config.HllGamePath;
                    sleepAfterSeeding = config.SleepAfterSeeding;
                    seededThreshold = config.SeededThreshold;

                    // Load schedule time settings
                    scheduleHour = config.ScheduleHour;
                    scheduleMinute = config.ScheduleMinute;

                    // Update server list if present in config
                    if (config.Servers != null && config.Servers.Count > 0)
                    {
                        servers.Clear();
                        servers.AddRange(config.Servers);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Update your SaveConfig method to save the schedule time
        private void SaveConfig()
        {
            try
            {
                var config = new ConfigSettings
                {
                    HllGamePath = hllGamePath,
                    SleepAfterSeeding = sleepAfterSeeding,
                    SeededThreshold = seededThreshold,
                    Servers = servers,
                    ScheduleHour = scheduleHour,
                    ScheduleMinute = scheduleMinute
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void cbSleepAfterSeeding_Checked(object sender, RoutedEventArgs e)
        {
            sleepAfterSeeding = cbSleepAfterSeeding.IsChecked ?? false;
            SaveConfig();
        }

        private void cbSleepAfterSeeding_Unchecked(object sender, RoutedEventArgs e)
        {
            sleepAfterSeeding = false;
            SaveConfig();
        }
        private async Task UpdateServerListSimple()
        {
            try
            {
                // Process each server and update the UI immediately
                foreach (var server in servers)
                {
                    try
                    {
                        // Generate simulated data instead of making API calls
                        var random = new Random();
                        server.TotalCount = random.Next(50, 100);
                        server.AlliedCount = random.Next(20, 40);
                        server.AxisCount = server.TotalCount - server.AlliedCount;
                        server.TimeRemaining = "1:30:00";
                        server.IsOnline = true;
                        server.WasOnline = true;

                        // Update the UI with the new server data
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"Server {server.Name} is online with {server.TotalCount} players (simulated)");

                            // Find existing item or create a new one
                            bool found = false;
                            foreach (ServerListItem item in lvServers.Items)
                            {
                                if (item.Name == server.Name)
                                {
                                    item.AlliedCount = server.AlliedCount;
                                    item.AxisCount = server.AxisCount;
                                    item.TotalCount = server.TotalCount;
                                    item.Status = server.GetStatusText();
                                    item.StatusColor = server.GetStatusColor();
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                lvServers.Items.Add(new ServerListItem
                                {
                                    Name = server.Name,
                                    AlliedCount = server.AlliedCount,
                                    AxisCount = server.AxisCount,
                                    TotalCount = server.TotalCount,
                                    Status = server.GetStatusText(),
                                    StatusColor = server.GetStatusColor()
                                });
                            }
                        });

                        // Add a small delay to avoid UI freezing
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogMessage($"Error updating {server.Name}: {ex.Message}");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogMessage($"Error updating server list: {ex.Message}");
                });
            }
        }

        private async Task UpdateServerInfo(ServerInfo server)
        {
            try
            {
                // Use temporary test endpoints that will work reliably
                bool isTestEndpoint = server.RconUrl.Contains("jsonplaceholder");

                // Add timeout to avoid hanging on unreachable servers
                using (var cts = new System.Threading.CancellationTokenSource(15000)) // 15 second timeout
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LogMessage($"Querying {server.Name} server status...");
                    });

                    // Make HTTP GET request
                    HttpResponseMessage response = null;
                    try
                    {
                        response = await httpClient.GetAsync(server.RconUrl, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LogMessage($"HTTP request failed for {server.Name}: {ex.Message}");
                        });
                        throw;
                    }

                    if (response != null && response.IsSuccessStatusCode)
                    {
                        try
                        {
                            string jsonContent = await response.Content.ReadAsStringAsync();

                            JsonDocument jsonDoc;

                            // For test endpoints, convert the response to our expected format
                            if (isTestEndpoint)
                            {
                                // Generate random test data
                                var random = new Random();
                                server.TotalCount = random.Next(50, 100);
                                server.AlliedCount = random.Next(20, 40);
                                server.AxisCount = server.TotalCount - server.AlliedCount;
                                server.TimeRemaining = "1:30:00";
                                server.IsOnline = true;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    LogMessage($"Test server {server.Name} is online with {server.TotalCount} players (simulated)");
                                });

                                server.WasOnline = true;
                                return;
                            }
                            // For real endpoints, try to parse the JSON normally
                            else
                            {
                                try
                                {
                                    jsonDoc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { AllowTrailingCommas = true });
                                }
                                catch (JsonException)
                                {
                                    // If JSON parsing fails, log and throw
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        LogMessage($"Invalid JSON from {server.Name} server");
                                    });
                                    throw;
                                }

                                var root = jsonDoc.RootElement;

                                if (root.TryGetProperty("result", out var result))
                                {
                                    if (result.TryGetProperty("player_count", out var playerCount))
                                    {
                                        server.TotalCount = playerCount.GetInt32();
                                    }

                                    if (result.TryGetProperty("player_count_by_team", out var teamCounts))
                                    {
                                        if (teamCounts.TryGetProperty("allied", out var allied))
                                        {
                                            server.AlliedCount = allied.GetInt32();
                                        }

                                        if (teamCounts.TryGetProperty("axis", out var axis))
                                        {
                                            server.AxisCount = axis.GetInt32();
                                        }
                                    }

                                    if (result.TryGetProperty("time_remaining", out var timeRemaining))
                                    {
                                        server.TimeRemaining = timeRemaining.GetString();
                                    }

                                    server.IsOnline = true;
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        LogMessage($"Server {server.Name} is online with {server.TotalCount} players");
                                    });
                                    server.WasOnline = true;
                                }
                                else
                                {
                                    throw new Exception("Invalid JSON response format");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                LogMessage($"Error processing data from {server.Name}: {ex.Message}");
                            });
                            throw;
                        }
                    }
                    else
                    {
                        string statusCode = response != null ? response.StatusCode.ToString() : "No response";
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            LogMessage($"HTTP error from {server.Name} server: {statusCode}");
                        });
                        throw new HttpRequestException($"Bad status code: {response?.StatusCode}");
                    }
                }
            }
            catch (Exception)
            {
                server.IsOnline = false;
                server.TotalCount = 0;
                server.AlliedCount = 0;
                server.AxisCount = 0;
                server.WasOnline = false;
            }
        }

        private async Task StartSeeding()
        {
            if (isSeeding)
            {
                MessageBox.Show("Already seeding a server", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Disable UI elements
            btnStartSeeding.IsEnabled = false;
            btnStopSeeding.IsEnabled = true;
            isSeeding = true;

            LogMessage("Starting seeding process...");
            await StartSeedingNextServer();
        }

        private async Task StartSeedingNextServer()
        {
            // Find first server that needs seeding
            var serverToSeed = servers.FirstOrDefault(s => s.TotalCount < seededThreshold);

            if (serverToSeed == null)
            {
                LogMessage("All servers are already seeded. Stopping.");
                await StopSeeding();

                if (sleepAfterSeeding)
                {
                    LogMessage("Putting computer to sleep...");
                    await Task.Delay(3000);
                    PutComputerToSleep();
                }

                return;
            }

            currentServer = serverToSeed;
            LogMessage($"Seeding server {serverToSeed.Name} ({serverToSeed.TotalCount}/{seededThreshold} players)");

            // Determine which team to join
            string teamToJoin = serverToSeed.AlliedCount <= serverToSeed.AxisCount ? "Allied" : "Axis";
            LogMessage($"Joining as {teamToJoin} to balance teams ({serverToSeed.AlliedCount} Allied vs {serverToSeed.AxisCount} Axis)");

            // Launch game
            await LaunchGame(serverToSeed, teamToJoin);
        }

        private async Task LaunchGame(ServerInfo server, string team)
        {
            try
            {
                // Check if HLL is already running
                var existingProcesses = Process.GetProcessesByName("HLL-Win64-Shipping");
                if (existingProcesses.Length > 0)
                {
                    LogMessage("HLL is already running. Closing it first...");
                    foreach (var proc in existingProcesses)
                    {
                        try { proc.Kill(); } catch { }
                    }
                    await Task.Delay(5000);
                }

                // Launch game
                if (File.Exists(hllGamePath))
                {
                    LogMessage($"Launching HLL from path: {hllGamePath}");
                    gameProcess = Process.Start(hllGamePath);
                }
                else
                {
                    LogMessage($"Game path not found. Launching via Steam URL: {hllSteamUrl}");
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = hllSteamUrl,
                        UseShellExecute = true
                    });

                    // Wait for game to start
                    await Task.Delay(10000);

                    // Try to find the game process
                    gameProcess = Process.GetProcessesByName("HLL-Win64-Shipping").FirstOrDefault();
                }

                if (gameProcess == null)
                {
                    LogMessage("Failed to detect game process. Make sure HLL is installed.");
                    await StopSeeding();
                    return;
                }

                // Show manual instructions
                MessageBox.Show(
                    $"Please perform these steps manually:\n\n" +
                    $"1. Navigate to the server browser\n" +
                    $"2. Find and join: {server.DisplayName}\n" +
                    $"3. Select the {team} team\n\n" +
                    $"The program will continue monitoring in the background.",
                    "Manual Action Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                LogMessage("Waiting for player count to reach threshold...");
            }
            catch (Exception ex)
            {
                LogMessage($"Error launching game: {ex.Message}");
                await StopSeeding();
            }
        }

        private async Task StopSeeding()
        {
            btnStartSeeding.IsEnabled = true;
            btnStopSeeding.IsEnabled = false;
            isSeeding = false;

            LogMessage("Stopping seeding process...");

            // Close game if it's running
            if (gameProcess != null && !gameProcess.HasExited)
            {
                try
                {
                    LogMessage("Closing game...");
                    gameProcess.Kill();
                    await Task.Delay(2000);
                    LogMessage("Game closed successfully");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error closing game: {ex.Message}");
                }
            }
            else
            {
                // Try to find and close HLL process if our reference is null
                try
                {
                    var processes = Process.GetProcessesByName("HLL-Win64-Shipping");
                    if (processes.Length > 0)
                    {
                        LogMessage("Closing game...");
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                        }
                        await Task.Delay(2000);
                        LogMessage("Game closed successfully");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error closing game: {ex.Message}");
                }
            }

            gameProcess = null;
            currentServer = null;

            if (sleepAfterSeeding)
            {
                LogMessage("Putting computer to sleep as requested...");
                await Task.Delay(5000);

                try
                {
                    Process.Start("powercfg", "-h off").WaitForExit();
                    Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    Process.Start("powercfg", "-h on");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error putting computer to sleep: {ex.Message}");
                }
            }

            LogMessage("Seeding stopped");
        }

        private async void btnStopSeeding_Click(object sender, RoutedEventArgs e)
        {
            await StopSeeding();
        }

        private void PutComputerToSleep()
        {
            try
            {
                // Disable/enable hibernation to ensure sleep works properly
                Process.Start("powercfg", "-h off").WaitForExit();
                Process.Start("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                Process.Start("powercfg", "-h on");
            }
            catch (Exception ex)
            {
                LogMessage($"Error putting computer to sleep: {ex.Message}");
            }
        }

        private void CreateScheduledTask()
        {
            try
            {
                // Get the path to this executable
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // Format time for schtasks
                string timeString = $"{scheduleHour:D2}:{scheduleMinute:D2}";

                // Create the task using schtasks command
                Process process = new Process();
                process.StartInfo.FileName = "schtasks.exe";
                process.StartInfo.Arguments = $"/create /tn \"HLL Server Seeder\" /tr \"\\\"{exePath}\\\" autostart\" /sc daily /st {timeString} /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Configure additional settings using separate commands
                    Process.Start("schtasks.exe", "/change /tn \"HLL Server Seeder\" /DISALLOWSTARTIFONBATTERIES:FALSE").WaitForExit();
                    Process.Start("schtasks.exe", "/change /tn \"HLL Server Seeder\" /WAKETORUN:TRUE").WaitForExit();

                    LogMessage($"Scheduled task created to run daily at {timeString}");
                    MessageBox.Show($"Task scheduled to run daily at {timeString}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"Error creating scheduled task: {error}");
                    MessageBox.Show($"Failed to create scheduled task: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating scheduled task: {ex.Message}");
                MessageBox.Show($"Failed to create scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteScheduledTask()
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "schtasks.exe";
                process.StartInfo.Arguments = "/delete /tn \"HLL Server Seeder\" /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    LogMessage("Scheduled task removed");
                    MessageBox.Show("Scheduled task removed", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"Error removing scheduled task: {error}");
                    MessageBox.Show($"Failed to remove scheduled task: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error removing scheduled task: {ex.Message}");
                MessageBox.Show($"Failed to remove scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                // Always use the Dispatcher from the current instance
                Dispatcher.Invoke(() =>
                {
                    // Check if tbLog exists before using it
                    if (tbLog != null)
                    {
                        string timestamp = DateTime.Now.ToString("HH:mm:ss");
                        tbLog.AppendText($"[{timestamp}] {message}\n");
                        tbLog.ScrollToEnd();
                    }
                    else
                    {
                        // If tbLog doesn't exist yet, just write to debug output
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
                    }
                });
            }
            catch (Exception ex)
            {
                // Last resort - if we can't log to the UI, at least log to Debug
                Debug.WriteLine($"Error logging to UI: {ex.Message}");
                Debug.WriteLine($"Original message: {message}");
            }
        }

        #region Event Handlers

        private async void btnStartSeeding_Click(object sender, RoutedEventArgs e)
        {
            // Disable UI elements while seeding
            btnStartSeeding.IsEnabled = false;
            btnStopSeeding.IsEnabled = true;
            isSeeding = true;

            LogMessage("Starting seeding process...");

            try
            {
                // Get the first server that needs seeding
                var serverToSeed = servers.FirstOrDefault(s => s.IsOnline && s.TotalCount < seededThreshold);

                if (serverToSeed == null)
                {
                    LogMessage("No servers currently need seeding. Will check again soon.");
                    currentServer = null;
                    btnStartSeeding.IsEnabled = true;
                    btnStopSeeding.IsEnabled = false;
                    isSeeding = false;
                    return;
                }

                currentServer = serverToSeed;
                string teamToJoin = serverToSeed.AlliedCount <= serverToSeed.AxisCount ? "Allied" : "Axis";

                LogMessage($"Seeding {serverToSeed.Name} server as {teamToJoin} team");
                LogMessage($"Current player count: {serverToSeed.TotalCount}/{seededThreshold} ({serverToSeed.AlliedCount} Allied, {serverToSeed.AxisCount} Axis)");

                // Check if HLL is already running
                var existingProcesses = Process.GetProcessesByName("HLL-Win64-Shipping");
                if (existingProcesses.Length > 0)
                {
                    LogMessage("HLL is already running. Closing it first...");
                    foreach (var proc in existingProcesses)
                    {
                        try { proc.Kill(); } catch { }
                    }
                    await Task.Delay(5000);
                }

                // Create automation instance
                var automation = new HLLAutomation(LogMessage);

                // Get screen resolution (approximate)
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int resolutionToUse = 1920; // Default to 1080p

                // Determine which resolution to use
                if (screenWidth >= 3800) resolutionToUse = 3840; // 4K
                else if (screenWidth >= 2500) resolutionToUse = 2560; // 1440p

                LogMessage($"Detected screen resolution width: {screenWidth}px. Using {resolutionToUse}p template.");

                // Launch game and join server
                bool success = false;

                if (File.Exists(hllGamePath))
                {
                    success = await automation.LaunchAndJoinServer(hllGamePath, serverToSeed.DisplayName, teamToJoin, resolutionToUse);
                    gameProcess = automation.GetGameProcess();
                }
                else
                {
                    // Try using Steam URL
                    string steamUrl = "steam://run/686810";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamUrl,
                        UseShellExecute = true
                    });

                    LogMessage("Game launched via Steam. Waiting for it to initialize...");

                    // Wait for game to start
                    await Task.Delay(20000);

                    // Try to find the game process
                    gameProcess = Process.GetProcessesByName("HLL-Win64-Shipping").FirstOrDefault();

                    if (gameProcess != null)
                    {
                        // Try automation after Steam launch
                        success = await automation.LaunchAndJoinServer(hllGamePath, serverToSeed.DisplayName, teamToJoin, resolutionToUse);
                    }
                    else
                    {
                        LogMessage("Failed to detect game process after Steam launch.");
                    }
                }

                if (success)
                {
                    LogMessage("Successfully joined server. Monitoring player count...");

                    // Start a background task to monitor the server and close when seeded
                    _ = Task.Run(async () =>
                    {
                        while (isSeeding && currentServer != null)
                        {
                            try
                            {
                                // Update server info
                                await QueryRealServerData(currentServer);

                                // Check if server is now seeded
                                if (currentServer.TotalCount >= seededThreshold)
                                {
                                    await Dispatcher.InvokeAsync(async () =>
                                    {
                                        LogMessage($"Server is now seeded ({currentServer.TotalCount}/{seededThreshold} players). Stopping seeding.");
                                        await StopSeeding();
                                    });
                                    break;
                                }

                                await Task.Delay(60000); // Check every minute
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error in monitoring task: {ex.Message}");
                            }
                        }
                    });
                }
                else
                {
                    LogMessage("Failed to automatically join server. Please join manually or try again.");

                    if (gameProcess != null && !gameProcess.HasExited)
                    {
                        LogMessage("Game is still running. You can try to join the server manually.");
                        LogMessage($"Server: {serverToSeed.DisplayName}");
                        LogMessage($"Team: {teamToJoin}");
                    }
                    else
                    {
                        LogMessage("Game process closed unexpectedly.");
                        btnStartSeeding.IsEnabled = true;
                        btnStopSeeding.IsEnabled = false;
                        isSeeding = false;
                        currentServer = null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting seeding: {ex.Message}");
                btnStartSeeding.IsEnabled = true;
                btnStopSeeding.IsEnabled = false;
                isSeeding = false;
            }
        }

        private void btnBrowseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "HLL Executable|HLL-Win64-Shipping.exe",
                Title = "Select HLL Executable"
            };

            if (dialog.ShowDialog() == true)
            {
                hllGamePath = dialog.FileName;
                tbGamePath.Text = hllGamePath;
                SaveConfig();
            }
        }

        private void btnCreateTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the path to this executable
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                // Format time for schtasks
                string timeString = $"{scheduleHour:D2}:{scheduleMinute:D2}";

                LogMessage($"Creating scheduled task to run at {timeString}...");

                // Create the task using schtasks command
                Process process = new Process();
                process.StartInfo.FileName = "schtasks.exe";
                process.StartInfo.Arguments = $"/create /tn \"HLL Server Seeder\" /tr \"\\\"{exePath}\\\" autostart\" /sc daily /st {timeString} /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Configure additional settings using separate commands
                    Process.Start("schtasks.exe", "/change /tn \"HLL Server Seeder\" /DISALLOWSTARTIFONBATTERIES:FALSE").WaitForExit();
                    Process.Start("schtasks.exe", "/change /tn \"HLL Server Seeder\" /WAKETORUN:TRUE").WaitForExit();

                    LogMessage($"Scheduled task created to run daily at {timeString}");
                    MessageBox.Show($"Task scheduled to run daily at {timeString}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"Error creating scheduled task: {error}");
                    MessageBox.Show($"Failed to create scheduled task: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating scheduled task: {ex.Message}");
                MessageBox.Show($"Failed to create scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("Removing scheduled task...");

                // Create process to run schtasks delete command
                Process process = new Process();
                process.StartInfo.FileName = "schtasks.exe";
                process.StartInfo.Arguments = "/delete /tn \"HLL Server Seeder\" /f";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    LogMessage("Scheduled task removed successfully");
                    MessageBox.Show("Scheduled task removed", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    LogMessage($"Error removing scheduled task: {error}");
                    MessageBox.Show($"Failed to remove scheduled task: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error removing scheduled task: {ex.Message}");
                MessageBox.Show($"Failed to remove scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public class ConfigSettings
        {
            public string HllGamePath { get; set; }
            public bool SleepAfterSeeding { get; set; }
            public int SeededThreshold { get; set; }
            public List<ServerInfo> Servers { get; set; }
            public int ScheduleHour { get; set; } = 12;
            public int ScheduleMinute { get; set; } = 0;
        }

        private void tbThreshold_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(tbThreshold.Text, out int threshold))
            {
                seededThreshold = threshold;
                SaveConfig();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfig();
        }
        #endregion
    }

    public class ServerInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string RconUrl { get; set; }
        public int AlliedCount { get; set; }
        public int AxisCount { get; set; }
        public int TotalCount { get; set; }
        public string TimeRemaining { get; set; }
        public bool IsOnline { get; set; } = false;
        public bool WasOnline { get; set; } = false; // Track previous online status

        public string GetStatusText()
        {
            return !IsOnline ? "Unreachable" :
                   TotalCount == 0 ? "Empty" :
                   TotalCount >= 75 ? "Seeded" : "Needs Seeding";
        }

        public Brush GetStatusColor()
        {
            return !IsOnline ? Brushes.Red :
                   TotalCount == 0 ? Brushes.Gray :
                   TotalCount >= 75 ? Brushes.Green : Brushes.Orange;
        }
    }

    public class ServerListItem
    {
        public string Name { get; set; }
        public int AlliedCount { get; set; }
        public int AxisCount { get; set; }
        public int TotalCount { get; set; }
        public string Status { get; set; }
        public Brush StatusColor { get; set; }
    }

    public class MockApiResponseProcessor
    {
        // This class handles different mock test endpoints to convert their 
        // response format into our expected format

        public static JsonDocument ConvertToHllServerFormat(string endpoint, string jsonContent)
        {
            try
            {
                // Create a JsonObject that matches our expected HLL server format
                var jsonObj = new System.Text.Json.Nodes.JsonObject
                {
                    ["result"] = new System.Text.Json.Nodes.JsonObject
                    {
                        ["player_count"] = new Random().Next(50, 100),
                        ["player_count_by_team"] = new System.Text.Json.Nodes.JsonObject
                        {
                            ["allied"] = new Random().Next(20, 40),
                            ["axis"] = new Random().Next(20, 40)
                        },
                        ["time_remaining"] = "1:30:00"
                    }
                };

                // Convert to a string
                string hllFormatJson = jsonObj.ToJsonString();

                // Parse and return as JsonDocument
                return JsonDocument.Parse(hllFormatJson);
            }
            catch (Exception)
            {
                // If anything goes wrong, return null
                return null;
            }
        }
    }
}