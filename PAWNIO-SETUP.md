# Setting Up PawnIO Bundling

This guide explains how to bundle the PawnIO installer with your application for seamless user experience.

## Quick Start

1. **Download PawnIO Installer**
   ```powershell
   # Visit https://pawnio.eu/ and download the latest installer
   # Or use direct link (update version as needed):
   # https://github.com/namazso/PawnIO.Setup/releases
   ```

2. **Add to Project**
   ```powershell
   # Rename the installer to PawnIO-Setup.exe
   # Place it in: ModernThinkPadLEDsController/Resources/
   ```

3. **Build**
   ```powershell
   dotnet build -c Release
   ```

The installer is now embedded in your application!

## How It Works

### First Launch (PawnIO Not Installed)
1. User runs your app
2. App detects PawnIO is missing
3. Shows `PawnIOSetupWindow` dialog
4. User clicks "Install PawnIO"
5. Embedded installer extracted to temp folder
6. Installer runs with admin privileges
7. After completion, app verifies PawnIO works
8. Main application launches

### Subsequent Launches (PawnIO Installed)
1. User runs your app
2. App detects PawnIO is present
3. Main application launches immediately

## License Compliance

PawnIO is GPL-2.0 licensed. The bundling implementation includes:

✅ Attribution in the installer UI  
✅ Link to source code (https://github.com/namazso/PawnIO)  
✅ License information displayed to users  

**No code modifications needed** - you're just redistributing the official installer.

## Without Bundling

If you don't bundle the installer:

1. The app will still work
2. Users see a dialog with a manual download link
3. They must download and install PawnIO themselves
4. Then restart your app

## File Structure

```
ModernThinkPadLEDsController/
├── Resources/
│   ├── PawnIO-Setup.exe          ← Place installer here (not in git)
│   └── README-PawnIO.md          ← License and attribution info
├── Hardware/
│   ├── LhmDriver.cs              ← LibreHardwareMonitor wrapper
│   └── PawnIOInstaller.cs        ← Auto-installation logic
└── Views/
    ├── PawnIOSetupWindow.xaml    ← User-facing install dialog
    └── PawnIOSetupWindow.xaml.cs
```

## Testing

### Test Without Installer Bundled
```powershell
# Don't place PawnIO-Setup.exe in Resources/
dotnet run
# Should show dialog with manual download link
```

### Test With Installer Bundled
```powershell
# Place PawnIO-Setup.exe in Resources/
dotnet build
dotnet run
# Should show dialog with functional "Install PawnIO" button
```

### Test When PawnIO Already Installed
```powershell
# Install PawnIO manually first from https://pawnio.eu/
dotnet run
# App should start immediately without any dialogs
```

## Distribution

### Portable ZIP
- Include or exclude `PawnIO-Setup.exe` from Resources/
- Installer gets embedded in the main .exe during build
- Single executable contains everything

### MSI Installer
- The embedded installer is part of your .exe
- No changes needed to Product.wxs
- Users get seamless installation experience

## Troubleshooting

**Q: Build fails with "PawnIO-Setup.exe not found"**  
A: That's expected if you haven't downloaded it. The build will succeed without it - bundling is optional.

**Q: Installer launches but PawnIO doesn't work**  
A: Computer may need a restart after driver installation.

**Q: Want to test without bundling?**  
A: Simply don't place PawnIO-Setup.exe in Resources/. The app will fall back to manual installation instructions.

## For Contributors

Add `PawnIO-Setup.exe` to `.gitignore`:
```
ModernThinkPadLEDsController/Resources/PawnIO-Setup.exe
```

This prevents committing the binary installer to the repository.
