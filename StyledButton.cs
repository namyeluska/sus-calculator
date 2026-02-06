using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SusCalculator;

internal sealed class StyledButton : Button
{
    private readonly Color _baseColor;
    private readonly Color _hoverColor;
    private readonly Color _pressedColor;
    private readonly int _cornerRadius;
    private bool _isHovering;
    private bool _isPressed;

    public StyledButton(
        string text,
        Color baseColor,
        Color hoverColor,
        Color pressedColor,
        int cornerRadius = 0)
    {
        Text = text;
        _baseColor = baseColor;
        _hoverColor = hoverColor;
        _pressedColor = pressedColor;
        _cornerRadius = cornerRadius;

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = _baseColor;
        UseVisualStyleBackColor = false;
        TabStop = false;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _isHovering = true;
        if (!_isPressed)
        {
            BackColor = _hoverColor;
        }
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _isHovering = false;
        if (!_isPressed)
        {
            BackColor = _baseColor;
        }
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        _isPressed = true;
        BackColor = _pressedColor;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        _isPressed = false;
        BackColor = _isHovering ? _hoverColor : _baseColor;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        BackColor = Enabled ? _baseColor : ControlPaint.Dark(_baseColor);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    private void UpdateRegion()
    {
        var radius = ResolveCornerRadius();
        if (radius <= 0)
        {
            Region = new Region(new Rectangle(0, 0, Width, Height));
            return;
        }

        var bounds = new Rectangle(0, 0, Width, Height);
        using var path = CreateRoundedRectanglePath(bounds, radius);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = ResolveCornerRadius();
        var background = _isPressed ? _pressedColor : _isHovering ? _hoverColor : _baseColor;
        var highlight = AdjustColor(background, 14);
        var shadow = AdjustColor(background, -12);

        using var path = CreateRoundedRectanglePath(bounds, radius);
        using var brush = new LinearGradientBrush(bounds, highlight, shadow, 90f);
        e.Graphics.FillPath(brush, path);

        using var borderPen = new Pen(Color.FromArgb(80, 255, 255, 255));
        e.Graphics.DrawPath(borderPen, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Suppress default background to reduce flicker.
    }

    private int ResolveCornerRadius()
    {
        var radius = _cornerRadius;
        if (radius <= 0)
        {
            radius = Math.Max(0, Math.Min(Width, Height) / 2 - 1);
        }

        return radius;
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        if (radius <= 0)
        {
            var square = new GraphicsPath();
            square.AddRectangle(bounds);
            return square;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color AdjustColor(Color color, int delta)
    {
        var r = Math.Clamp(color.R + delta, 0, 255);
        var g = Math.Clamp(color.G + delta, 0, 255);
        var b = Math.Clamp(color.B + delta, 0, 255);
        return Color.FromArgb(r, g, b);
    }
}
