namespace pm.Application.Settings;

public class ReminderSettings
{
    public bool Enabled { get; set; } = true;
    public int InitialDelayMinutes { get; set; } = 5;
    public int ScanIntervalMinutes { get; set; } = 60;
}
