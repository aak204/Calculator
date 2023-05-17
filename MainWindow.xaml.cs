using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using BinCalc;

namespace Calculator
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Максимальное количество цифр, которое будет отображаться в метке памяти, не влияет на фактическое число, хранящееся в памяти
        const int maxMemoryLabelLength = 6;
        // Размер шрифта поля результата по умолчанию 
        const int defaultFontSize = 48;

        // Истина, если выполняется математическая операция
        bool operationCheck;
        // Истинно, если во время другой математической операции над числом была вызвана функция (sin, tan, ln, log и т.д.)
        bool functionCheck;
        // Истина, если окно результата должно быть очищено при вводе числа
        bool clearNext;
        // Истина, если текст в поле результата является результатом какого-либо вычисления
        bool isResult;
        // Истина, если текст в поле результата не был изменен после щелчка на операторе
        bool isOldText;
        // Сохраняет число в памяти, доступ к которой осуществляется через MR
        double memory = 0;
        // Сохраняет текст в текстовом поле после выбора новой математической операции
        string previousText;
        // Тригонометрические режимы
        enum trigModes
        {
            STANDARD,  //  Режим по умолчанию
            HYPERBOLIC,
            ARC
        }
        // Сохраняет текущий тригонометрический режим
        trigModes currentTrigMode;
        // Символы для отображения на кнопке для различных режимов тригонометрии
        Dictionary<trigModes, string> trigModeSymbols = new Dictionary<trigModes, string>()
        {
            { trigModes.STANDARD, "STD" },
            { trigModes.ARC, "ARC" },
            { trigModes.HYPERBOLIC, "HYP" }
        };
        // Хранит текущую единицу измерения угла, по умолчанию это радианы
        Angles.units angleUnit;
        // Символы для отображения на кнопке для различных единиц измерения угла
        Dictionary<Angles.units, string> angleUnitSymbols = new Dictionary<Angles.units, string>()
            {
                { Angles.units.RADIANS, "RAD" },
                { Angles.units.DEGREES, "DEG" },
                { Angles.units.GRADIANS, "GRAD" }
            };
        static string OVERFLOW = "Overflow";
        static string INVALID_INPUT = "Invalid input";
        static string NOT_A_NUMBER = "NaN";
        bool click = true, ones = false;
        string[] errors = { OVERFLOW, INVALID_INPUT, NOT_A_NUMBER };
        operations currentOperation = operations.NULL;
        // Математические операции, принимающие два операнда
        enum operations
        {
            ADDITION,
            SUBTRACTION,
            DIVISION,
            MULTIPLICATION,
            POWER,
            NULL, // Представляет собой отсутствие операции (используется для сброса статуса)
            BINARY_ADDITION,
            BINARY_SUBTRACTION,
            BINARY_MULTIPLICATION,
            BINARY_DIVISION
        }

        public MainWindow()
        {
            InitializeComponent();
            angle_unit_button.Content = angleUnitSymbols[angleUnit];
            trig_mode_button.Content = trigModeSymbols[currentTrigMode];
        }

        /// <summary>
        /// Отображает заданный текст в поле результата и устанавливает значение параметра clearNext в true по умолчанию (false, если указано)
        /// </summary>
        private void showText(string text, bool clear=true)
        {
            try
            {
                if (double.Parse(text) == 0)
                    text = "0";
            }
            catch (Exception)
            {
                showError(INVALID_INPUT);
                return;
            }

            if (text.Length > 30)
                return;
            if (text.Length > 12)
                resultBox.FontSize = 25;
            if (text.Length > 24)
                resultBox.FontSize = 20;

            clearNext = clear;
            resultBox.Text = text;
        }

        /// <summary>
        /// Отображает заданный текст в поле результата
        /// </summary>
        private void showError(string text)
        {
            resultBox.Text = text;
            previousText = null;
            operationCheck = false;
            clearNext = true;
            updateEquationBox("");
            currentOperation = operations.NULL;
            resetFontSize();
        }

        /// <summary>
        /// Обновляет поле уравнения с заданной строкой уравнения.
        /// Если append равен true, то заданный текст добавляется к существующему тексту в окне уравнения
        /// </summary>
        private void updateEquationBox(string equation, bool append=false)
        {
            // Удаляет бессмысленные десятичные знаки из чисел в уравнении
            equation = Regex.Replace(equation, @"(\d+)\.\s", "$1 ");
            
            if (equation.Length > 10)
                equationBox.FontSize = 18;

            if (!append)
                equationBox.Text = equation;
            else
                equationBox.Text += equation;
        }

        /// <summary>
        /// Обновляет текст метки памяти значением в переменной памяти
        /// </summary>
        private void updateMemoryLabel()
        {
            memoryLabel.Content = memory.ToString();
            if (memoryLabel.Content.ToString().Length > maxMemoryLabelLength)
                memoryLabel.Content = memoryLabel.Content.ToString().Substring(0, 5) + "...";
        }

        /// <summary>
        /// Разбирает текст в текстовом поле в тип данных double и возвращает его
        /// </summary>
        private double getNumber()
        {
            double number = double.Parse(resultBox.Text);
            return number;
        }

        /// <summary>
        /// Устанавливает размер шрифта поля результата на значение defaultSize
        /// </summary>
        private void resetFontSize()
        {
            resultBox.FontSize = defaultFontSize;
        }

        private int add(int num1, int num2)
        {
            int res = 0, carry = 0;
            try
            {
                res = num1 ^ num2;
            }
            catch (Exception)
            {
                if (!ones)
                    MessageBox.Show("Отрицательное число!");
                ones = true;
                return 0;
            }
            carry = (num1 & num2) << 1;
            while (carry != 0)
            {
                int tmp = res;
                res = res ^ carry;
                carry = (tmp & carry) << 1;
            }
            ones = false;
            return res;
        }

        // Находим противоположное число n
        // ~: побитовое отрицание
        // добавить: добавить операцию, добавить один к последнему биту
        int negtive(int n)
        {
            return add(~n, 1);
        }

        int subtraction(int a, int b)
        {
            // Добавить противоположный номер вычитаемого числа
            return add(a, negtive(b));
        }

        // Удалить знаковый бит
        int getSign(int n)
        {
            return n >> 31;
        }

        // Находим абсолютное значение n
        int Positive(int n)
        {
            return (getSign(n) & 1) != 0 ? negtive(n) : n;
        }

        int Multiply(int a, int b)
        {
            a = Positive(a);
            b = Positive(b);
            int res = 0;
            while (b != 0)
            {
                // Когда соответствующий бит b равен 1, нужно только добавить
                if ((b & 1) != 0)
                    res = add(res, a);
                a = a << 1; // сдвиг влево
                b = b >> 1; // b сдвиг вправо
            }
            return res;
        }

        int Divide(int a, int b)
        {
            // Делитель не может быть 0
            if (b == 0)
                MessageBox.Show("Делитель не может быть нулевым");

            a = Positive(a);
            b = Positive(b);

            int res = 0;
            while (a >= b)
            {
                res = add(res, 1);
                a = subtraction(a, b);
            }
            return res;
        }


        /// <summary>
        /// Вычисляет результат, решая предыдущийТекст и текущий текст в результате
        /// поле с операндом в currentOperation
        /// </summary>
        private void calculateResult()
        {
            if (currentOperation == operations.NULL)
                return;

            string result;

            if (click)
            {
                double a = double.Parse(previousText); // первый операнд
                double b = double.Parse(resultBox.Text); // второй операнд
                double resultDecimal;

                switch (currentOperation)
                {
                    case operations.DIVISION:
                        resultDecimal = a / b;
                        break;
                    case operations.MULTIPLICATION:
                        resultDecimal = a * b;
                        break;
                    case operations.ADDITION:
                        resultDecimal = a + b;
                        break;
                    case operations.SUBTRACTION:
                        resultDecimal = a - b;
                        break;
                    case operations.POWER:
                        resultDecimal = Math.Pow(a, b);
                        break;
                    default:
                        return;
                }
                result = resultDecimal.ToString();
            }
            else
            {
                int a = Convert.ToInt32(previousText, 2); // первый операнд в двоичном формате
                int b = Convert.ToInt32(resultBox.Text, 2); // второй операнд в двоичном формате

                switch (currentOperation)
                {
                    case operations.BINARY_DIVISION:
                        result = Convert.ToString(Divide(a, b), 2);
                        break;
                    case operations.BINARY_MULTIPLICATION:
                        result = Convert.ToString(Multiply(a, b), 2);
                        break;
                    case operations.BINARY_ADDITION:
                        result = Convert.ToString(add(a, b), 2);
                        break;
                    case operations.BINARY_SUBTRACTION:
                        result = Convert.ToString(subtraction(a, b), 2);
                        break;
                    default:
                        return;
                }
            }

            if (errors.Contains(resultBox.Text))
                return;

            operationCheck = false;
            previousText = null;
            string equation;
            // Если во время выполнения математической операции не была нажата кнопка функции, то в окне уравнения будет текст с.
            // формат <оператив a> <операция> <оператив b как число> else <оператив a> <операция> <функция>(<оператив b>)
            if (!functionCheck)
                equation = equationBox.Text + resultBox.Text;
            else
            {
                equation = equationBox.Text;
                functionCheck = false;
            }
            updateEquationBox(equation);
            showText(result);
            currentOperation = operations.NULL;
            isResult = true;
        }

        /// <summary>
        /// Добавляет нажатую цифру к тексту в текстовом поле.
        /// Если была выбрана текущая операция, то значение текстового поля сначала присваивается переменной previousText, а затем новый текст 
        /// добавляется в текстовое поле после усечения предыдущего текста
        /// </summary>
        private void numberClick(object sender, RoutedEventArgs e)
        {
            isResult = false;
            Button button = (Button)sender;

            if (resultBox.Text == "0" || errors.Contains(resultBox.Text))
                resultBox.Clear();

            string text;

            if (clearNext)
            {
                resetFontSize();
                text = button.Content.ToString();
                isOldText = false;
            }
            else
                text = resultBox.Text + button.Content.ToString();

            if (!operationCheck && equationBox.Text != "")
                updateEquationBox("");
            showText(text, false);
        }

        /// <summary>
        /// Изменяет текущую единицу измерения угла. Также служит для перевода из 2-ой в 16-ю в бин. моде
        /// </summary>
        private void angle_unit_button_Click(object sender, RoutedEventArgs e)
        {
            if (click)
            {
                List<Angles.units> units = new List<Angles.units>()
            {
                Angles.units.RADIANS,
                Angles.units.DEGREES,
                Angles.units.GRADIANS
            };

                Button button = (Button)sender;
                angleUnit = units.ElementAtOrDefault(units.IndexOf(angleUnit) + 1);
                button.Content = angleUnitSymbols[angleUnit];
            }
            else
            {
                if (errors.Contains(resultBox.Text))
                    return;

                double number = getNumber();
                string buttonText = Convert.ToInt32(number).ToString();
                // Переводим из двоичной системы счисления в восьмеричную.
                int decimalNumber = Convert.ToInt32(buttonText, 2);
                string octalString = Convert.ToString(decimalNumber, 8);
                showText(octalString);
            }

        }

        /// <summary>
        /// Изменяет режим тригонометрических функций на нормальный, гиперболический или арк. Также служит для перевода из 2-ой в 8-ю в бин. моде
        /// </summary>
        private void trig_mode_button_Click(object sender, RoutedEventArgs e)
        {
            if (click)
            {
                List<trigModes> modes = new List<trigModes>()
            {
                trigModes.STANDARD,
                trigModes.ARC,
                trigModes.HYPERBOLIC
            };

                Button button = (Button)sender;
                currentTrigMode = modes.ElementAtOrDefault(modes.IndexOf(currentTrigMode) + 1);
                button.Content = trigModeSymbols[currentTrigMode];

                if (currentTrigMode == trigModes.STANDARD)
                {
                    sin_button.Content = "sin";
                    cos_button.Content = "cos";
                    tan_button.Content = "tan";
                }

                if (currentTrigMode == trigModes.HYPERBOLIC)
                {
                    sin_button.Content = "sinh";
                    cos_button.Content = "cosh";
                    tan_button.Content = "tanh";
                }

                if (currentTrigMode == trigModes.ARC)
                {
                    sin_button.Content = "asin";
                    cos_button.Content = "acos";
                    tan_button.Content = "atan";
                }
            }
            else
            {
                if (errors.Contains(resultBox.Text))
                    return;

                double number = getNumber();
                string buttonText = Convert.ToInt32(number).ToString();
                // Переводим из двоичной системы счисления в восьмеричную.
                int decimalNumber = Convert.ToInt32(buttonText, 2);
                string octalString = Convert.ToString(decimalNumber, 16);
                resultBox.Text = octalString.ToUpper();
            }
        }

        /// <summary>
        /// Функция для работы с нажатиями функциональных кнопок
        /// </summary>
        private void function(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;
            
            Button button = (Button)sender;
            string buttonText = button.Content.ToString();
            double number = getNumber();
            string equation = "";
            string result = "";
            switch (buttonText)
            {
                case "!":
                    if (number < 0 || number.ToString().Contains("."))
                    {
                        showError(INVALID_INPUT);
                        return;
                    }

                    if (number > 3248)
                    {
                        showError(OVERFLOW);
                        return;
                    }
                    double res = 1;
                    if (number == 1 || number == 0)
                        result = res.ToString();
                    else
                    {
                        for (int i = 2; i <= number; i++)
                        {
                            res *= i;
                        }
                    }
                    equation = "fact(" + number.ToString() + ")";
                    result = res.ToString();
                    break;

                case "ln":
                    equation = "ln(" + number + ")";
                    result = Math.Log(number).ToString();
                    break;

                case "log":
                    equation = "log(" + number + ")";
                    result = Math.Log10(number).ToString();
                    break;

                case "√":
                    equation = "√(" + number + ")";
                    result = Math.Sqrt(number).ToString();
                    break;

                case "-n":
                    equation = "negate(" + number + ")";
                    result = decimal.Negate((decimal)number).ToString();
                    break;
            }

            if (operationCheck)
            {
                equation = equationBox.Text + equation;
                functionCheck = true;
            }

            updateEquationBox(equation);
            showText(result);
        }

        /// <summary>
        /// Функция для работы с нажатиями кнопок тригонометрических функций
        /// </summary>
        private void trigFunction(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;
            
            Button button = (Button)sender;
            string buttonText = button.Content.ToString();
            string equation = "";
            string result = "";
            double number = getNumber();

            switch (currentTrigMode)
            {
                // Стандартные функции
                case trigModes.STANDARD:
                    double radianAngle = Angles.Converter.radians(number, angleUnit);
                    switch (buttonText)
                    {
                        case "sin":
                            equation = "sin(" + number.ToString() + ")";
                            result = Math.Sin(radianAngle).ToString();
                            break;

                        case "cos":
                            equation = "cos(" + number.ToString() + ")";
                            result = Math.Cos(radianAngle).ToString();
                            break;

                        case "tan":
                            equation = "tan(" + number.ToString() + ")";
                            result = Math.Tan(radianAngle).ToString();
                            break;
                    }
                    break;

                // Гиперболические функции
                case trigModes.HYPERBOLIC:
                    switch(buttonText)
                    {
                        case "sinh":
                            equation = "sinh(" + number + ")";
                            result = Math.Sinh(number).ToString();
                            break;

                        case "cosh":
                            equation = "cosh(" + number + ")";
                            result = Math.Cosh(number).ToString();
                            break;

                        case "tanh":
                            equation = "tanh(" + number + ")";
                            result = Math.Tanh(number).ToString();
                            break;
                    }
                    break;

                // Арк функции
                case trigModes.ARC:
                    switch (buttonText)
                    {
                        case "asin":
                            equation = "asin(" + number + ")";
                            result = Math.Asin(number).ToString();
                            break;

                        case "acos":
                            equation = "acos(" + number + ")";
                            result = Math.Acos(number).ToString();
                            break;

                        case "atan":
                            equation = "atan(" + number + ")";
                            result = Math.Atan(number).ToString();
                            break;
                    }
                    break;
            }

            // Необходимо преобразовать результат в заданную единицу измерения угла, если используются триггерные функции дуги
            if (currentTrigMode == trigModes.ARC)
            {
                switch(angleUnit)
                {
                    case Angles.units.DEGREES:
                        result = Angles.Converter.degrees(double.Parse(result), Angles.units.RADIANS).ToString();
                        break;
                    case Angles.units.GRADIANS:
                        result = Angles.Converter.gradians(double.Parse(result), Angles.units.RADIANS).ToString();
                        break;
                    default:  // 'Результат' по умолчанию указывается в радианах
                        break;
                }
            }

            if (operationCheck)
            {
                equation = equationBox.Text + equation;
                functionCheck = true;
            }

            updateEquationBox(equation);
            showText(result);
        }

        /// <summary>
        /// Функция для работы с кликами функций с двойным операндом
        /// </summary>
        private void doubleOperandFunction(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;

            if (operationCheck && !isOldText)
                calculateResult();

            Button button = (Button)sender;

            operationCheck = true;
            previousText = resultBox.Text;
            string buttonText = button.Content.ToString();
            string equation = previousText + " " + buttonText + " ";
            switch (buttonText)
            {
                case "/":
                    currentOperation = !click ? operations.BINARY_DIVISION : operations.DIVISION;
                    break;
                case "x":
                    currentOperation = !click ? operations.BINARY_MULTIPLICATION : operations.MULTIPLICATION;
                    break;
                case "-":
                    currentOperation = !click ? operations.BINARY_SUBTRACTION : operations.SUBTRACTION;
                    break;
                case "+":
                    currentOperation = !click ? operations.BINARY_ADDITION : operations.ADDITION;
                    break;
                case "^":
                    if (!click)
                    {
                        MessageBox.Show("Работа с ^ недоступна в двоичном режиме");
                        return;
                    }
                    currentOperation = operations.POWER;
                    break;
            }
            updateEquationBox(equation);
            resetFontSize();
            showText(resultBox.Text);
            isOldText = true;
        }

        /// <summary>
        /// Добавляет десятичную точку к числу в поле результата по щелчку,
        /// если число уже имеет десятичную точку, то никаких действий не предпринимается
        /// </summary>
        private void decimal_button_Click(object sender, RoutedEventArgs e)
        {
            if (!resultBox.Text.Contains("."))
            {
                string text = resultBox.Text += ",";
                showText(text, false);
            }
        }

        private void pi_button_Click(object sender, RoutedEventArgs e)
        {
            if (!operationCheck)
                updateEquationBox("");
            showText(Math.PI.ToString());
            isResult = true; // Константы не могут быть изменены
        }

        private void e_button_Click(object sender, RoutedEventArgs e)
        {
            if (!operationCheck)
                updateEquationBox("");
            double number = getNumber();
            showText(Math.Exp(number).ToString());
            isResult = true; // Константы не могут быть изменены
        }

        private void madd_button_Click(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;
            memory += getNumber();
            updateMemoryLabel();
        }

        private void msub_button_Click(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;
            memory -= getNumber();
            updateMemoryLabel();
        }

        private void mc_button_Click(object sender, RoutedEventArgs e)
        {
            memory = 0;
            updateMemoryLabel();
        }

        private void mr_button_Click(object sender, RoutedEventArgs e)
        {
            showText(memory.ToString());
            if (!operationCheck)
                updateEquationBox("");
        }

        private void clear_button_Click(object sender, RoutedEventArgs e)
        {
            resultBox.Text = "0";
            operationCheck = false;
            previousText = null;
            updateEquationBox("");
            resetFontSize();
        }

        private void clr_entry_button_Click(object sender, RoutedEventArgs e)
        {
            resultBox.Text = "0";
            resetFontSize();
        }

        private void equals_button_Click(object sender, RoutedEventArgs e)
        {
            calculateResult();
        }

        // Копирование
        private void copy_button_Click(object sender, RoutedEventArgs e)
        {
            if (errors.Contains(resultBox.Text))
                return;

            Clipboard.SetData(DataFormats.UnicodeText, resultBox.Text);
        }
        
        // Вставка
        private void paste_button_Click(object sender, RoutedEventArgs e)
        {
            object clipboardData = Clipboard.GetData(DataFormats.UnicodeText);
            if (clipboardData != null)
            {
                string data = clipboardData.ToString();
                showText(data.ToString());
            }
            else
                return;
        }

        /// <summary>
        /// Кнопка возврата назад
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void back_button_Click(object sender, RoutedEventArgs e)
        {
            if (isResult)
                return;

            string text;

            if (resultBox.Text.Length == 1)
                text = "0";
            else
                text = resultBox.Text.Substring(0, resultBox.Text.Length - 1);

            showText(text, false);

        }

        /// <summary>
        /// Кнопка для включения бинарного мода
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bin_mode_Click(object sender, RoutedEventArgs e)
        {
            foreach (var button in new Button[] { two_button, three_button, four_button, five_button, six_button, seven_button, eight_button, nine_button,
        negate_button, pi_button, log_button, nlog_button, e_button, tan_button, cos_button, sin_button,
        mr_button, msub_button, mc_button, madd_button, fact_button, power_button, sqrt_button })
            {
                button.IsEnabled = !click;
            }
            if (click)
            {
                click = false;
                resultBox.Text = "";
                angle_unit_button.Content = "В 8";
                trig_mode_button.Content = "В 16";
            }
            else
            {
                click = true;
                angle_unit_button.Content = "RAD";
                trig_mode_button.Content = "STD";
                resultBox.Text = "";
            }
        }
    }
}
