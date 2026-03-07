# PawnIO Installer Bundling

This directory should contain the PawnIO installer executable for bundling with the application.

## Setup Instructions

1. **Download PawnIO Installer**
   - Visit https://pawnio.eu/
   - Download the latest PawnIO installer (e.g., `PawnIO-Setup-2.1.0.exe`)
   - Rename it to `PawnIO-Setup.exe`
   - Place it in this directory: `ModernThinkPadLEDsController/Resources/`

2. **Add to Project**
   - The installer will be embedded as a resource in the application
   - It will be extracted and launched automatically when users run the app for the first time

## License Compliance

PawnIO is distributed under the GPL-2.0 license, which allows redistribution.

**Attribution:**
- PawnIO © 2026 namazso
- Licensed under GPL-2.0
- Source: https://github.com/namazso/PawnIO
- Website: https://pawnio.eu/

**Requirements:**
- Include PawnIO's license in your application
- Provide attribution (already included in installer UI)
- Link to source code (already included in installer UI)

## How It Works

1. On first launch, the app checks if PawnIO is installed
2. If not found, it shows the `PawnIOSetupWindow`
3. User clicks "Install PawnIO"
4. The embedded installer is extracted to temp folder
5. Installer runs with admin privileges
6. After installation completes, the app verifies PawnIO is working
7. If successful, the main application starts

## Alternative: Manual Installation

If you prefer not to bundle the installer:
1. Remove the embedded resource
2. Update `PawnIOInstaller.cs` to return false from `InstallPawnIO()`
3. The UI will show the manual download link: https://pawnio.eu/
