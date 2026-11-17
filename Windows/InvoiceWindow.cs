using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Security_agent
{
    // Формирует счёт и показывает начисление, оплату и остаток.
    public partial class InvoiceWindow : UserControl
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

        // Открывает системную печать, где счёт можно сохранить через Microsoft Print to PDF.
        private void PrintInvoiceClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new PrintDialog();
                if (dialog.ShowDialog() != true)
                    return;

                var document = CreatePrintDocument(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                dialog.PrintDocument(paginator, "Счёт клиенту");
            }
            catch (Exception error)
            {
                MessageBox.Show(Window.GetWindow(this), error.Message, "Не удалось распечатать счёт",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Переносит в печатный документ данные счёта и выбранного расчётного периода.
        private FlowDocument CreatePrintDocument(double pageWidth, double pageHeight)
        {
            var document = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                PageWidth = pageWidth,
                PageHeight = pageHeight,
                PagePadding = new Thickness(32),
                ColumnWidth = double.PositiveInfinity
            };

            document.Blocks.Add(new Paragraph(new Run("Счёт клиенту"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.MidnightBlue,
                Margin = new Thickness(0, 0, 0, 12)
            });
            document.Blocks.Add(new Paragraph(new Run(ContractText.Text)));
            document.Blocks.Add(new Paragraph(new Bold(new Run("Клиент")))
            {
                Margin = new Thickness(0, 12, 0, 4)
            });
            document.Blocks.Add(new Paragraph(new Run(ClientText.Text)));
            document.Blocks.Add(new Paragraph(new Run(ServiceText.Text)));

            if (PeriodBox.SelectedItem is InvoicePeriod period)
            {
                document.Blocks.Add(new Paragraph(new Bold(new Run(ContractTotalText.Text))));
                document.Blocks.Add(new Paragraph(new Run($"Расчётный период: {period.Label}")));
                document.Blocks.Add(new Paragraph(new Run(DueText.Text)));
            }

            document.Blocks.Add(new Paragraph(new Run(AmountText.Text))
            {
                BorderBrush = Brushes.LightSteelBlue,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 12, 0, 0),
                Margin = new Thickness(0, 12, 0, 4)
            });
            document.Blocks.Add(new Paragraph(new Run(PaidText.Text))
            {
                Margin = new Thickness(0, 0, 0, 8)
            });
            document.Blocks.Add(new Paragraph(new Run(BalanceText.Text))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0)
            });

            return document;
        }

        // Форматирует сумму в рублях.
        private static string Money(decimal amount) => amount.ToString("C2", RussianCulture);

        // Возвращает в раздел, из которого открыли форму.
        private void CancelClick(object sender, RoutedEventArgs e) =>
            ((MainWindow)Window.GetWindow(this)).CloseForm();
    }
}