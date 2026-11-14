using System;
using System.Globalization;
using System.Windows;

namespace Security_agent
{
    // Показывает форму ввода платежа и остаток по договору.
    public partial class PaymentWindow : Window
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private readonly decimal remaining;
        public Payment? EnteredPayment {get; private set;}

        // Подключает разметку окна и подготавливает его к работе.
        public PaymentWindow(Agreement agreement, decimal remaining)
        {
            this.remaining = remaining;
            InitializeComponent();

            PaymentSummary.Text = $"Договор № {agreement.NumberAgreement}\n" + $"Осталось оплатить {remaining.ToString("C2", RussianCulture)}";

            PaymentDatePicker.SelectedDate = DateTime.Today;
            AmountBox.Text = remaining.ToString("F2", RussianCulture);
            AmountBox.IsReadOnly = agreement is EventAgreement;
        }

        // Проверяет введённые реквизиты и передаёт платёж в окно договоров.
        private void SavePaymentClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!decimal.TryParse(AmountBox.Text, NumberStyles.Number, RussianCulture, out decimal amount) ||
                    amount <= 0 || amount > remaining)
                        throw new ArgumentException("Укажите положительную сумма в пределах остатка.");
                    if (decimal.Round(amount, 2) != amount)
                        throw new ArgumentException("Укажите сумму с точностью до копеек.");
                    DateTime date = PaymentDatePicker.SelectedDate ?? throw new ArgumentException("Выберите дату платежа");

                    if (date.Date > DateTime.Today)
                        throw new ArgumentException("Дата платежа не может быть в будущем.");
                    
                    EnteredPayment = new Payment(amount, date, DocumentNumberBox.Text);
                    DialogResult = true;
            }
            catch (ArgumentException error)
            {
                MessageBox.Show(
                    this, error.Message, "Проверьте платёж",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

        }
    }
}