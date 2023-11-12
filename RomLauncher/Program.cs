// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MSURandomizerLibrary;
using MSURandomizerLibrary.Configs;
using MSURandomizerLibrary.Models;
using MSURandomizerLibrary.Services;
using Serilog;
using YamlDotNet.Serialization;

namespace RomLauncher;

public static class Program
{
    private static ServiceProvider _services = null!;

    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();
        
        _services = new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.AddSerilog(dispose: true);
            })
            .AddMsuRandomizerServices()
            .BuildServiceProvider();
        
        var settingsFile = new FileInfo("rom-launcher.yml");

        if (!settingsFile.Exists)
        {
            CreateSettingsFile(settingsFile);
            Log.Information($"Created settings file at {settingsFile.FullName}");
            return;
        }
        
        Log.Information($"Loaded settings file at {settingsFile.FullName}");

        var settings = LoadSettingsFile(settingsFile);

        InitializeMsuRandomizer(_services.GetRequiredService<IMsuRandomizerInitializationService>());

        var targetRomPath = CopyRom(args[0], settings);
        if (targetRomPath == null)
        {
            return;
        }

        var msuType = GetMsuType(settings);
        if (msuType == null)
        {
            return;
        }
        
        var msus = GetSelectedMsus(settings, msuType);
        if (msus == null)
        {
            return;
        }

        if (msus.Count > 0)
        {
            _services.GetRequiredService<IMsuSelectorService>().CreateShuffledMsu(new MsuSelectorRequest()
            {
                Msus = msus,
                OutputMsuType = msuType,
                OutputPath = targetRomPath,
                EmptyFolder = true,
                OpenFolder = false,
                PrevMsu = null
            });
        }

        LaunchRom(targetRomPath, settings.LaunchApplication, settings.LaunchArguments);
        
        Thread.Sleep(2000);
    }

    static string? CopyRom(string sourcePath, Settings settings)
    {
        var source = new FileInfo(sourcePath);
        
        if (!source.Exists)
        {
            Log.Error($"File not found {source.FullName}");
            return null;
        }
        
        var destination = new DirectoryInfo(settings.TargetPath);
        if (!destination.Exists)
        {
            Log.Error($"Destination Path {destination.FullName} does not exist");
            return null;
        }
        
        var targetFolder =
            new DirectoryInfo(Path.Combine(destination.FullName, source.Name.Replace(source.Extension, "")));
        if (!targetFolder.Exists)
        {
            targetFolder.Create();
        }

        var targetFile = new FileInfo(Path.Combine(targetFolder.FullName, source.Name));
        if (targetFile.Exists)
        {
            try
            {
                targetFile.Delete();
            }
            catch (Exception e)
            {
                Log.Warning(e, "Could not delete previous rom file");
            }
        }

        try
        {
            File.Copy(source.FullName, targetFile.FullName);
            Log.Information($"Copied rom file to {targetFile.FullName}");
        }
        catch (Exception e)
        {
            Log.Warning(e, "Could not copy rom file");
        }

        if (File.Exists(targetFile.FullName))
        {
            return targetFile.FullName;
        }
        else
        {
            return null;
        }
        
    }

    static MsuType? GetMsuType(Settings settings)
    {
        var msuTypes = _services.GetRequiredService<IMsuTypeService>().MsuTypes.Where(settings.MsuTypeMatches).ToList();
        
        for (var i = 0; i < msuTypes.Count; i++)
        {
            Log.Information($"{i+1}) {msuTypes[i].DisplayName}");
        }
        
        Log.Information($"Select an MSU Type (1-{msuTypes.Count})");

        if (!int.TryParse(Console.ReadLine(), out var index) || index <= 0 || index > msuTypes.Count)
        {
            Log.Information("Invalid selection");
            return null;
        }

        return msuTypes[index - 1];
    }

    static List<Msu>? GetSelectedMsus(Settings settings, MsuType msuType)
    {
        var msuPath = new DirectoryInfo(settings.MsuPath);
        if (!msuPath.Exists)
        {
            Log.Error($"MSU Path {msuPath.FullName} does not exist");
            return null;
        }
        
        var msus = _services.GetRequiredService<IMsuLookupService>().LookupMsus(settings.MsuPath)
            .Where(x => x.NumUniqueTracks > 10 && x.MatchesFilter(MsuFilter.Compatible, msuType, null)).OrderBy(x => x.DisplayName).ToList();

        for (var i = 0; i < msus.Count; i++)
        {
            Log.Information($"{i+1}) {msus[i].DisplayName} ({msus[i].NumUniqueTracks} Tracks)");
        }
        
        Log.Information($"{msus.Count+1}) Shuffle All");
        
        Log.Information($"{msus.Count+2}) Vanilla Music");
        
        Log.Information($"Select an MSU (1-{msus.Count+2})");

        if (!int.TryParse(Console.ReadLine(), out var index) || index <= 0 || index > msus.Count+2)
        {
            Log.Information("Invalid selection");
            return null;
        }

        if (index <= msus.Count)
        {
            return new List<Msu>() { msus[index - 1] };
        }
        else if (index == msus.Count+1)
        {
            return msus;
        }
        else
        {
            return new List<Msu>();
        }
    }
    
    static void InitializeMsuRandomizer(IMsuRandomizerInitializationService msuRandomizerInitializationService)
    {
        var settingsStream =  Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RomLauncher.msu-randomizer-settings.yml");
        var msuInitializationRequest = new MsuRandomizerInitializationRequest()
        {
            MsuAppSettingsStream = settingsStream,
            LookupMsus = false
        };
        msuRandomizerInitializationService.Initialize(msuInitializationRequest);
    }

    static void CreateSettingsFile(FileInfo file)
    {
        var settings = new Settings();
        var serializer = new Serializer();
        var yamlText = serializer.Serialize(settings);
        File.WriteAllText(file.FullName, yamlText);
    }

    static Settings LoadSettingsFile(FileInfo file)
    {
        var yamlText = File.ReadAllText(file.FullName);
        var serializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return serializer.Deserialize<Settings>(yamlText);
    }
    
    static void LaunchRom(string romPath, string? launchApplication, string? launchArguments)
    {
        if (!File.Exists(romPath))
        {
            throw new FileNotFoundException($"{romPath} not found");
        }

        if (string.IsNullOrEmpty(launchApplication))
        {
            launchApplication = romPath;
        }
        
        if (string.IsNullOrEmpty(launchApplication))
        {
            launchApplication = romPath;
        }
        else
        {
            if (string.IsNullOrEmpty(launchArguments))
            {
                launchArguments = $"\"{romPath}\"";
            }
            else if (launchArguments.Contains("%rom%"))
            {
                launchArguments = launchArguments.Replace("%rom%", $"{romPath}");
            }
            else
            {
                launchArguments = $"{launchArguments} \"{romPath}\"";
            }
        }
        
        Log.Information($"Launching {launchApplication} {launchArguments}");

        Process.Start(new ProcessStartInfo
        {
            FileName = launchApplication,
            Arguments = launchArguments,
            UseShellExecute = true,
        });
    }
    
    private static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RomLauncher", "rom-launcher-.log");
}
