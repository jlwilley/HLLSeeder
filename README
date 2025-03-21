# 🎮 HLL Server Seeder 🎖️

## Automate Your Server Seeding Adventures!

Are you tired of manually joining empty Hell Let Loose servers just to get the action started? Do you want to contribute to your community by helping seed servers but don't want to babysit your PC? **HLL Server Seeder** is here to save the day!

## 🚀 Features

- 🤖 **Fully Automated** - Join servers with zero manual input
- ⚖️ **Team Balancing** - Automatically joins the team with fewer players
- 🔄 **Sequential Seeding** - Seeds multiple servers in order of priority
- ⏰ **Scheduled Seeding** - Set it and forget it with Windows Task Scheduler
- 💤 **Sleep Mode** - Put your PC to sleep after seeding is complete
- 🖥️ **Multi-Resolution Support** - Works with 1080p, 1440p, and 4K displays

## 📋 Requirements

- Windows 10 or 11
- Hell Let Loose installed via Steam
- One of the following display resolutions: 1920x1080, 2560x1440, or 3840x2160
- .NET Framework 4.7.2 or higher

## 🛠️ Installation

1. Download the latest release from the [releases page](https://github.com/jlwilley/HLLServerSeeder/releases)
2. Extract the ZIP file to a location of your choice
3. Run `HLLSeeder.exe` to start the application
4. Configure your settings and you're ready to go!

## 📝 Configuration

The application has several settings you can customize:

| Setting | Description |
|---------|-------------|
| Game Path | Path to your HLL-Win64-Shipping.exe file |
| Seeded Threshold | Player count at which a server is considered "seeded" |
| Schedule Time | When automatic seeding should run if scheduled |
| Sleep After Seeding | Whether to put your PC to sleep when done |

## 🔧 How It Works

1. **Server Monitoring**: The app constantly checks server populations using the RCON API
2. **Team Selection**: Joins the team with fewer players to help balance the game
3. **Menu Navigation**: Uses simulated inputs to navigate through the game menus
4. **Continuous Checking**: Monitors the server player count until it reaches the threshold
5. **Automated Shutdown**: Closes the game when seeding is complete or the server is populated

## 🎯 Supported Servers

The application comes pre-configured with these servers:
- 🔴 ROTN (=ROTN= | discord)
- 🔵 SYN (Syndicate | US East)
- 🟢 CTRL (Ctrl Alt Defeat[Hellfire)

Want to add more servers? Edit the `config.json` file in the installation directory:
```
%USERPROFILE%\hll-seeder\config.json
```

## ⚠️ Important Notes

- Avoid moving your mouse or using keyboard during the automation process
- Make sure your game resolution matches your monitor resolution
- The application needs to be run as Administrator for best results
- Due to anti-cheat considerations, this uses legitimate input simulation rather than memory manipulation

## 🛡️ Is This Allowed?

Yes! Server seeding is not only allowed but encouraged by server admins. This tool simply automates what you would normally do manually to help populate servers. It does not manipulate the game in any way that would give you an advantage.

## 🔍 Troubleshooting

| Problem | Solution |
|---------|----------|
| Game doesn't launch | Verify the game path in settings |
| Can't find server | Make sure the server name matches exactly |
| Automation fails | Try running in Windowed mode (not Fullscreen) |
| Stuck in a menu | Restart the application and try again |

## 🤝 Contributing

Contributions are welcome! Feel free to submit pull requests or open issues if you have suggestions or find bugs.

## 📄 License

MIT License - See LICENSE file for details!