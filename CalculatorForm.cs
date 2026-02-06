using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SusCalculator;

internal sealed class CalculatorForm : Form
{
    private readonly CalculatorEngine _engine = new();
    private readonly Label _displayLabel = new();
    private readonly Font _displayFont = new("Segoe UI", 32F, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _buttonFont = new("Segoe UI Semibold", 14F, FontStyle.Regular, GraphicsUnit.Point);
    private readonly VmConfig _config;
    private readonly VmLauncher _vmLauncher;
    private readonly SecretTriggerSpec? _triggerSpec;
    private readonly string? _startupError;

    private readonly Color _backgroundColor = Color.FromArgb(28, 28, 30);
    private readonly Color _displayColor = Color.FromArgb(10, 10, 10);
    private readonly Color _numberButtonColor = Color.FromArgb(58, 58, 60);
    private readonly Color _functionButtonColor = Color.FromArgb(72, 72, 74);
    private readonly Color _operatorButtonColor = Color.FromArgb(255, 149, 0);
    private readonly Color _textLight = Color.White;

    public CalculatorForm()
    {
        Text = "Sus Calculator";
        MinimumSize = new Size(320, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        KeyPreview = true;
        BackColor = _backgroundColor;
        DoubleBuffered = true;

        var layout = BuildLayout();
        Controls.Add(layout);

        _config = ConfigLoader.Load(out _startupError);
        var configPath = ConfigLoader.GetConfigPath();
        var configDirectory = Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory;
        if (!SecretTriggerParser.TryParse(_config.SecretTrigger?.Expression, out _triggerSpec, out var triggerError))
        {
            _triggerSpec = null;
            if (!string.IsNullOrWhiteSpace(triggerError))
            {
                _startupError = string.IsNullOrWhiteSpace(_startupError)
                    ? triggerError
                    : $"{_startupError}{Environment.NewLine}{triggerError}";
            }
        }

        _vmLauncher = new VmLauncher(_config, configDirectory);

        UpdateDisplay();
        KeyDown += OnKeyDown;
        KeyPress += OnKeyPress;
        Shown += OnShown;
        _engine.OperationComputed += OnOperationComputed;
    }

    private TableLayoutPanel BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = 7,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = _backgroundColor
        };

        for (var i = 0; i < 4; i++)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        }

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110f));
        for (var i = 1; i < 7; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6f));
        }

        var displayPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = _displayColor
        };

        _displayLabel.Dock = DockStyle.Fill;
        _displayLabel.TextAlign = ContentAlignment.MiddleRight;
        _displayLabel.Font = _displayFont;
        _displayLabel.ForeColor = _textLight;
        _displayLabel.BackColor = _displayColor;
        _displayLabel.Text = "0";

        displayPanel.Controls.Add(_displayLabel);

        layout.Controls.Add(displayPanel, 0, 0);
        layout.SetColumnSpan(displayPanel, 4);

        var decimalSeparator = _engine.DecimalSeparator;

        AddButton(layout, "%", 0, 1, ButtonKind.Function, (_, _) => { _engine.Percent(); UpdateDisplay(); });
        AddButton(layout, "CE", 1, 1, ButtonKind.Function, (_, _) => { _engine.ClearEntry(); UpdateDisplay(); });
        AddButton(layout, "C", 2, 1, ButtonKind.Function, (_, _) => { _engine.ClearAll(); UpdateDisplay(); });
        AddButton(layout, "Back", 3, 1, ButtonKind.Function, (_, _) => { _engine.Backspace(); UpdateDisplay(); });

        AddButton(layout, "1/x", 0, 2, ButtonKind.Function, (_, _) => { _engine.Reciprocal(); UpdateDisplay(); });
        AddButton(layout, "x^2", 1, 2, ButtonKind.Function, (_, _) => { _engine.Square(); UpdateDisplay(); });
        AddButton(layout, "sqrt", 2, 2, ButtonKind.Function, (_, _) => { _engine.SquareRoot(); UpdateDisplay(); });
        AddButton(layout, "/", 3, 2, ButtonKind.Operator, (_, _) => { _engine.ApplyOperator(Operator.Divide); UpdateDisplay(); });

        AddButton(layout, "7", 0, 3, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "8", 1, 3, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "9", 2, 3, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "*", 3, 3, ButtonKind.Operator, (_, _) => { _engine.ApplyOperator(Operator.Multiply); UpdateDisplay(); });

        AddButton(layout, "4", 0, 4, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "5", 1, 4, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "6", 2, 4, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "-", 3, 4, ButtonKind.Operator, (_, _) => { _engine.ApplyOperator(Operator.Subtract); UpdateDisplay(); });

        AddButton(layout, "1", 0, 5, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "2", 1, 5, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "3", 2, 5, ButtonKind.Number, OnDigitClick);
        AddButton(layout, "+", 3, 5, ButtonKind.Operator, (_, _) => { _engine.ApplyOperator(Operator.Add); UpdateDisplay(); });

        AddButton(layout, "+/-", 0, 6, ButtonKind.Function, (_, _) => { _engine.ToggleSign(); UpdateDisplay(); });
        AddButton(layout, "0", 1, 6, ButtonKind.Number, OnDigitClick);
        AddButton(layout, decimalSeparator, 2, 6, ButtonKind.Number, (_, _) => { _engine.InputDecimalSeparator(); UpdateDisplay(); });
        AddButton(layout, "=", 3, 6, ButtonKind.Operator, (_, _) => { _engine.Equals(); UpdateDisplay(); });

        return layout;
    }

    private void AddButton(
        TableLayoutPanel layout,
        string text,
        int column,
        int row,
        ButtonKind kind,
        EventHandler onClick)
    {
        var (baseColor, textColor) = GetColors(kind);
        var button = new StyledButton(text, baseColor, AdjustColor(baseColor, 18), AdjustColor(baseColor, -18), 0)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Font = _buttonFont,
            ForeColor = textColor
        };

        button.Click += onClick;
        layout.Controls.Add(button, column, row);
    }

    private void OnDigitClick(object? sender, EventArgs e)
    {
        if (sender is Button button && button.Text.Length == 1 && char.IsDigit(button.Text[0]))
        {
            _engine.InputDigit(button.Text[0]);
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        _displayLabel.Text = _engine.Display;
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_startupError))
        {
            MessageBox.Show(this, _startupError, "Config Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnOperationComputed(object? sender, OperationComputedEventArgs e)
    {
        if (_triggerSpec == null)
        {
            return;
        }

        if (!_triggerSpec.Matches(e.Left, e.Right, e.Operator))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            if (_vmLauncher.TryLaunch(out var error))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                return;
            }

            BeginInvoke(new Action(() =>
                MessageBox.Show(this, error, "VM Launch Failed", MessageBoxButtons.OK, MessageBoxIcon.Error)));
        });
    }

    private void OnKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (char.IsDigit(e.KeyChar))
        {
            _engine.InputDigit(e.KeyChar);
            UpdateDisplay();
            e.Handled = true;
            return;
        }

        var separator = _engine.DecimalSeparator;
        if (e.KeyChar.ToString(CultureInfo.CurrentCulture) == separator)
        {
            _engine.InputDecimalSeparator();
            UpdateDisplay();
            e.Handled = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Add:
            case Keys.Oemplus when e.Shift:
                _engine.ApplyOperator(Operator.Add);
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Subtract:
            case Keys.OemMinus:
                _engine.ApplyOperator(Operator.Subtract);
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Multiply:
                _engine.ApplyOperator(Operator.Multiply);
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Divide:
            case Keys.OemQuestion:
                _engine.ApplyOperator(Operator.Divide);
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Enter:
                _engine.Equals();
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Back:
                _engine.Backspace();
                UpdateDisplay();
                e.Handled = true;
                break;
            case Keys.Escape:
                _engine.ClearAll();
                UpdateDisplay();
                e.Handled = true;
                break;
        }
    }

    private (Color Base, Color Text) GetColors(ButtonKind kind)
    {
        return kind switch
        {
            ButtonKind.Operator => (_operatorButtonColor, _textLight),
            ButtonKind.Function => (_functionButtonColor, _textLight),
            _ => (_numberButtonColor, _textLight)
        };
    }

    private static Color AdjustColor(Color color, int delta)
    {
        var r = Math.Clamp(color.R + delta, 0, 255);
        var g = Math.Clamp(color.G + delta, 0, 255);
        var b = Math.Clamp(color.B + delta, 0, 255);
        return Color.FromArgb(r, g, b);
    }

    private enum ButtonKind
    {
        Number,
        Function,
        Operator
    }
}
