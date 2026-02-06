using System;
using System.Diagnostics;
using System.IO;

namespace SusCalculator;

internal sealed class VmLauncher
{
    private readonly VmConfig _config;
    private readonly string _baseDirectory;
    private readonly object _lock = new();
    private bool _launchAttempted;
    private string? _lastLogPath;

    public string? LastLogPath => _lastLogPath;

    public VmLauncher(VmConfig config, string baseDirectory)
    {
        _config = config;
        _baseDirectory = baseDirectory;
    }

    public bool TryLaunch(out string? error)
    {
        lock (_lock)
        {
            if (_launchAttempted)
            {
                error = "VM launch already attempted.";
                return false;
            }

            _launchAttempted = true;
        }

        try
        {
            return TryLaunchInternal(out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool TryLaunchInternal(out string? error)
    {
        error = null;
        var settings = _config.Qemu ?? new QemuSettings();
        var logPath = ResolveLogPath(settings);
        _lastLogPath = logPath;
        var logWriter = CreateLogWriter(logPath);
        var logLock = new object();

        void WriteLog(string message)
        {
            if (logWriter == null)
            {
                return;
            }

            lock (logLock)
            {
                logWriter.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                logWriter.Flush();
            }
        }

        var qemuPath = ResolvePath(settings.QemuPath);
        if (string.IsNullOrWhiteSpace(qemuPath) || !File.Exists(qemuPath))
        {
            error = AppendLogHint("qemu.qemuPath is not set or the file does not exist.", logPath);
            WriteLog(error);
            logWriter?.Dispose();
            return false;
        }

        var diskPath = ResolvePath(settings.DiskPath);
        if (string.IsNullOrWhiteSpace(diskPath))
        {
            error = AppendLogHint("qemu.diskPath is empty.", logPath);
            WriteLog(error);
            logWriter?.Dispose();
            return false;
        }

        if (!EnsureDisk(settings, diskPath, out error))
        {
            error = AppendLogHint(error ?? "Disk creation failed.", logPath);
            WriteLog(error);
            logWriter?.Dispose();
            return false;
        }

        var startInfo = new ProcessStartInfo(qemuPath)
        {
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = Path.GetDirectoryName(qemuPath) ?? _baseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        AddBaseArguments(startInfo, settings, diskPath);

        if (!string.IsNullOrWhiteSpace(settings.IsoPath))
        {
            var isoPath = ResolvePath(settings.IsoPath);
            if (!File.Exists(isoPath))
            {
                error = "qemu.isoPath does not exist.";
                return false;
            }

            startInfo.ArgumentList.Add("-cdrom");
            startInfo.ArgumentList.Add(isoPath);
        }

        if (!string.IsNullOrWhiteSpace(settings.BootOrder))
        {
            startInfo.ArgumentList.Add("-boot");
            startInfo.ArgumentList.Add($"order={settings.BootOrder}");
        }

        if (!string.IsNullOrWhiteSpace(settings.Accelerator))
        {
            startInfo.ArgumentList.Add("-accel");
            startInfo.ArgumentList.Add(settings.Accelerator);
        }

        var debugFlags = settings.DebugFlags?.Trim();
        if (!string.IsNullOrWhiteSpace(debugFlags))
        {
            var debugLogPath = BuildDebugLogPath(logPath, qemuPath);
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(debugFlags);
            startInfo.ArgumentList.Add("-D");
            startInfo.ArgumentList.Add(debugLogPath);
            WriteLog($"QEMU debug log: {debugLogPath}");
        }

        if (settings.ExtraArgs != null)
        {
            foreach (var arg in settings.ExtraArgs)
            {
                if (!string.IsNullOrWhiteSpace(arg))
                {
                    startInfo.ArgumentList.Add(arg);
                }
            }
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                WriteLog($"[OUT] {args.Data}");
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                WriteLog($"[ERR] {args.Data}");
            }
        };
        process.Exited += (_, _) =>
        {
            WriteLog($"QEMU exited with code {process.ExitCode}.");
            logWriter?.Dispose();
            process.Dispose();
        };

        WriteLog($"Launching QEMU: {qemuPath}");
        WriteLog($"Disk: {diskPath}");

        if (!process.Start())
        {
            error = AppendLogHint("Failed to start QEMU.", logPath);
            WriteLog(error);
            logWriter?.Dispose();
            return false;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return true;
    }

    private bool EnsureDisk(QemuSettings settings, string diskPath, out string? error)
    {
        error = null;
        if (File.Exists(diskPath))
        {
            return true;
        }

        var directory = Path.GetDirectoryName(diskPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var qemuImgPath = ResolvePath(settings.QemuImgPath);
        if (string.IsNullOrWhiteSpace(qemuImgPath))
        {
            qemuImgPath = GuessQemuImgPath(settings.QemuPath);
        }

        if (string.IsNullOrWhiteSpace(qemuImgPath) || !File.Exists(qemuImgPath))
        {
            error = "qemu.qemuImgPath is not set and qemu-img.exe could not be found.";
            return false;
        }

        var sizeGb = settings.DiskSizeGB <= 0 ? 40 : settings.DiskSizeGB;
        var createInfo = new ProcessStartInfo(qemuImgPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        createInfo.ArgumentList.Add("create");
        createInfo.ArgumentList.Add("-f");
        createInfo.ArgumentList.Add("qcow2");
        createInfo.ArgumentList.Add(diskPath);
        createInfo.ArgumentList.Add($"{sizeGb}G");

        using var process = Process.Start(createInfo);
        if (process == null)
        {
            error = "Failed to start qemu-img.";
            return false;
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            error = "qemu-img failed to create the disk.";
            return false;
        }

        return true;
    }

    private void AddBaseArguments(ProcessStartInfo startInfo, QemuSettings settings, string diskPath)
    {
        var memoryMb = settings.MemoryMB <= 0 ? 2048 : settings.MemoryMB;
        var cpus = settings.Cpus <= 0 ? 2 : settings.Cpus;

        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(memoryMb.ToString());
        startInfo.ArgumentList.Add("-smp");
        startInfo.ArgumentList.Add(cpus.ToString());
        startInfo.ArgumentList.Add("-drive");
        startInfo.ArgumentList.Add($"file={diskPath},if=virtio,format=qcow2");
    }

    private string ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (Path.IsPathRooted(expanded))
        {
            return expanded;
        }

        return Path.GetFullPath(Path.Combine(_baseDirectory, expanded));
    }

    private string ResolveLogPath(QemuSettings settings)
    {
        var logPath = ResolvePath(settings.LogPath);
        if (string.IsNullOrWhiteSpace(logPath))
        {
            logPath = ResolvePath(@"vm\qemu.log");
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return logPath;
    }

    private static StreamWriter? CreateLogWriter(string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return null;
        }

        try
        {
            return new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        }
        catch
        {
            return null;
        }
    }

    private static string AppendLogHint(string message, string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return message;
        }

        return $"{message} See log: {logPath}";
    }

    private string BuildDebugLogPath(string logPath, string qemuPath)
    {
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            var directory = Path.GetDirectoryName(logPath);
            var fileName = Path.GetFileNameWithoutExtension(logPath) + ".debug.log";
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return Path.Combine(directory, fileName);
            }
        }

        var baseDir = Path.GetDirectoryName(qemuPath) ?? _baseDirectory;
        return Path.Combine(baseDir, "qemu.debug.log");
    }

    private string GuessQemuImgPath(string? qemuPath)
    {
        if (string.IsNullOrWhiteSpace(qemuPath))
        {
            return string.Empty;
        }

        var resolved = ResolvePath(qemuPath);
        var directory = Path.GetDirectoryName(resolved);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        return Path.Combine(directory, "qemu-img.exe");
    }
}
