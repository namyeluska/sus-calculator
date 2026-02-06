using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SusCalculator;

internal sealed class ConfigEditorForm : Form
{
    private readonly string _configPath;
    private VmConfig _config = new();

    private readonly TextBox _secretTriggerBox = new();
    private readonly TextBox _configEditorTriggerBox = new();
    private readonly TextBox _qemuPathBox = new();
    private readonly TextBox _qemuImgPathBox = new();
    private readonly TextBox _isoPathBox = new();
    private readonly TextBox _diskPathBox = new();
    private readonly NumericUpDown _diskSizeBox = new();
    private readonly NumericUpDown _memoryBox = new();
    private readonly NumericUpDown _cpusBox = new();
    private readonly TextBox _bootOrderBox = new();
    private readonly TextBox _acceleratorBox = new();
    private readonly TextBox _logPathBox = new();
    private readonly TextBox _debugFlagsBox = new();
    private readonly TextBox _extraArgsBox = new();
    private readonly TextBox _notesBox = new();

    private readonly Color _background = Color.FromArgb(26, 26, 28);
    private readonly Color _panel = Color.FromArgb(34, 34, 36);
    private readonly Color _input = Color.FromArgb(20, 20, 22);
    private readonly Color _text = Color.Gainsboro;
    private readonly Color _muted = Color.FromArgb(170, 170, 170);

