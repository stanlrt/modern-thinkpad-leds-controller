# Contributing

## Prerequisites

- Windows 10 or later
- A C# compatible IDE (Visual Studio 2022+, Rider, or VS Code with C# extensions)
- NET 10 SDK
- A ThinkPad laptop
- PawnIO (unless you want to test the "missing PawnIO" user flow)

## Setup

1. Clone the repository

   ```shell
   git clone https://github.com/stanlrt/modern-thinkpad-leds-controller.git
   cd modern-thinkpad-leds-controller
   ```

2. Open the solution

   Open `ModernThinkPadLEDsController.slnx` in your IDE.

3. Restore NuGet packages

   ```shell
   dotnet restore
   ```

## Project Structure

```txt
ModernThinkPadLEDsController/
├── Hardware/           # EC access, hardware gating, and low-level device writes
├── Lighting/           # LED domain types, mappings, blink control, and runtime behavior
├── Monitoring/         # Background observers for disk, audio mute, power, and fullscreen state
├── Presentation/       # WPF UI layer
│   ├── Converters/     # XAML value converters
│   ├── Services/       # Presentation-facing orchestration
│   ├── ViewModels/     # MVVM view models (CommunityToolkit.Mvvm)
│   └── Views/          # WPF windows (MainWindow, PawnIOSetupWindow)
├── Runtime/            # Application startup and runtime orchestration
├── Settings/           # Settings persistence and runtime settings side effects
├── Shell/              # Tray icon, hotkeys, startup task, and main window hosting
├── Logging/            # Logging configuration and related docs
├── Resources/          # Packaged assets and resource dictionaries
├── App.xaml(.cs)       # Application entry point
├── AssemblyInfo.cs     # WPF assembly-level resource metadata
└── app.manifest        # UAC elevation + DPI awareness declarations
```

## Running

On first run, if PawnIO is not installed, the **PawnIO Driver Setup** window will appear.

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

### Building MSI Locally

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
