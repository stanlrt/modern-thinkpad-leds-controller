using ModernThinkPadLEDsController.Monitoring;

namespace ModernThinkPadLEDsController.Services;

public sealed class MonitoringHub
{
    public MonitoringHub(
        DiskActivityMonitor diskMonitor,
        KeyboardBacklightMonitor keyboardBacklightMonitor,
        MicrophoneMuteMonitor microphoneMuteMonitor,
        SpeakerMuteMonitor speakerMuteMonitor,
        PowerEventListener powerListener)
    {
        DiskMonitor = diskMonitor;
        KeyboardBacklightMonitor = keyboardBacklightMonitor;
        MicrophoneMuteMonitor = microphoneMuteMonitor;
        SpeakerMuteMonitor = speakerMuteMonitor;
        PowerListener = powerListener;
    }

    public DiskActivityMonitor DiskMonitor { get; }

    public KeyboardBacklightMonitor KeyboardBacklightMonitor { get; }

    public MicrophoneMuteMonitor MicrophoneMuteMonitor { get; }

    public SpeakerMuteMonitor SpeakerMuteMonitor { get; }

    public PowerEventListener PowerListener { get; }
}