    public ConfigEditorForm(string configPath)
    {
        _configPath = configPath;

        Text = "VM Configurator";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 620);
        BackColor = _background;
        ForeColor = _text;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            BackColor = _background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));

        var header = BuildHeader();
        var content = BuildContent();
        var buttons = BuildButtons();

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(content, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        LoadConfig();
    }

    private Control BuildHeader()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = _background
        };

        var title = new Label
        {
            Text = "Virtual Machine Settings",
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI Semibold", 13F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = _text
        };

        var path = new Label
        {
            Text = _configPath,
            Dock = DockStyle.Top,
            Height = 18,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = _muted
        };

        panel.Controls.Add(path);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildContent()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _background
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = _background
        };

        stack.Controls.Add(BuildTriggerGroup());
        stack.Controls.Add(BuildPathsGroup());
        stack.Controls.Add(BuildResourcesGroup());
        stack.Controls.Add(BuildBootGroup());
        stack.Controls.Add(BuildLoggingGroup());
        stack.Controls.Add(BuildExtraArgsGroup());
        stack.Controls.Add(BuildNotesGroup());

        scroll.Controls.Add(stack);
        return scroll;
    }

    private Control BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0),
            BackColor = _background
        };

        var save = new StyledButton("Save", Color.FromArgb(60, 130, 246), Color.FromArgb(80, 150, 255), Color.FromArgb(30, 110, 230), 12)
        {
            Width = 110,
            Height = 36,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.White,
            Margin = new Padding(8, 8, 0, 8)
        };
        save.Click += OnSave;

        var cancel = new StyledButton("Cancel", Color.FromArgb(70, 70, 72), Color.FromArgb(85, 85, 88), Color.FromArgb(50, 50, 54), 12)
        {
            Width = 110,
            Height = 36,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.White,
            Margin = new Padding(8, 8, 0, 8)
        };
        cancel.Click += (_, _) => Close();

        panel.Controls.Add(save);
        panel.Controls.Add(cancel);
        return panel;
    }

    private Control BuildTriggerGroup()
    {
        var group = CreateGroup("Triggers");
        var grid = CreateGrid();

        StyleInput(_secretTriggerBox);
        StyleInput(_configEditorTriggerBox);

        AddRow(grid, "Secret trigger", _secretTriggerBox, null);
        AddRow(grid, "Config editor trigger", _configEditorTriggerBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildPathsGroup()
    {
        var group = CreateGroup("Paths");
        var grid = CreateGrid();

        StyleInput(_qemuPathBox);
        StyleInput(_qemuImgPathBox);
        StyleInput(_isoPathBox);
        StyleInput(_diskPathBox);

        AddRow(grid, "QEMU binary", _qemuPathBox, () => BrowseForFile(_qemuPathBox, "QEMU executable|qemu-system-*.exe|All files|*.*"));
        AddRow(grid, "qemu-img binary", _qemuImgPathBox, () => BrowseForFile(_qemuImgPathBox, "qemu-img.exe|qemu-img.exe|All files|*.*"));
        AddRow(grid, "ISO path", _isoPathBox, () => BrowseForFile(_isoPathBox, "ISO files|*.iso|All files|*.*"));
        AddRow(grid, "Disk path", _diskPathBox, () => BrowseForSave(_diskPathBox, "QCOW2 disk|*.qcow2|All files|*.*"));

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildResourcesGroup()
    {
        var group = CreateGroup("Resources");
        var grid = CreateGrid();

        StyleNumeric(_diskSizeBox, 1, 1024, 40, 1);
        StyleNumeric(_memoryBox, 256, 131072, 4096, 256);
        StyleNumeric(_cpusBox, 1, 64, 2, 1);

        AddRow(grid, "Disk size (GB)", _diskSizeBox, null);
        AddRow(grid, "Memory (MB)", _memoryBox, null);
        AddRow(grid, "CPUs", _cpusBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildBootGroup()
    {
        var group = CreateGroup("Boot");
        var grid = CreateGrid();

        StyleInput(_bootOrderBox);
        StyleInput(_acceleratorBox);

        AddRow(grid, "Boot order", _bootOrderBox, null);
        AddRow(grid, "Accelerator", _acceleratorBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildLoggingGroup()
    {
        var group = CreateGroup("Logging");
        var grid = CreateGrid();

        StyleInput(_logPathBox);
        StyleInput(_debugFlagsBox);

        AddRow(grid, "Log file", _logPathBox, () => BrowseForSave(_logPathBox, "Log file|*.log|All files|*.*"));
        AddRow(grid, "Debug flags", _debugFlagsBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildExtraArgsGroup()
    {
        var group = CreateGroup("Extra arguments");
        var grid = CreateGrid();

        StyleInput(_extraArgsBox);
        _extraArgsBox.Multiline = true;
        _extraArgsBox.ScrollBars = ScrollBars.Vertical;
        _extraArgsBox.Height = 120;

        AddRow(grid, "Arguments (one per line)", _extraArgsBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private Control BuildNotesGroup()
    {
        var group = CreateGroup("Notes");
        var grid = CreateGrid();

        StyleInput(_notesBox);
        _notesBox.Multiline = true;
        _notesBox.ReadOnly = true;
        _notesBox.ScrollBars = ScrollBars.Vertical;
        _notesBox.Height = 120;
        _notesBox.ForeColor = _muted;

        AddRow(grid, "Info", _notesBox, null);

        group.Controls.Add(grid);
        return group;
    }

    private GroupBox CreateGroup(string title)
    {
        return new GroupBox
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = _panel,
            ForeColor = _text,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 0, 12)
        };
    }

    private TableLayoutPanel CreateGrid()
    {
        var grid = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            Dock = DockStyle.Top
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f));
        return grid;
    }

    private void AddRow(TableLayoutPanel grid, string label, Control input, Action? browse)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var labelControl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = _muted,
            Padding = new Padding(0, 6, 0, 6),
            AutoSize = true
        };

        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(0, 4, 8, 4);

        Control browseControl;
        if (browse != null)
        {
            var browseButton = new StyledButton("Browse", Color.FromArgb(70, 70, 72), Color.FromArgb(85, 85, 88), Color.FromArgb(50, 50, 54), 10)
            {
                Height = 30,
                Width = 80,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = new Padding(0, 4, 0, 4)
            };
            browseButton.Click += (_, _) => browse();
            browseControl = browseButton;
        }
        else
        {
            browseControl = new Panel { Dock = DockStyle.Fill };
        }

        grid.Controls.Add(labelControl, 0, row);
        grid.Controls.Add(input, 1, row);
        grid.Controls.Add(browseControl, 2, row);
    }

    private void StyleInput(TextBox box)
    {
        box.BackColor = _input;
        box.ForeColor = _text;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private void StyleNumeric(NumericUpDown box, int min, int max, int value, int increment)
    {
        box.Minimum = min;
        box.Maximum = max;
        box.Value = Math.Clamp(value, min, max);
        box.Increment = increment;
        box.BackColor = _input;
        box.ForeColor = _text;
        box.BorderStyle = BorderStyle.FixedSingle;
    }

    private void LoadConfig()
    {
        _config = ConfigLoader.Load(out _);

        _secretTriggerBox.Text = _config.SecretTrigger?.Expression ?? string.Empty;
        _configEditorTriggerBox.Text = _config.ConfigEditorTrigger?.Expression ?? string.Empty;
        _qemuPathBox.Text = _config.Qemu?.QemuPath ?? string.Empty;
        _qemuImgPathBox.Text = _config.Qemu?.QemuImgPath ?? string.Empty;
        _isoPathBox.Text = _config.Qemu?.IsoPath ?? string.Empty;
        _diskPathBox.Text = _config.Qemu?.DiskPath ?? string.Empty;
        _diskSizeBox.Value = Math.Clamp(_config.Qemu?.DiskSizeGB ?? 40, (int)_diskSizeBox.Minimum, (int)_diskSizeBox.Maximum);
        _memoryBox.Value = Math.Clamp(_config.Qemu?.MemoryMB ?? 4096, (int)_memoryBox.Minimum, (int)_memoryBox.Maximum);
        _cpusBox.Value = Math.Clamp(_config.Qemu?.Cpus ?? 2, (int)_cpusBox.Minimum, (int)_cpusBox.Maximum);
        _bootOrderBox.Text = _config.Qemu?.BootOrder ?? string.Empty;
        _acceleratorBox.Text = _config.Qemu?.Accelerator ?? string.Empty;
        _logPathBox.Text = _config.Qemu?.LogPath ?? string.Empty;
        _debugFlagsBox.Text = _config.Qemu?.DebugFlags ?? string.Empty;
        _extraArgsBox.Text = _config.Qemu?.ExtraArgs == null ? string.Empty : string.Join(Environment.NewLine, _config.Qemu.ExtraArgs);
        _notesBox.Text = _config.Notes == null ? string.Empty : string.Join(Environment.NewLine, _config.Notes);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (!SecretTriggerParser.TryParse(_secretTriggerBox.Text, out _, out var secretError))
        {
            MessageBox.Show(this, secretError ?? "Invalid secret trigger.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!SecretTriggerParser.TryParse(_configEditorTriggerBox.Text, out _, out var editorError))
        {
            MessageBox.Show(this, editorError ?? "Invalid config editor trigger.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var config = new VmConfig
        {
            Notes = _config.Notes,
            SecretTrigger = new SecretTriggerConfig { Expression = _secretTriggerBox.Text.Trim() },
            ConfigEditorTrigger = new ConfigEditorTriggerConfig { Expression = _configEditorTriggerBox.Text.Trim() },
            Qemu = new QemuSettings
            {
                QemuPath = _qemuPathBox.Text.Trim(),
                QemuImgPath = _qemuImgPathBox.Text.Trim(),
                IsoPath = _isoPathBox.Text.Trim(),
                DiskPath = _diskPathBox.Text.Trim(),
                DiskSizeGB = (int)_diskSizeBox.Value,
                MemoryMB = (int)_memoryBox.Value,
                Cpus = (int)_cpusBox.Value,
                BootOrder = _bootOrderBox.Text.Trim(),
                Accelerator = _acceleratorBox.Text.Trim(),
                LogPath = _logPathBox.Text.Trim(),
                DebugFlags = _debugFlagsBox.Text.Trim(),
                ExtraArgs = _extraArgsBox.Lines
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToArray()
            }
        };

        if (!ConfigLoader.TrySaveConfig(config, out var error))
        {
            MessageBox.Show(this, error ?? "Failed to save config.", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void BrowseForFile(TextBox target, string filter)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = "Select file"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void BrowseForSave(TextBox target, string filter)
    {
        using var dialog = new SaveFileDialog
        {
            Filter = filter,
            Title = "Select disk image path"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }
}
