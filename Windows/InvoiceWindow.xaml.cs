using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    // Формирует счёт и показывает начисление, оплату и остаток.
    public partial class InvoiceWindow : Window
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");
        // Хранит расчётный период и отнесённую к нему сумму оплаты.
        private sealed record InvoicePeriod(BillingPeriod Period, decimal Paid)
        {
            public string Label => $"{Period.StartDate:dd.MM.yyyy} — {Period.EndDate:dd.MM.yyyy}";
            public decimal Remaining => Period.Amount - Paid;
        }

        // Подключает разметку окна и подготавливает его к работе.
        public InvoiceWindow(Agreement agreement, string clientName)
        {
            InitializeComponent();

            ClientText.Text = $"{clientName}\n{agreement.Client.Address}";
            ContractText.Text = $"Договор № {agreement.NumberAgreement} от {agreement.DateAgreement:dd.MM.yyyy}";

            if (agreement is ObjectAgreement obj)
            {
                ServiceText.Text = $"Охрана объекта\n{obj.ObjectAddress}";
                decimal available = agreement.PaidAmount;
                var periods = obj.GetBillingPeriods().Select(period =>
                {
                    decimal applied = Math.Min(available, period.Amount);
                    available -= applied;
                    return new InvoicePeriod(period, applied);
                }).ToList();

                ContractTotalText.Text = $"Полная сумма договора  {Money(obj.CalculateCost())}";
                PeriodPanel.Visibility = Visibility.Visible;
                PeriodBox.ItemsSource = periods;
                PeriodBox.SelectedItem = periods.FirstOrDefault(period => period.Remaining > 0)
                    ?? periods.FirstOrDefault();
            }
            else if (agreement is EventAgreement item)
            {
                ServiceText.Text =
                    $"Охрана мероприятия\n{item.DateEvent:dd.MM.yyyy HH:mm} — {item.EndEvent:dd.MM.yyyy HH:mm}";
                ShowAmounts(agreement.CalculateCost(), agreement.PaidAmount);
            }
        }

        // Обновляет суммы и срок оплаты для выбранного периода.
        private void PeriodChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PeriodBox.SelectedItem is not InvoicePeriod period)
                return;

            DueText.Text = $"Срок оплаты до {period.Period.DueDate:dd.MM.yyyy}";
            ShowAmounts(period.Period.Amount, period.Paid);
        }

        // Выводит начисление, оплату и остаток к оплате.
        private void ShowAmounts(decimal amount, decimal paid)
        {
            decimal remaining = Math.Max(0, amount - paid);
            AmountText.Text = $"Начислено  {Money(amount)}";
            PaidText.Text = $"Оплачено  {Money(paid)}";
            BalanceText.Text = remaining == 0 ? "Оплачено полностью" : $"К оплате  {Money(remaining)}";
        }

        // Форматирует сумму в рублях.
        private static string Money(decimal amount) => amount.ToString("C2", RussianCulture);
    }
}