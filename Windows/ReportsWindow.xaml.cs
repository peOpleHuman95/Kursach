using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Globalization;
using System.Linq;

namespace Security_agent
{
    public partial class ReportsWindow : Window
    {
        private readonly AgencyData data;
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private sealed record PaymentRow(DateTime Date, string AgreementNumber, string ClientName, string DocumentNumber, decimal Amount);

        public ReportsWindow(AgencyData data)
        {
            this.data = data;
            
            InitializeComponent();

            DateTime today = DateTime.Today;
            FromPicker.SelectedDate = new DateTime(today.Year, today.Month, 1);
            ToPicker.SelectedDate = today;

            GenerateReport();
        }

        private void GenerateClick(object sender, RoutedEventArgs e)
        {
            GenerateReport();
        }

        private void GenerateReport()
        {
            PrintReportButton.IsEnabled = false;
            PaymentsGrid.ItemsSource = null;

            if (FromPicker.SelectedDate is not DateTime from || ToPicker.SelectedDate is not DateTime to)
            {
                SummaryText.Text = "Выберите обе даты";
                return;
            }

            if (from.Date > to.Date)
            {
                SummaryText.Text = "Дата начала должна быть не позже даты окончания.";
                return;
            }

            var rows = data.Agreements.SelectMany(agreement => agreement.Payments.Where(payment => payment.DatePayment.Date >= from.Date && payment.DatePayment.Date <= to.Date)
            .Select(payment => new PaymentRow(payment.DatePayment, agreement.NumberAgreement, GetClientName(agreement.Client),payment.NumberDocument, payment.Sum)))
            .OrderBy(row => row.Date).ThenBy(row => row.AgreementNumber).ThenBy(row => row.DocumentNumber).ToList();

            PaymentsGrid.ItemsSource = rows;
            PrintReportButton.IsEnabled = true;

            decimal total = rows.Sum(row => row.Amount);
             SummaryText.Text =
                $"Период {from:dd.MM.yyyy} — {to:dd.MM.yyyy}  ·  " +
                $"Платежей: {rows.Count}  ·  Итого: {total.ToString("N2", RussianCulture)} ₽";
        }

        private void PrintReportClick(object sender, RoutedEventArgs e)
        {
            if (PaymentsGrid.ItemsSource == null)
                return;

            try
            {
                var dialog = new PrintDialog();
                if (dialog.ShowDialog() != true)
                    return;

                var document = new FlowDocument
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    PageWidth = dialog.PrintableAreaWidth,
                    PageHeight = dialog.PrintableAreaHeight,
                    PagePadding = new Thickness(32),
                    ColumnWidth = double.PositiveInfinity
                };

                document.Blocks.Add(new Paragraph(new Run("Финансовый отчёт"))
                {
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.MidnightBlue,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                document.Blocks.Add(new Paragraph(new Run(SummaryText.Text))
                {
                    Margin = new Thickness(0, 0, 0, 16)
                });

                var rows = PaymentsGrid.Items.OfType<PaymentRow>().ToList();
                if (rows.Count == 0)
                {
                    document.Blocks.Add(new Paragraph(new Run("За выбранный период платежей нет.")));
                }
                else
                {
                    var table = new Table { CellSpacing = 0 };
                    foreach (double width in new[] { 1.15, 0.9, 2.3, 1.2, 1.25 })
                        table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });

                    var group = new TableRowGroup();
                    table.RowGroups.Add(group);
                    var header = CreatePrintRow("Дата платежа", "Договор", "Клиент", "Документ", "Сумма, ₽");
                    header.Background = Brushes.MidnightBlue;
                    header.Foreground = Brushes.White;
                    header.FontWeight = FontWeights.SemiBold;
                    group.Rows.Add(header);

                    for (int i = 0; i < rows.Count; i++)
                    {
                        PaymentRow row = rows[i];
                        var entry = CreatePrintRow(
                            row.Date.ToString("dd.MM.yyyy", RussianCulture),
                            row.AgreementNumber, row.ClientName, row.DocumentNumber,
                            row.Amount.ToString("N2", RussianCulture));
                        if (i % 2 == 0)
                            entry.Background = Brushes.AliceBlue;
                        group.Rows.Add(entry);
                    }

                    document.Blocks.Add(table);
                }

                var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
                dialog.PrintDocument(paginator, "Финансовый отчёт");
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось распечатать отчёт",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static TableRow CreatePrintRow(params string[] values)
        {
            var row = new TableRow();
            for (int i = 0; i < values.Length; i++)
            {
                row.Cells.Add(new TableCell(new Paragraph(new Run(values[i]))
                {
                    Margin = new Thickness(0),
                    TextAlignment = i == values.Length - 1 ? TextAlignment.Right : TextAlignment.Left
                })
                {
                    Padding = new Thickness(8, 7, 8, 7),
                    BorderBrush = Brushes.LightSteelBlue,
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                });
            }

            return row;
        }

        private static string GetClientName(Client client)
        {
            return client switch
            {
                ClientLegalEntity legal => legal.NameFirm,
                ClientIndividual person => $"{person.LastName} {person.FirstName} {person.MiddleName}".Trim(),
                _ => "-"
            };
        }
    }
}