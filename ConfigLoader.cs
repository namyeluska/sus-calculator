using System;
using System.IO;
using System.Text.Json;

namespace SusCalculator;

internal static class ConfigLoader
{
    public const string FileName = "vm-config.json";

    public static VmConfig Load(out string? error)
    {
        error = null;
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            var created = CreateDefault();
            TrySave(created, configPath, out _);
            return created;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<VmConfig>(json, SerializerOptions) ?? CreateDefault();
            Normalize(config);
            return config;
        }
        catch (Exception ex)
        {
            error = $"Failed to read {configPath}: {ex.Message}";
            var fallback = CreateDefault();
            Normalize(fallback);
            return fallback;
        }
    }

    public static string GetConfigPath()
    {
        var cwdPath = Path.Combine(Environment.CurrentDirectory, FileName);
        if (File.Exists(cwdPath))
        {
            return cwdPath;
        }
        var basePath = Path.Combine(AppContext.BaseDirectory, FileName);
        if (File.Exists(basePath))
        {
            return basePath;
        }

        return cwdPath;
    }

    private static void TrySave(VmConfig config, string path, out string? error)
    {
        error = null;
        try
        {
            var json = JsonSerializer.Serialize(config, SerializerOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
    }

    private static VmConfig CreateDefault()
    {
        return new VmConfig();
    }

    private static void Normalize(VmConfig config)
    {
        config.SecretTrigger ??= new SecretTriggerConfig();
        config.Qemu ??= new QemuSettings();
        if (config.Notes == null || config.Notes.Length == 0)
        {
            config.Notes = new VmConfig().Notes;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
