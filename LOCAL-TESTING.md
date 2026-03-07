# Local Testing Guide

## Quick Start - Testing Without PawnIO Bundled

1. **Build the app**
   ```powershell
   cd d:\Repos\System\modern-thinkpad-leds-controller\ModernThinkPadLEDsController
   dotnet build -c Release
   ```

2. **Run as Administrator** (required for hardware access)
   ```powershell
   # From the build output directory
   cd bin\Release\net10.0-windows\win-x64
   Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
   ```

3. **What happens:**
   - If PawnIO not installed → Shows dialog with "Install PawnIO" button
   - Without bundled installer → Button shows manual download instructions
   - With bundled installer → Button launches automatic installation

## Testing WITH Bundled Installer

1. **Download PawnIO installer**
   - Get from: https://pawnio.eu/ or https://github.com/namazso/PawnIO.Setup/releases
   - Rename to `PawnIO-Setup.exe`

2. **Place in project**
   ```powershell
   # Create Resources folder if it doesn't exist
   New-Item -ItemType Directory -Path "Resources" -Force
   
   # Copy the installer
   Copy-Item "path\to\your\downloaded\PawnIO-Setup.exe" "Resources\PawnIO-Setup.exe"
   ```

3. **Build**
   ```powershell
   dotnet build -c Release
   ```

4. **Run**
   ```powershell
   cd bin\Release\net10.0-windows\win-x64
   Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
   ```

5. **Test the installation flow:**
   - Dialog appears → Click "Install PawnIO" → Installer launches → Completes → App starts

## Testing Scenarios

### Scenario 1: PawnIO Not Installed + No Bundled Installer
```powershell
# Don't add PawnIO-Setup.exe to Resources/
dotnet build -c Release
cd bin\Release\net10.0-windows\win-x64
Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
```
**Expected:** Dialog shows with manual download link to https://pawnio.eu/

### Scenario 2: PawnIO Not Installed + Bundled Installer
```powershell
# Add PawnIO-Setup.exe to Resources/ first
dotnet build -c Release
cd bin\Release\net10.0-windows\win-x64
Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
```
**Expected:** Dialog shows → Click button → Installer runs → App starts

### Scenario 3: PawnIO Already Installed
```powershell
# Install PawnIO manually from https://pawnio.eu/ first
dotnet build -c Release
cd bin\Release\net10.0-windows\win-x64
Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs
```
**Expected:** App starts immediately, no dialogs

## One-Liner for Quick Testing

```powershell
# Build and run (from project root)
dotnet build -c Release; Start-Process ".\bin\Release\net10.0-windows\win-x64\ModernThinkPadLEDsController.exe" -Verb RunAs
```

## Debugging in Visual Studio/Rider

1. **Open the solution** in Visual Studio or Rider
2. **Run as Administrator:**
   - Visual Studio: Right-click project → Properties → set requireAdministrator in manifest (already done)
   - Or: Run Visual Studio itself as Administrator
3. **Press F5** or click Debug → Start Debugging
4. **Breakpoints work** normally in PawnIOInstaller.cs and PawnIOSetupWindow.xaml.cs

## Common Issues

**Issue:** "Access is denied" when running
- **Fix:** Must run as Administrator (use `Start-Process -Verb RunAs`)

**Issue:** PawnIO installer doesn't launch
- **Fix:** Check if `PawnIO-Setup.exe` exists in Resources/ and was embedded (check .csproj)

**Issue:** App doesn't detect installed PawnIO
- **Fix:** Computer might need restart after PawnIO installation

**Issue:** Can't test with bundled installer
- **Fix:** You can skip bundling and test the manual installation flow instead

## Clean Testing (Reset State)

To test fresh installation flow:

1. **Uninstall PawnIO** (if installed)
   ```powershell
   # Check if installed
   Get-WmiObject -Class Win32_Product | Where-Object { $_.Name -like "*PawnIO*" }
   
   # Or uninstall via Windows Settings → Apps
   ```

2. **Delete build output**
   ```powershell
   Remove-Item -Recurse -Force bin\Release\net10.0-windows\win-x64
   ```

3. **Rebuild and test**
   ```powershell
   dotnet build -c Release
   ```

## File Locations After Build

```
bin/Release/net10.0-windows/win-x64/
├── ModernThinkPadLEDsController.exe    ← Run this
├── LibreHardwareMonitorLib.dll         ← Bundled automatically
└── [other dependencies]                 ← Framework DLLs

Note: PawnIO-Setup.exe is EMBEDDED in the .exe, not a separate file
```

## Quick Reference

| Action                  | Command                                                        |
| ----------------------- | -------------------------------------------------------------- |
| Build                   | `dotnet build -c Release`                                      |
| Run                     | `Start-Process .\ModernThinkPadLEDsController.exe -Verb RunAs` |
| Clean                   | `dotnet clean`                                                 |
| Rebuild                 | `dotnet build -c Release --no-incremental`                     |
| Check if PawnIO bundled | Check if .exe file size increased by ~10-20 MB                 |
