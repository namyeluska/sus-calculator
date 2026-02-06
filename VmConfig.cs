using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SusCalculator;

internal sealed class VmConfig
{
    public string[] Notes { get; set; } =
    {
        "This file configures the hidden VM trigger and QEMU settings.",
        "secretTrigger.expression format: <number><operator><number> (no spaces). Example: 1337+1",
        "Use '.' as the decimal separator in the expression.",
        "configEditorTrigger.expression opens the config editor GUI.",
        "Set qemu.qemuPath to qemu-system-x86_64.exe.",
        "Disk is persistent. Keep isoPath if you need to install the OS; remove it later to boot from disk only.",
        "bootOrder should be like cd or dc (no comma). You can also use full QEMU syntax like order=dc,menu=on."
    };

    public SecretTriggerConfig SecretTrigger { get; set; } = new();
    public ConfigEditorTriggerConfig ConfigEditorTrigger { get; set; } = new();
    public QemuSettings Qemu { get; set; } = new();
}

internal sealed class SecretTriggerConfig
{
    public string Expression { get; set; } = "1337+1";
}

internal sealed class ConfigEditorTriggerConfig
{
    public string Expression { get; set; } = "404+404";
}

internal sealed class QemuSettings
{
    public string QemuPath { get; set; } = string.Empty;
    public string QemuImgPath { get; set; } = string.Empty;
    public string IsoPath { get; set; } = string.Empty;
    public string DiskPath { get; set; } = @"vm\sus.qcow2";
    public int DiskSizeGB { get; set; } = 40;
    public int MemoryMB { get; set; } = 4096;
    public int Cpus { get; set; } = 2;
    public string BootOrder { get; set; } = "c,d";
    public string Accelerator { get; set; } = string.Empty;
    public string LogPath { get; set; } = "log.txt";
    public string DebugFlags { get; set; } = string.Empty;
    public string[] ExtraArgs { get; set; } =
    {
        "-device", "virtio-net-pci,netdev=net0",
        "-netdev", "user,id=net0"
    };
}

internal sealed class SecretTriggerSpec
{
    public SecretTriggerSpec(double left, double right, Operator op, string raw)
    {
        Left = left;
        Right = right;
        Operator = op;
        RawExpression = raw;
    }

    public double Left { get; }
    public double Right { get; }
    public Operator Operator { get; }
    public string RawExpression { get; }

    public bool Matches(double left, double right, Operator op)
    {
        return op == Operator && NearlyEquals(left, Left) && NearlyEquals(right, Right);
    }

    private static bool NearlyEquals(double a, double b)
    {
        var diff = Math.Abs(a - b);
        return diff <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
    }
}

internal static class SecretTriggerParser
{
    private static readonly Regex ExpressionPattern = new(
        @"^\s*(?<left>[+-]?\d+(\.\d+)?)\s*(?<op>[+\-*/])\s*(?<right>[+-]?\d+(\.\d+)?)\s*$",
        RegexOptions.Compiled);

    public static bool TryParse(string? expression, out SecretTriggerSpec? spec, out string? error)
    {
        spec = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expression))
        {
            error = "secretTrigger.expression is empty.";
            return false;
        }

        var match = ExpressionPattern.Match(expression);
        if (!match.Success)
        {
            error = "Invalid expression format. Use <number><operator><number>, e.g. 1337+1.";
            return false;
        }

        if (!double.TryParse(match.Groups["left"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var left))
        {
            error = "Invalid left operand.";
            return false;
        }

        if (!double.TryParse(match.Groups["right"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var right))
        {
            error = "Invalid right operand.";
            return false;
        }

        if (!TryMapOperator(match.Groups["op"].Value, out var op))
        {
            error = "Unsupported operator. Use +, -, *, or /.";
            return false;
        }

        spec = new SecretTriggerSpec(left, right, op, expression.Trim());
        return true;
    }

    private static bool TryMapOperator(string opText, out Operator op)
    {
        op = Operator.Add;
        if (string.IsNullOrWhiteSpace(opText))
        {
            return false;
        }

        switch (opText[0])
        {
            case '+':
                op = Operator.Add;
                return true;
            case '-':
                op = Operator.Subtract;
                return true;
            case '*':
                op = Operator.Multiply;
                return true;
            case '/':
                op = Operator.Divide;
                return true;
            default:
                return false;
        }
    }
}
