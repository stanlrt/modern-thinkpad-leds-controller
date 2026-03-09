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
