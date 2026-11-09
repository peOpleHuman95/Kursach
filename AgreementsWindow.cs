using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    public partial class AgreementsWindow : Window
    {
        private AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;

        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private sealed record AgreementBox(Agreement Agreement ,string Number, string ClientName, string Service, DateTime EndDate, string Staffing);

        private sealed record GuardChoice(int EmployeeIndex, string Name);

        public AgreementsWindow(AgencyData data, Storage storage, string dataPath)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;

            InitializeComponent();
            RefreshAgreements();
        }

        private static string GetClientName(Client client)
        {
            if (client is ClientLegalEntity legal)
                return legal.NameFirm;
            
            var person = (ClientIndividual)client;
            return $"{person.LastName} {person.FirstName} {person.MiddleName}".Trim();
        }

        private void RefreshAgreements(Agreement? selected = null)
        {
            var rows = data.Agreements
                .OrderByDescending(agreement => agreement.DateAgreement)
                .Select(agreement => new AgreementBox(
                    agreement,
                    agreement.NumberAgreement,
                    GetClientName(agreement.Client),
                    agreement is ObjectAgreement ? "Объект" : "Мероприятие",
                    agreement.DateEndContract,
                    $"{agreement.Employees.Count} из {agreement.RequiredStaffCount}"))
                .ToList();

            AgreementsGrid.ItemsSource = rows;
            AgreementsGrid.SelectedItem =
                rows.FirstOrDefault(row => row.Agreement == selected);

            RefreshStaff();
        }

        private void NewAgreementClick(object sender, RoutedEventArgs e)
        {
            if (data.Clients.Count == 0)
            {
                MessageBox.Show(this, "Сначала добавьте клиента в разделе «Клиенты».", "Новый договор");
                return;
            }

            var window = new NewAgreementWindow(data, storage, dataPath) { Owner = this };
            if (window.ShowDialog() == true)
                RefreshAgreements(window.SavedAgreement);
        }

        private void AgreementSelected(object sender, SelectionChangedEventArgs e)
        {
            RefreshStaff();
        }

        private GuardChoice CreateGuardChoice(Staff employee)
        {
            string name = $"{employee.LastName} {employee.FirstName} {employee.MiddleName}".Trim();
            return new GuardChoice(data.Employees.IndexOf(employee), name);
        }
        private void RefreshStaff()
        {
            if (AgreementsGrid.SelectedItem is not AgreementBox row)
            {
                EditAgreementButton.IsEnabled = false;
                EditAgreementButton.ToolTip = "Выберите договор в таблице.";
                AssignedGuardBox.ItemsSource = null;
                AvailableGuardBox.ItemsSource = null;
                StaffSummary.Text = "Выбери договор в таблице.";
                AgreementDetailsText.Text = "Выберите договор в таблице, чтобы увидеть его данные.";
                return;
            }

            Agreement agreement = row.Agreement;
            AgreementDetailsText.Text = GetAgreementDetails(agreement);
            EditAgreementButton.IsEnabled = agreement.CanEdit;
            EditAgreementButton.ToolTip = agreement.CanEdit
                ? "Изменить данные договора."
                : "Договор с платежами нельзя редактировать.";

            AssignedGuardBox.ItemsSource = agreement.Employees
                .Select(CreateGuardChoice)
                .ToList();

            AvailableGuardBox.ItemsSource = data.Employees
                .Where(employee => employee.Position.IsGuard
                    && !employee.IsDismissed
                    && !agreement.Employees.Contains(employee))
                .Select(CreateGuardChoice)
                .ToList();

            string count =
                agreement is ObjectAgreement obj
                    ? $"В смене: {obj.GuardsPerShift}. Для графика: {obj.Employees.Count} из {obj.RequiredStaffCount}"
                    : $"Охранники: {agreement.Employees.Count} из {agreement.RequiredStaffCount}";

            if (agreement.CanCalculateCost)
            {
                string cost = agreement.CalculateCost().ToString("C2", RussianCulture);
                StaffSummary.Text = $"{count}   Стоимость: {cost}";
            }
            else
            {
                StaffSummary.Text = count;
            }
        }

        private string GetAgreementDetails(Agreement agreement)
        {
            string clientType = agreement.Client is ClientLegalEntity
                ? "Юридическое лицо" : "Физическое лицо";
            string common =
                $"Договор № {agreement.NumberAgreement}\n" +
                $"Клиент: {GetClientName(agreement.Client)}\n" +
                $"Тип клиента: {clientType}\n" +
                $"Адрес клиента: {agreement.Client.Address}\n" +
                $"Заключён: {agreement.DateAgreement:dd.MM.yyyy}\n" +
                $"Окончание: {agreement.DateEndContract:dd.MM.yyyy}";

            string service = agreement switch
            {
                EventAgreement item =>
                    $"Вид охраны: мероприятие\nАдрес: {item.EventAddress}\n" +
                    $"Характер: {item.EventNature}\n" +
                    $"Начало: {item.DateEvent:dd.MM.yyyy HH:mm}\n" +
                    $"Окончание: {item.EndEvent:dd.MM.yyyy HH:mm}\n" +
                    $"Участников: {item.ParticipantsCount}\n" +
                    $"Участников на охранника: {item.ParticipantsPerGuard}\n" +
                    $"Тариф на охранника в час: {item.HourlyRate.ToString("C2", RussianCulture)}",
                ObjectAgreement item =>
                    $"Вид охраны: объект\nАдрес: {item.ObjectAddress}\n" +
                    $"Тип объекта: {item.ObjectType}\n" +
                    $"Площадь: {item.Area.ToString("G29", RussianCulture)} м²\n" +
                    $"Площадь на охранника в смене: {item.AreaPerGuard.ToString("G29", RussianCulture)} м²\n" +
                    $"Одновременно в смене: {item.GuardsPerShift}\n" +
                    $"Месячный тариф за место в смене: {item.MonthlyRate.ToString("C2", RussianCulture)}",
                _ => "Вид охраны не указан."
            };

            string guards = agreement.Employees.Count == 0
                ? "Пока никто не назначен."
                : string.Join("\n", agreement.Employees.Select(employee => CreateGuardChoice(employee).Name));

            string cost = agreement.CanCalculateCost
                ? agreement.CalculateCost().ToString("C2", RussianCulture)
                : "Будет рассчитана после назначения охраны.";

            return $"{common}\n\n{service}\n\n" +
                $"Назначено охранников: {agreement.Employees.Count} из {agreement.RequiredStaffCount}\n" +
                $"{guards}\n\nСтоимость: {cost}\n" +
                GetPaymentDetails(agreement);
        }

        private void AssignGuardClick(object sender, RoutedEventArgs e)
        {
            SaveAgreementChange((agreement, working) =>
            {
                Staff employee = ReadGuard(AvailableGuardBox, working);
                agreement.AssignEmployee(employee);
            });
        }

        private void RemoveGuardClick(object sender, RoutedEventArgs e)
        {
            SaveAgreementChange((agreement, working) =>
            {
                Staff employee = ReadGuard(AssignedGuardBox, working);
                agreement.RemoveEmployee(employee);
            });
        }

        private void ReplaceGuardClick(object sender, RoutedEventArgs e)
        {
            SaveAgreementChange((agreement, working) =>
            {
                Staff previous = ReadGuard(AssignedGuardBox, working);
                Staff replacement = ReadGuard(AvailableGuardBox, working);
                agreement.ReplaceEmployee(previous, replacement);
            });
        }

        private static Staff ReadGuard(ComboBox box, AgencyData working)
        {
            if (box.SelectedItem is not GuardChoice choice)
                throw new InvalidOperationException("Выбери охранника в нужном списке.");

            return working.Employees[choice.EmployeeIndex];
        }

        private void EditAgreementClick(object sender, RoutedEventArgs e)
        {
            if (AgreementsGrid.SelectedItem is not AgreementBox row)
                return;

            try
            {
                AgencyData working = storage.Load(dataPath);
                Agreement agreement = working.Agreements.SingleOrDefault(
                    item => string.Equals(item.NumberAgreement, row.Number, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException("Договор не найден в сохранённых данных.");

                if (!agreement.CanEdit)
                    throw new InvalidOperationException("Договор с платежами нельзя редактировать.");

                var window = new NewAgreementWindow(working, storage, dataPath, agreement)
                {
                    Owner = this
                };

                if (window.ShowDialog() == true)
                {
                    data = working;
                    RefreshAgreements(window.SavedAgreement);
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    this, error.Message, "Не удалось открыть договор для редактирования",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveAgreementChange(Action<Agreement, AgencyData> change)
        {
            if (AgreementsGrid.SelectedItem is not AgreementBox row)
            {
                MessageBox.Show(this, "Выбери договор в таблице.", "Состав охраны");
                return;
            }

            try
            {
                int agreementIndex = data.Agreements.IndexOf(row.Agreement);

                // Меняем отдельную копию, пока запись JSON не прошла успешно.
                AgencyData working = storage.Load(dataPath);
                Agreement agreement = working.Agreements[agreementIndex];

                change(agreement, working);
                storage.Save(dataPath, working);

                data = working;

                RefreshAgreements(agreement);
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    this, error.Message, "Не удалось изменить договор",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AddPaymentClick(object sender, RoutedEventArgs e)
        {
            if (AgreementsGrid.SelectedItem is not AgreementBox row)
            {
                MessageBox.Show(this, "Выберите договор в таблице.", "Оплата");
                return;
            }
            try
            {
                if (row.Agreement.Employees.Count < row.Agreement.RequiredStaffCount)
                    throw new InvalidOperationException("Сначала назначьте всех охранников по договору.");

                decimal remaining = row.Agreement.CalculateCost() - row.Agreement.PaidAmount;

                if (remaining <= 0)
                {
                    MessageBox.Show(this, "Договор уже оплачен", "Оплата");
                    return;
                }

                var window = new PaymentWindow(row.Agreement, remaining){Owner = this};

                if (window.ShowDialog() == true && window.EnteredPayment is Payment payment)
                {
                    SaveAgreementChange((agreement, working) => agreement.AddPayment(payment));
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось открыть оплату", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private string GetPaymentDetails(Agreement agreement)
        {
            string Money(decimal amount) => amount.ToString("C2", RussianCulture);

            string text = $"Оплачено: {Money(agreement.PaidAmount)}";

            if (agreement.CanCalculateCost)
            {
                decimal remaining =
                    Math.Max(0, agreement.CalculateCost() - agreement.PaidAmount);

                string status = remaining == 0 ? "Оплачен"
                    : agreement.PaidAmount > 0 ? "Частично оплачен"
                    : "Не оплачен";

                text += $"\nОсталось: {Money(remaining)}\nСтатус: {status}";

                if (agreement is ObjectAgreement obj)
                {
                    text += "\n\nОплата по месяцам";
                    decimal available = agreement.PaidAmount;

                    foreach (BillingPeriod period in obj.GetBillingPeriods())
                    {
                        decimal applied = Math.Min(available, period.Amount);
                        available -= applied;

                        decimal debt = period.Amount - applied;
                        string overdue = debt > 0 && DateTime.Today > period.DueDate
                            ? " — просрочено"
                            : "";

                        text += $"\n\n{period.StartDate:dd.MM.yyyy} — {period.EndDate:dd.MM.yyyy}" +
                                $"\nНачислено: {Money(period.Amount)}" +
                                $"\nОстаток: {Money(debt)}, срок до {period.DueDate:dd.MM.yyyy}{overdue}";
                    }
                }
            }

            if (agreement.Payments.Count > 0)
            {
                text += "\n\nИстория платежей";

                foreach (Payment payment in agreement.Payments.OrderBy(item => item.DatePayment))
                {
                    text += $"\n{payment.DatePayment:dd.MM.yyyy} — " +
                            $"{Money(payment.Sum)}, документ № {payment.NumberDocument}";
                }
            }

            return text;
        }
    }
}