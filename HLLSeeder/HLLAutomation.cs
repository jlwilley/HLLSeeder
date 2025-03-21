using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HLLServerSeeder
{
    public class HLLAutomation
    {
        private Action<string> logCallback;
        private Process gameProcess;
        private bool isWidescreen;

        // Screen resolution based coordinates
        private int[] resolutionWidths = { 1920, 2560, 3840 };
        private int currentResIndex = 0;

        // Coordinates for 1920x1080 resolution
        private readonly int[,] mainMenuCoords =
        {
            { 960, 600 },    // 1920x1080 - Play button
            { 1280, 600 },   // 2560x1440
            { 1920, 900 }    // 3840x2160
        };

        private readonly int[,] serverBrowserCoords =
        {
            { 960, 380 },    // 1920x1080 - Server Browser button 
            { 1280, 380 },   // 2560x1440
            { 1920, 570 }    // 3840x2160
        };

        private readonly int[,] serverSearchCoords =
        {
            { 960, 200 },    // 1920x1080 - Search box
            { 1280, 200 },   // 2560x1440
            { 1920, 300 }    // 3840x2160
        };

        private readonly int[,] firstServerCoords =
        {
            { 960, 350 },    // 1920x1080 - First server in list
            { 1280, 350 },   // 2560x1440
            { 1920, 525 }    // 3840x2160
        };

        private readonly int[,] joinButtonCoords =
        {
            { 960, 950 },    // 1920x1080 - Join button
            { 1280, 1250 },  // 2560x1440
            { 1920, 1875 }   // 3840x2160
        };

        private readonly int[,] alliedTeamCoords =
        {
            { 600, 540 },    // 1920x1080 - Allied team button
            { 800, 720 },    // 2560x1440
            { 1200, 1080 }   // 3840x2160
        };

        private readonly int[,] axisTeamCoords =
        {
            { 1320, 540 },   // 1920x1080 - Axis team button
            { 1760, 720 },   // 2560x1440
            { 2640, 1080 }   // 3840x2160
        };

        public HLLAutomation(Action<string> logCallback)
        {
            this.logCallback = logCallback;
        }

        public async Task<bool> LaunchAndJoinServer(string gamePath, string serverName, string teamToJoin, int resolutionWidth = 1920)
        {
            try
            {
                // Set resolution index
                for (int i = 0; i < resolutionWidths.Length; i++)
                {
                    if (resolutionWidths[i] == resolutionWidth)
                    {
                        currentResIndex = i;
                        break;
                    }
                }

                // Launch the game
                Log($"Launching Hell Let Loose from {gamePath}");
                gameProcess = Process.Start(gamePath);

                if (gameProcess == null)
                {
                    Log("Failed to launch the game.");
                    return false;
                }

                // Wait for game to initialize
                Log("Waiting for game to initialize (60 seconds)...");
                await Task.Delay(60000);

                // Make sure game window is focused
                Log("Focusing game window...");
                InputSimulator.FocusWindow(gameProcess.MainWindowHandle);
                await Task.Delay(2000);

                // Click on Play button in main menu
                Log("Navigating to the main menu...");
                InputSimulator.ClickAt(mainMenuCoords[currentResIndex, 0], mainMenuCoords[currentResIndex, 1]);
                await Task.Delay(2000);

                // Click on Server Browser 
                Log("Opening server browser...");
                InputSimulator.ClickAt(serverBrowserCoords[currentResIndex, 0], serverBrowserCoords[currentResIndex, 1]);
                await Task.Delay(5000);

                // Click on search box and enter server name
                Log($"Searching for server: {serverName}");
                InputSimulator.ClickAt(serverSearchCoords[currentResIndex, 0], serverSearchCoords[currentResIndex, 1]);
                await Task.Delay(500);

                // Clear search field with Ctrl+A and Delete
                InputSimulator.KeyDown(0x11); // Ctrl
                InputSimulator.PressKey(0x41); // A
                InputSimulator.KeyUp(0x11);
                InputSimulator.PressKey(0x2E); // Delete
                await Task.Delay(500);

                // Type server name
                InputSimulator.TypeText(serverName);
                await Task.Delay(3000);

                // Click on first server in the list
                Log("Selecting server from list...");
                InputSimulator.ClickAt(firstServerCoords[currentResIndex, 0], firstServerCoords[currentResIndex, 1]);
                await Task.Delay(1000);

                // Click on Join button
                Log("Joining server...");
                InputSimulator.ClickAt(joinButtonCoords[currentResIndex, 0], joinButtonCoords[currentResIndex, 1]);
                await Task.Delay(20000); // Wait for server connection

                // Select team
                Log($"Selecting {teamToJoin} team...");
                if (teamToJoin.ToLower() == "allied")
                {
                    InputSimulator.ClickAt(alliedTeamCoords[currentResIndex, 0], alliedTeamCoords[currentResIndex, 1]);
                }
                else
                {
                    InputSimulator.ClickAt(axisTeamCoords[currentResIndex, 0], axisTeamCoords[currentResIndex, 1]);
                }

                Log("Successfully joined server!");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Error during automation: {ex.Message}");
                return false;
            }
        }

        public Process GetGameProcess()
        {
            return gameProcess;
        }

        private void Log(string message)
        {
            logCallback?.Invoke(message);
        }
    }
}