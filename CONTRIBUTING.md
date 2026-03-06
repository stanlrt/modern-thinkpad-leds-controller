# Contributing

## Prerequisites

- Windows 10 or later
- A C# compatible IDE (Visual Studio 2022+, Rider, or VS Code with C# extensions)
- NET 10 SDK
- A ThinkPad laptop

## Setup

1. Clone the repository

   ```shell
   git clone https://github.com/stanlrt/modern-thinkpad-leds-controller.git
   cd modern-thinkpad-leds-controller
   ```

2. Open the solution

   Open `ModernThinkPadLEDsController.slnx` in Visual Studio or Rider.

3. **Restore NuGet packages**

   This happens automatically when you open the solution. To do it manually:

   ```shell
   dotnet restore
   ```

## Building

### Via IDE

Press **Build → Build Solution** (or <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>B</kbd> in Visual Studio).

### Via CLI

```shell
dotnet build ModernThinkPadLEDsController/ModernThinkPadLEDsController.csproj
```

The build output (including `inpoutx64.dll`) lands in:

```
ModernThinkPadLEDsController/bin/Debug/net10.0-windows/win-x64/
```

## Running

> **The app must always be launched as Administrator.** The UAC prompt appears automatically thanks to the `requireAdministrator` entry in `app.manifest`.

### Via IDE

In Visual Studio, right-click the project → **Debug → Start New Instance** (Visual Studio runs elevated when the manifest requests it). Alternatively, launch Visual Studio itself as Administrator.

### Via CLI (from the build output folder)

```shell
# Navigate to the build output first
cd ModernThinkPadLEDsController/bin/Debug/net10.0-windows/win-x64

# Run as Administrator (Start-Process with -Verb RunAs in PowerShell)
Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
```

On first run, the **Driver Setup** window will appear. Click **Initialize Driver** to register the `inpoutx64.dll` kernel service. This is a one-time step; subsequent launches (including the "Start with Windows" scheduled task) are silent.

## Publishing

The project is configured for a **self-contained, single-file** `win-x64` executable:

```shell
dotnet publish ModernThinkPadLEDsController/ModernThinkPadLEDsController.csproj -c Release
```

Output lands in:

```
ModernThinkPadLEDsController/bin/Release/net10.0-windows/win-x64/publish/
```

> **Note:** `inpoutx64.dll` is a native (C++) DLL and cannot be bundled inside the single-file executable. It will always appear as a separate file next to the `.exe` in the publish folder.

## Project Structure

```
ModernThinkPadLEDsController/
├── Hardware/           # InpOut driver P/Invoke wrapper, EC controller, LED controller
├── Monitoring/         # Background monitors (disk activity, keyboard backlight,
│                       #   key lock, microphone mute, power events)
├── Services/           # Startup task manager, system-tray icon service
├── ViewModels/         # MVVM view models (CommunityToolkit.Mvvm)
├── Views/              # WPF windows (MainWindow, DriverSetupWindow)
├── App.xaml(.cs)       # Application entry point
├── AppSettings.cs      # Persisted user settings
├── app.manifest        # UAC elevation + DPI awareness declarations
└── inpoutx64.dll       # Native port I/O kernel driver (pre-built, x64 only)
```

## Making Changes

1. **Fork** the repository and create a branch from `main`:

   ```shell
   git checkout -b feature/your-feature-name
   ```

2. Make your changes, keeping the existing code style and MVVM patterns.

3. Verify the build is clean:

   ```shell
   dotnet build
   ```

4. Test on real ThinkPad hardware if your change touches `Hardware/` or `Monitoring/`.

5. Open a **Pull Request** against `main` with a clear description of what was changed and why.
