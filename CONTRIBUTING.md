# Contributing

## Prerequisites

- Windows 10 or later
- A C# compatible IDE (Visual Studio 2022+, Rider, or VS Code with C# extensions)
- NET 10 SDK
- A ThinkPad laptop
- **(Optional for bundling)** PawnIO installer from https://pawnio.eu/

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

The build output lands in:

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

On first run, if PawnIO is not installed, the **PawnIO Driver Setup** window will appear. The app will automatically install PawnIO if you've bundled the installer (see setup instructions). If the installer is not bundled, users will need to download and install PawnIO manually from https://pawnio.eu/.

> **Note:** The application uses [PawnIO](https://pawnio.eu/) for hardware I/O access. PawnIO provides better security than legacy drivers by using scriptable kernel modules with restricted access.

### Bundling PawnIO Installer (Optional)

To bundle the PawnIO installer for automatic installation:

1. Download the latest PawnIO installer from https://pawnio.eu/
2. Rename it to `PawnIO-Setup.exe`
3. Place it in `ModernThinkPadLEDsController/Resources/`
4. Rebuild the project - the installer will be embedded automatically

See [Resources/README-PawnIO.md](ModernThinkPadLEDsController/Resources/README-PawnIO.md) for license compliance and details.

See [Resources/README-PawnIO.md](ModernThinkPadLEDsController/Resources/README-PawnIO.md) for details.

## Publishing

The project is configured for a **self-contained, single-file** `win-x64` executable:

```shell
dotnet publish ModernThinkPadLEDsController/ModernThinkPadLEDsController.csproj -c Release
```

Output lands in:

```
ModernThinkPadLEDsController/bin/Release/net10.0-windows/win-x64/publish/
```

> **Note:** The application requires [PawnIO](https://pawnio.eu/) to be installed on the target system for hardware access. Users should install PawnIO before running the application.

## Project Structure

```
ModernThinkPadLEDsController/
├── Hardware/           # LibreHardwareMonitor-based driver wrapper, EC controller, LED controller
├── Monitoring/         # Background monitors (disk activity, keyboard backlight,
│                       #   key lock, microphone mute, power events)
├── Services/           # Startup task manager, system-tray icon service
├── ViewModels/         # MVVM view models (CommunityToolkit.Mvvm)
├── Views/              # WPF windows (MainWindow, DriverSetupWindow)
├── App.xaml(.cs)       # Application entry point
├── AppSettings.cs      # Persisted user settings
└── app.manifest        # UAC elevation + DPI awareness declarations
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

## Creating a Release

Releases are automated via GitHub Actions. When you push a version tag, a workflow builds an MSI installer and creates a GitHub release.

### Release Process

1. **Ensure `main` branch is ready**
   
   Make sure all desired changes are merged and tested.

2. **Create and push a version tag**

   Use semantic versioning (`v{major}.{minor}.{patch}`):

   ```shell
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. **GitHub Actions automatically:**
   - Publishes the single-file executable
   - Builds the MSI installer using WiX Toolset
   - Creates a GitHub release with:
     - Auto-generated release notes
     - `ModernThinkPadLEDsController-v1.0.0-win-x64.msi` download

### Version Numbering Guidelines

- `v1.0.0` - First stable release
- `v1.1.0` - New features (minor version bump)
- `v1.0.1` - Bug fixes only (patch version bump)
- `v2.0.0` - Breaking changes (major version bump)

### Building MSI Locally (Optional)

To test the installer before releasing:

```shell
# Install WiX Toolset (one-time setup)
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext

# Publish the application
dotnet publish ModernThinkPadLEDsController/ModernThinkPadLEDsController.csproj `
  --configuration Release `
  --output ./publish `
  /p:PublishSingleFile=true `
  /p:SelfContained=true `
  /p:RuntimeIdentifier=win-x64

# Build the MSI
wix build Installer/Product.wxs `
  -d PublishDir=publish `
  -out ModernThinkPadLEDsController.msi `
  -ext WixToolset.UI.wixext `
  -arch x64
```

The MSI installs to `C:\Program Files\Modern ThinkPad LEDs Controller\` and creates a Start Menu shortcut.
