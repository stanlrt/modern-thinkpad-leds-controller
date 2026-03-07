# MSI Installer Configuration

This folder contains the WiX Toolset configuration for building the MSI installer.

## Files

- **Product.wxs** - WiX source file defining the installer structure
- **License.rtf** - License agreement shown during installation

## Building Locally

To build the MSI installer locally:

```powershell
# Install WiX (one-time setup)
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext

# Publish the application first
dotnet publish ../ModernThinkPadLEDsController/ModernThinkPadLEDsController.csproj `
  --configuration Release `
  --output ../publish `
  /p:PublishSingleFile=true `
  /p:SelfContained=true `
  /p:RuntimeIdentifier=win-x64 `
  /p:PublishReadyToRun=true

# Build the MSI
wix build Product.wxs `
  -d PublishDir=../publish `
  -out ModernThinkPadLEDsController.msi `
  -ext WixToolset.UI.wixext `
  -arch x64
```

The MSI will be created in the current directory.

## What the Installer Does

- Installs to `C:\Program Files\Modern ThinkPad LEDs Controller\`
- Creates a Start Menu shortcut
- Registers in Add/Remove Programs for clean uninstall
- Requires administrator privileges (perMachine install)

> **Note:** The application requires [PawnIO](https://pawnio.eu/) to be installed for hardware access. Users must install PawnIO separately if not already installed.

## Important Notes

- The `UpgradeCode` GUID in Product.wxs must remain constant across versions
- This ensures Windows recognizes upgrades and properly replaces old versions
- Each build gets a unique `Product Id="*"` automatically
