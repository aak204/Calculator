using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Calculator
{
    public partial class MainWindow : Window
    {
        private enum CalculatorMode
        {
            Standard,
            Programmer
        }

        private enum BinaryOperation
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Power
        }

        private enum TrigMode
        {
            Standard,
            Hyperbolic,
            Arc
        }

        private const double DefaultResultFontSize = 40;
        private const double DefaultEquationFontSize = 22;
        private const string OverflowMessage = "Переполнение";
        private const string InvalidInputMessage = "Некорректный ввод";
        private const string NotANumberMessage = "Не число";
        private const string DivideByZeroMessage = "Деление на ноль";
        private const int FactorialLimit = 170;

        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly Dictionary<Angles.units, string> _angleUnitSymbols = new Dictionary<Angles.units, string>
        {
            { Angles.units.RADIANS, "RAD" },
            { Angles.units.DEGREES, "DEG" },
            { Angles.units.GRADIANS, "GRAD" }
        };

        private readonly Dictionary<TrigMode, string> _trigModeSymbols = new Dictionary<TrigMode, string>
        {
            { TrigMode.Standard, "STD" },
            { TrigMode.Arc, "ARC" },
            { TrigMode.Hyperbolic, "HYP" }
        };

        private readonly List<Button> _binaryRestrictedDigits;
        private readonly List<Button> _disabledInProgrammer;

        private string _decimalSeparator;
        private CalculatorMode _mode = CalculatorMode.Standard;
        private Angles.units _angleUnit = Angles.units.RADIANS;
        private TrigMode _currentTrigMode = TrigMode.Standard;
        private double _memory;

        private double? _leftOperand;
        private long? _binaryLeftOperand;
        private BinaryOperation? _pendingOperation;
        private string _pendingEquationPrefix = string.Empty;

        private bool _clearOnNextDigit;
        private bool _justEvaluated;
        private bool _isTypingNumber;

        public MainWindow()
        {
            InitializeComponent();

            _decimalSeparator = _culture.NumberFormat.NumberDecimalSeparator;
            decimal_button.Content = _decimalSeparator;

            _binaryRestrictedDigits = new List<Button>
            {
                two_button, three_button, four_button, five_button, six_button,
                seven_button, eight_button, nine_button
            };

            _disabledInProgrammer = new List<Button>
            {
                decimal_button, sin_button, cos_button, tan_button, pi_button,
                e_button, log_button, nlog_button, sqrt_button, power_button,
                fact_button, negate_button, madd_button, msub_button,
                mr_button, mc_button
            };

            UpdateMemoryLabel();
            ApplyMode();
        }

        private void DigitButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null)
                return;

            string digit = button.Content.ToString();
            if (_mode == CalculatorMode.Programmer && digit != "0" && digit != "1")
                return;

            string current = resultBox.Text;

            if (_clearOnNextDigit || _justEvaluated)
            {
                current = string.Empty;
                _clearOnNextDigit = false;
                _justEvaluated = false;
            }
            else if (current == "0")
            {
                current = string.Empty;
            }
            else if (current == "-0")
            {
                current = "-";
            }

            string newText = current + digit;
            if (string.IsNullOrEmpty(newText))
                newText = "0";
            else if (newText == "-")
                newText = "-0";

            SetDisplay(newText);
            _isTypingNumber = true;
        }

        private void DecimalButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                return;

            string current = resultBox.Text;
            if (_clearOnNextDigit || _justEvaluated)
            {
                current = "0";
                _clearOnNextDigit = false;
                _justEvaluated = false;
            }

            if (!current.Contains(_decimalSeparator))
            {
                if (current == string.Empty)
                    current = "0";

                current += _decimalSeparator;
                SetDisplay(current);
                _isTypingNumber = true;
            }
        }

        private void BinaryOperator_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            BinaryOperation operation = GetOperation(button.Tag.ToString());

            if (_mode == CalculatorMode.Programmer)
            {
                if (operation == BinaryOperation.Power)
                {
                    MessageBox.Show("Возведение в степень недоступно в двоичном режиме", "Калькулятор", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                PrepareProgrammerOperation(operation);
            }
            else
            {
                PrepareStandardOperation(operation);
            }
        }

        private void equals_button_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                EvaluateProgrammer();
            else
                EvaluateStandard();
        }

        private void UnaryFunction_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            string tag = button.Tag.ToString();

            if (tag == "Negate")
            {
                ToggleSign();
                return;
            }

            double number;
            if (!TryGetDisplayValue(out number))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            if (_mode == CalculatorMode.Programmer)
                return;

            double result;
            string equation;

            try
            {
                switch (tag)
                {
                    case "Sqrt":
                        if (number < 0)
                            throw new InvalidOperationException();
                        result = Math.Sqrt(number);
                        equation = "√(" + FormatNumber(number) + ")";
                        break;
                    case "Factorial":
                        if (number < 0 || Math.Abs(number - Math.Round(number)) > 1e-10)
                            throw new InvalidOperationException();
                        int integer = (int)Math.Round(number);
                        if (integer > FactorialLimit)
                            throw new OverflowException();
                        result = Factorial(integer);
                        equation = "fact(" + FormatNumber(number) + ")";
                        break;
                    case "Ln":
                        if (number <= 0)
                            throw new InvalidOperationException();
                        result = Math.Log(number);
                        equation = "ln(" + FormatNumber(number) + ")";
                        break;
                    case "Log10":
                        if (number <= 0)
                            throw new InvalidOperationException();
                        result = Math.Log10(number);
                        equation = "log(" + FormatNumber(number) + ")";
                        break;
                    default:
                        return;
                }
            }
            catch (OverflowException)
            {
                ShowError(OverflowMessage);
                return;
            }
            catch (InvalidOperationException)
            {
                ShowError(InvalidInputMessage);
                return;
            }

            if (_pendingOperation.HasValue && _leftOperand.HasValue)
                UpdateEquation(_pendingEquationPrefix + equation);
            else
                UpdateEquation(equation);

            DisplayResult(result);
        }

        private void TrigFunction_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                return;

            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            double number;
            if (!TryGetDisplayValue(out number))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            string tag = button.Tag.ToString();
            double result = double.NaN;
            string equation = string.Empty;

            switch (_currentTrigMode)
            {
                case TrigMode.Standard:
                    double radians = Angles.Converter.radians(number, _angleUnit);
                    switch (tag)
                    {
                        case "Sin":
                            result = Math.Sin(radians);
                            equation = "sin(" + FormatNumber(number) + ")";
                            break;
                        case "Cos":
                            result = Math.Cos(radians);
                            equation = "cos(" + FormatNumber(number) + ")";
                            break;
                        case "Tan":
                            result = Math.Tan(radians);
                            equation = "tan(" + FormatNumber(number) + ")";
                            break;
                    }
                    break;
                case TrigMode.Hyperbolic:
                    switch (tag)
                    {
                        case "Sin":
                            result = Math.Sinh(number);
                            equation = "sinh(" + FormatNumber(number) + ")";
                            break;
                        case "Cos":
                            result = Math.Cosh(number);
                            equation = "cosh(" + FormatNumber(number) + ")";
                            break;
                        case "Tan":
                            result = Math.Tanh(number);
                            equation = "tanh(" + FormatNumber(number) + ")";
                            break;
                    }
                    break;
                case TrigMode.Arc:
                    switch (tag)
                    {
                        case "Sin":
                            result = Math.Asin(number);
                            equation = "asin(" + FormatNumber(number) + ")";
                            break;
                        case "Cos":
                            result = Math.Acos(number);
                            equation = "acos(" + FormatNumber(number) + ")";
                            break;
                        case "Tan":
                            result = Math.Atan(number);
                            equation = "atan(" + FormatNumber(number) + ")";
                            break;
                    }

                    if (!double.IsNaN(result))
                    {
                        if (_angleUnit == Angles.units.DEGREES)
                            result = Angles.Converter.degrees(result, Angles.units.RADIANS);
                        else if (_angleUnit == Angles.units.GRADIANS)
                            result = Angles.Converter.gradians(result, Angles.units.RADIANS);
                    }
                    break;
            }

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                ShowError(NotANumberMessage);
                return;
            }

            if (_pendingOperation.HasValue && _leftOperand.HasValue)
                UpdateEquation(_pendingEquationPrefix + equation);
            else
                UpdateEquation(equation);

            DisplayResult(result);
        }

        private void PrepareStandardOperation(BinaryOperation operation)
        {
            double current;
            if (!TryGetDisplayValue(out current))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            if (_pendingOperation.HasValue && _leftOperand.HasValue && !_clearOnNextDigit && !_justEvaluated)
            {
                double intermediate;
                if (!ExecuteStandard(_leftOperand.Value, current, _pendingOperation.Value, out intermediate))
                    return;

                SetDisplay(FormatNumber(intermediate));
                _leftOperand = intermediate;
            }
            else
            {
                _leftOperand = current;
            }

            _pendingOperation = operation;
            _pendingEquationPrefix = FormatNumber(_leftOperand.Value) + " " + GetOperationSymbol(operation) + " ";
            UpdateEquation(_pendingEquationPrefix);

            _clearOnNextDigit = true;
            _justEvaluated = false;
            _isTypingNumber = false;
        }

        private void PrepareProgrammerOperation(BinaryOperation operation)
        {
            long current;
            if (!TryGetBinaryValue(resultBox.Text, out current))
                return;

            if (_pendingOperation.HasValue && _binaryLeftOperand.HasValue && !_clearOnNextDigit && !_justEvaluated)
            {
                long intermediate;
                if (!ExecuteProgrammer(_binaryLeftOperand.Value, current, _pendingOperation.Value, out intermediate))
                    return;

                SetDisplay(Convert.ToString(intermediate, 2));
                _binaryLeftOperand = intermediate;
            }
            else
            {
                _binaryLeftOperand = current;
            }

            _pendingOperation = operation;
            _pendingEquationPrefix = Convert.ToString(_binaryLeftOperand.Value, 2) + " " + GetOperationSymbol(operation) + " ";
            UpdateEquation(_pendingEquationPrefix);

            _clearOnNextDigit = true;
            _justEvaluated = false;
            _isTypingNumber = false;
        }

        private void EvaluateStandard()
        {
            if (!_pendingOperation.HasValue || !_leftOperand.HasValue)
                return;

            double right;
            if (!TryGetDisplayValue(out right))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            double result;
            if (!ExecuteStandard(_leftOperand.Value, right, _pendingOperation.Value, out result))
                return;

            string equation = equationBox.Text;
            if (string.IsNullOrWhiteSpace(equation) || equation == _pendingEquationPrefix)
                equation = _pendingEquationPrefix + FormatNumber(right);

            UpdateEquation(equation + " =");
            DisplayResult(result);

            _leftOperand = result;
            _pendingOperation = null;
            _pendingEquationPrefix = string.Empty;
        }

        private void EvaluateProgrammer()
        {
            if (!_pendingOperation.HasValue || !_binaryLeftOperand.HasValue)
                return;

            long right;
            if (!TryGetBinaryValue(resultBox.Text, out right))
                return;

            long result;
            if (!ExecuteProgrammer(_binaryLeftOperand.Value, right, _pendingOperation.Value, out result))
                return;

            string rightText = Convert.ToString(right, 2);
            string equation = equationBox.Text;
            if (string.IsNullOrWhiteSpace(equation) || equation == _pendingEquationPrefix)
                equation = _pendingEquationPrefix + rightText;

            UpdateEquation(equation + " =");
            DisplayBinaryResult(result);

            _binaryLeftOperand = result;
            _pendingOperation = null;
            _pendingEquationPrefix = string.Empty;
        }

        private bool ExecuteStandard(double left, double right, BinaryOperation operation, out double result)
        {
            result = double.NaN;
            try
            {
                switch (operation)
                {
                    case BinaryOperation.Add:
                        result = left + right;
                        break;
                    case BinaryOperation.Subtract:
                        result = left - right;
                        break;
                    case BinaryOperation.Multiply:
                        result = left * right;
                        break;
                    case BinaryOperation.Divide:
                        if (right == 0)
                            throw new DivideByZeroException();
                        result = left / right;
                        break;
                    case BinaryOperation.Power:
                        result = Math.Pow(left, right);
                        break;
                }

                if (double.IsInfinity(result) || double.IsNaN(result))
                    throw new OverflowException();
            }
            catch (DivideByZeroException)
            {
                ShowError(DivideByZeroMessage);
                return false;
            }
            catch (OverflowException)
            {
                ShowError(OverflowMessage);
                return false;
            }

            return true;
        }

        private bool ExecuteProgrammer(long left, long right, BinaryOperation operation, out long result)
        {
            result = 0;
            try
            {
                switch (operation)
                {
                    case BinaryOperation.Add:
                        result = checked(left + right);
                        break;
                    case BinaryOperation.Subtract:
                        result = checked(left - right);
                        break;
                    case BinaryOperation.Multiply:
                        result = checked(left * right);
                        break;
                    case BinaryOperation.Divide:
                        if (right == 0)
                            throw new DivideByZeroException();
                        result = checked(left / right);
                        break;
                    default:
                        result = left;
                        break;
                }
            }
            catch (DivideByZeroException)
            {
                ShowError(DivideByZeroMessage);
                return false;
            }
            catch (OverflowException)
            {
                ShowError(OverflowMessage);
                return false;
            }

            return true;
        }

        private void DisplayResult(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                ShowError(OverflowMessage);
                return;
            }

            SetDisplay(FormatNumber(value));
            _clearOnNextDigit = true;
            _justEvaluated = true;
            _isTypingNumber = false;
        }

        private void DisplayBinaryResult(long value)
        {
            SetDisplay(Convert.ToString(value, 2));
            _clearOnNextDigit = true;
            _justEvaluated = true;
            _isTypingNumber = false;
        }

        private void pi_button_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                return;

            UpdateEquation("π");
            DisplayResult(Math.PI);
        }

        private void e_button_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                return;

            double number;
            if (!TryGetDisplayValue(out number))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            double value = Math.Exp(number);
            if (double.IsInfinity(value) || double.IsNaN(value))
            {
                ShowError(OverflowMessage);
                return;
            }

            UpdateEquation("exp(" + FormatNumber(number) + ")");
            DisplayResult(value);
        }

        private void MemoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
                return;

            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            double number;
            if (!TryGetDisplayValue(out number))
            {
                ShowError(InvalidInputMessage);
                return;
            }

            switch (button.Tag.ToString())
            {
                case "MC":
                    _memory = 0;
                    break;
                case "MR":
                    DisplayResult(_memory);
                    break;
                case "MPlus":
                    _memory += number;
                    break;
                case "MMinus":
                    _memory -= number;
                    break;
            }

            UpdateMemoryLabel();
        }

        private void UpdateMemoryLabel()
        {
            string value = FormatNumber(_memory);
            if (Math.Abs(_memory) < double.Epsilon)
                value = "0";

            if (value.Length > 12)
                value = value.Substring(0, 12) + "…";

            memoryLabel.Text = "Память: " + value;
        }

        private void clear_button_Click(object sender, RoutedEventArgs e)
        {
            ResetState();
            SetDisplay("0");
            UpdateEquation(string.Empty);
        }

        private void clr_entry_button_Click(object sender, RoutedEventArgs e)
        {
            SetDisplay("0");
            _clearOnNextDigit = false;
            _justEvaluated = false;
            _isTypingNumber = false;
        }

        private void back_button_Click(object sender, RoutedEventArgs e)
        {
            if (_clearOnNextDigit || _justEvaluated)
            {
                SetDisplay("0");
                _clearOnNextDigit = false;
                _justEvaluated = false;
                return;
            }

            string text = resultBox.Text;
            if (text.Length <= 1 || (text.Length == 2 && text.StartsWith("-", StringComparison.Ordinal)))
            {
                SetDisplay("0");
                return;
            }

            text = text.Substring(0, text.Length - 1);
            if (text == "-")
                text = "-0";

            SetDisplay(text);
        }

        private void copy_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(resultBox.Text);
            }
            catch
            {
                MessageBox.Show("Не удалось скопировать текст", "Калькулятор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void paste_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string data = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(data))
                    return;

                string sanitized = data.Trim();

                if (_mode == CalculatorMode.Programmer)
                {
                    sanitized = Regex.Replace(sanitized, "[^01]", string.Empty);
                    if (string.IsNullOrEmpty(sanitized))
                    {
                        ShowError(InvalidInputMessage);
                        return;
                    }

                    SetDisplay(sanitized);
                }
                else
                {
                    double value;
                    if (!double.TryParse(sanitized, NumberStyles.Float, _culture, out value))
                    {
                        if (!double.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                        {
                            ShowError(InvalidInputMessage);
                            return;
                        }
                    }

                    SetDisplay(FormatNumber(value));
                }

                _clearOnNextDigit = true;
                _justEvaluated = true;
                _isTypingNumber = false;
            }
            catch
            {
                MessageBox.Show("Не удалось вставить данные", "Калькулятор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void angle_unit_button_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
            {
                long value;
                if (!TryGetBinaryValue(resultBox.Text, out value))
                    return;

                string octal = Convert.ToString(value, 8);
                UpdateEquation("BIN→OCT(" + Convert.ToString(value, 2) + ")");
                SetDisplay(octal);
                _clearOnNextDigit = true;
                _justEvaluated = true;
                _isTypingNumber = false;
                return;
            }

            if (_angleUnit == Angles.units.RADIANS)
                _angleUnit = Angles.units.DEGREES;
            else if (_angleUnit == Angles.units.DEGREES)
                _angleUnit = Angles.units.GRADIANS;
            else
                _angleUnit = Angles.units.RADIANS;

            UpdateAngleUnitVisuals();
        }

        private void trig_mode_button_Click(object sender, RoutedEventArgs e)
        {
            if (_mode == CalculatorMode.Programmer)
            {
                long value;
                if (!TryGetBinaryValue(resultBox.Text, out value))
                    return;

                string hex = Convert.ToString(value, 16).ToUpperInvariant();
                UpdateEquation("BIN→HEX(" + Convert.ToString(value, 2) + ")");
                SetDisplay(hex);
                _clearOnNextDigit = true;
                _justEvaluated = true;
                _isTypingNumber = false;
                return;
            }

            if (_currentTrigMode == TrigMode.Standard)
                _currentTrigMode = TrigMode.Arc;
            else if (_currentTrigMode == TrigMode.Arc)
                _currentTrigMode = TrigMode.Hyperbolic;
            else
                _currentTrigMode = TrigMode.Standard;

            UpdateTrigModeVisuals();
        }

        private void bin_mode_Click(object sender, RoutedEventArgs e)
        {
            _mode = _mode == CalculatorMode.Standard ? CalculatorMode.Programmer : CalculatorMode.Standard;
            ApplyMode();
        }

        private void ApplyMode()
        {
            bool programmer = _mode == CalculatorMode.Programmer;

            foreach (Button button in _binaryRestrictedDigits)
                button.IsEnabled = !programmer;

            foreach (Button button in _disabledInProgrammer)
                button.IsEnabled = !programmer;

            bin_mode.Content = programmer ? "Стандарт" : "Программист";

            ResetState();
            SetDisplay("0");
            UpdateEquation(string.Empty);
            UpdateAngleUnitVisuals();
            UpdateTrigModeVisuals();
        }

        private void ToggleSign()
        {
            string text = resultBox.Text;
            if (text.StartsWith("-", StringComparison.Ordinal))
                text = text.Substring(1);
            else if (text != "0")
                text = "-" + text;
            else
                text = "-0";

            SetDisplay(text);
            _clearOnNextDigit = false;
            _justEvaluated = false;
        }

        private void SetDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
                text = "0";

            resultBox.Text = text;
            AdjustResultFont(text);
        }

        private void UpdateEquation(string equation)
        {
            equationBox.Text = equation;
            AdjustEquationFont(equation);
        }

        private void ResetState()
        {
            _clearOnNextDigit = false;
            _justEvaluated = false;
            _isTypingNumber = false;
            _leftOperand = null;
            _binaryLeftOperand = null;
            _pendingOperation = null;
            _pendingEquationPrefix = string.Empty;
            ResetFonts();
        }

        private void ResetFonts()
        {
            resultBox.FontSize = DefaultResultFontSize;
            equationBox.FontSize = DefaultEquationFontSize;
        }

        private void AdjustResultFont(string text)
        {
            if (text.Length > 20)
                resultBox.FontSize = 26;
            else if (text.Length > 14)
                resultBox.FontSize = 32;
            else
                resultBox.FontSize = DefaultResultFontSize;
        }

        private void AdjustEquationFont(string text)
        {
            if (text.Length > 40)
                equationBox.FontSize = 16;
            else if (text.Length > 24)
                equationBox.FontSize = 18;
            else
                equationBox.FontSize = DefaultEquationFontSize;
        }

        private void ShowError(string message)
        {
            ResetState();
            SetDisplay(message);
            UpdateEquation(string.Empty);
            _clearOnNextDigit = true;
        }

        private bool TryGetDisplayValue(out double number)
        {
            return double.TryParse(resultBox.Text, NumberStyles.Float, _culture, out number);
        }

        private bool TryGetBinaryValue(string text, out long value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowError(InvalidInputMessage);
                return false;
            }

            try
            {
                value = Convert.ToInt64(text, 2);
                return true;
            }
            catch
            {
                ShowError(InvalidInputMessage);
                return false;
            }
        }

        private static double Factorial(int n)
        {
            double result = 1;
            for (int i = 2; i <= n; i++)
                result *= i;
            return result;
        }

        private string FormatNumber(double value)
        {
            string formatted = value.ToString("G15", _culture);

            if (formatted.Contains(_decimalSeparator))
            {
                formatted = formatted.TrimEnd('0');
                if (formatted.EndsWith(_decimalSeparator))
                    formatted = formatted.Substring(0, formatted.Length - _decimalSeparator.Length);
            }

            return string.IsNullOrEmpty(formatted) ? "0" : formatted;
        }

        private static BinaryOperation GetOperation(string tag)
        {
            switch (tag)
            {
                case "Add":
                    return BinaryOperation.Add;
                case "Subtract":
                    return BinaryOperation.Subtract;
                case "Multiply":
                    return BinaryOperation.Multiply;
                case "Divide":
                    return BinaryOperation.Divide;
                case "Power":
                    return BinaryOperation.Power;
                default:
                    throw new InvalidOperationException();
            }
        }

        private static string GetOperationSymbol(BinaryOperation operation)
        {
            switch (operation)
            {
                case BinaryOperation.Add:
                    return "+";
                case BinaryOperation.Subtract:
                    return "-";
                case BinaryOperation.Multiply:
                    return "×";
                case BinaryOperation.Divide:
                    return "÷";
                case BinaryOperation.Power:
                    return "^";
                default:
                    return string.Empty;
            }
        }

        private void UpdateAngleUnitVisuals()
        {
            if (_mode == CalculatorMode.Programmer)
                angle_unit_button.Content = "BIN→OCT";
            else
                angle_unit_button.Content = _angleUnitSymbols[_angleUnit];
        }

        private void UpdateTrigModeVisuals()
        {
            if (_mode == CalculatorMode.Programmer)
            {
                trig_mode_button.Content = "BIN→HEX";
                sin_button.Content = "sin";
                cos_button.Content = "cos";
                tan_button.Content = "tan";
                return;
            }

            trig_mode_button.Content = _trigModeSymbols[_currentTrigMode];

            switch (_currentTrigMode)
            {
                case TrigMode.Standard:
                    sin_button.Content = "sin";
                    cos_button.Content = "cos";
                    tan_button.Content = "tan";
                    break;
                case TrigMode.Hyperbolic:
                    sin_button.Content = "sinh";
                    cos_button.Content = "cosh";
                    tan_button.Content = "tanh";
                    break;
                case TrigMode.Arc:
                    sin_button.Content = "asin";
                    cos_button.Content = "acos";
                    tan_button.Content = "atan";
                    break;
            }
        }
    }
}
