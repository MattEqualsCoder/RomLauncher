using MSURandomizerLibrary.Configs;

namespace RomLauncher;

public class Settings
{
    public string MsuPath { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string? LaunchApplication { get; set; }
    public string? LaunchArguments { get; set; }
    public List<string>? MsuTypeFilter { get; set; }

    public bool MsuTypeMatches(MsuType msuType)
    {
        return MsuTypeFilter == null ||
               MsuTypeFilter.Contains(msuType.DisplayName) ||
               MsuTypeFilter.Contains(msuType.Name);
    }
}