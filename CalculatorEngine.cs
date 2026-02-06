using System;
using System.Globalization;

namespace SusCalculator;

internal enum Operator
{
    Add,
    Subtract,
    Multiply,
    Divide
}

internal sealed class OperationComputedEventArgs : EventArgs
{
    public OperationComputedEventArgs(double left, double right, Operator op, double result)
    {
        Left = left;
        Right = right;
        Operator = op;
        Result = result;
    }

    public double Left { get; }
    public double Right { get; }
    public Operator Operator { get; }
    public double Result { get; }
}

internal sealed class CalculatorEngine
{
    private const string ErrorText = "Error";
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    private string _display = "0";
    private double _accumulator;
    private Operator? _pendingOperator;
    private double? _lastOperand;
    private bool _isNewInput = true;
    private bool _hasError;
    private bool _justEvaluated;

    public string Display => _display;
    public bool HasError => _hasError;
    public string DecimalSeparator => _culture.NumberFormat.NumberDecimalSeparator;

    public event EventHandler<OperationComputedEventArgs>? OperationComputed;

    public void InputDigit(char digit)
    {
        if (_hasError)
        {
            ClearAll();
        }

        if (_justEvaluated)
        {
            ResetAfterEvaluation();
        }

        if (_isNewInput)
        {
            _display = digit == '0' ? "0" : digit.ToString(_culture);
            _isNewInput = false;
            return;
        }

        if (_display == "0")
        {
            _display = digit.ToString(_culture);
            return;
        }

        if (_display == "-0")
        {
            _display = "-" + digit;
            return;
        }

        _display += digit;
    }

    public void InputDecimalSeparator()
    {
        if (_hasError)
        {
            ClearAll();
        }

        if (_justEvaluated)
        {
            ResetAfterEvaluation();
        }

        var separator = DecimalSeparator;

        if (_isNewInput)
        {
            _display = "0" + separator;
            _isNewInput = false;
            return;
        }

        if (!_display.Contains(separator, StringComparison.Ordinal))
        {
            _display += separator;
        }
    }

    public void ToggleSign()
    {
        if (_hasError)
        {
            ClearAll();
        }

        if (_justEvaluated)
        {
            ResetAfterEvaluation();
        }

        if (_display.StartsWith("-", StringComparison.Ordinal))
        {
            _display = _display[1..];
            return;
        }

        if (_display != "0")
        {
            _display = "-" + _display;
            return;
        }

        _display = "-0";
    }

    public void Backspace()
    {
        if (_hasError)
        {
            ClearAll();
            return;
        }

        if (_justEvaluated)
        {
            ResetAfterEvaluation();
        }

        if (_isNewInput)
        {
            _display = "0";
            return;
        }

        if (_display.Length <= 1)
        {
            _display = "0";
            return;
        }

        _display = _display[..^1];
        if (_display == "-" || _display == string.Empty)
        {
            _display = "0";
        }
    }

    public void ClearEntry()
    {
        _display = "0";
        _isNewInput = true;
        _hasError = false;
        _justEvaluated = false;
    }

    public void ClearAll()
    {
        _display = "0";
        _accumulator = 0;
        _pendingOperator = null;
        _lastOperand = null;
        _isNewInput = true;
        _hasError = false;
        _justEvaluated = false;
    }

    public void ApplyOperator(Operator op)
    {
        if (_hasError)
        {
            return;
        }

        if (!_isNewInput)
        {
            var current = ParseDisplay();

            if (_pendingOperator.HasValue)
            {
                var result = ApplyBinary(_accumulator, current, _pendingOperator.Value);
                if (!TrySetResult(result))
                {
                    return;
                }

                _accumulator = result;
                _display = FormatValue(result);
            }
            else
            {
                _accumulator = current;
            }

            _lastOperand = null;
        }

        _pendingOperator = op;
        _isNewInput = true;
    }

    public void Equals()
    {
        if (_hasError || !_pendingOperator.HasValue)
        {
            return;
        }

        var current = ParseDisplay();
        var left = _accumulator;
        double right;

        if (_isNewInput)
        {
            if (!_lastOperand.HasValue)
            {
                _lastOperand = current;
            }

            right = _lastOperand.Value;
            current = ApplyBinary(left, right, _pendingOperator.Value);
        }
        else
        {
            right = current;
            _lastOperand = current;
            current = ApplyBinary(left, right, _pendingOperator.Value);
        }

        if (!TrySetResult(current))
        {
            return;
        }

        _accumulator = current;
        _display = FormatValue(current);
        _isNewInput = true;
        _justEvaluated = true;

        OperationComputed?.Invoke(this, new OperationComputedEventArgs(left, right, _pendingOperator.Value, current));
    }

    public void Percent()
    {
        if (_hasError)
        {
            return;
        }

        var current = ParseDisplay();

        if (_pendingOperator.HasValue)
        {
            current = _accumulator * current / 100.0;
        }
        else
        {
            current /= 100.0;
        }

        if (!TrySetResult(current))
        {
            return;
        }

        _display = FormatValue(current);
        _isNewInput = false;
    }

    public void SquareRoot()
    {
        ApplyUnary(value =>
        {
            if (value < 0)
            {
                return double.NaN;
            }

            return Math.Sqrt(value);
        });
    }

    public void Reciprocal()
    {
        ApplyUnary(value =>
        {
            if (value == 0)
            {
                return double.NaN;
            }

            return 1.0 / value;
        });
    }

    public void Square()
    {
        ApplyUnary(value => value * value);
    }

    private void ApplyUnary(Func<double, double> operation)
    {
        if (_hasError)
        {
            return;
        }

        var current = ParseDisplay();
        var result = operation(current);

        if (!TrySetResult(result))
        {
            return;
        }

        _display = FormatValue(result);
        _isNewInput = false;
    }

    private bool TrySetResult(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            _display = ErrorText;
            _hasError = true;
            _isNewInput = true;
            return false;
        }

        return true;
    }

    private double ParseDisplay()
    {
        if (double.TryParse(_display, NumberStyles.Float, _culture, out var value))
        {
            return value;
        }

        return 0;
    }

    private string FormatValue(double value)
    {
        if (value == 0)
        {
            return "0";
        }

        return value.ToString("G15", _culture);
    }

    private static double ApplyBinary(double left, double right, Operator op)
    {
        return op switch
        {
            Operator.Add => left + right,
            Operator.Subtract => left - right,
            Operator.Multiply => left * right,
            Operator.Divide => right == 0 ? double.NaN : left / right,
            _ => double.NaN
        };
    }

    private void ResetAfterEvaluation()
    {
        _pendingOperator = null;
        _lastOperand = null;
        _accumulator = 0;
        _justEvaluated = false;
    }
}
