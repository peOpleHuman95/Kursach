using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Security_agent
{
    public partial class AgreementsWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;

        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        private sealed record ClientChoice(Client Client, string Name);
        private sealed record AgreementBox(string Number, string ClientName, string Service, DateTime EndDate, string Staffing);

        public AgreementsWindow(AgencyData data, Storage storage, string dataPath)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;

            InitializeComponent();

            ClientBox.ItemsSource = data.Clients.Select(client => new ClientChoice(client, GetClientName(client))).OrderBy(item => item.Name).ToList();

            RefreshAgreements();
        }

        private static string GetClientName(Client client)
        {
            if (client is ClientLegalEntity legal)
                return legal.NameFirm;
            
            var person = (ClientIndividual)client;
            return $"{person.LastName} {person.FirstName} {person.MiddleName}".Trim();
        }

        private void RefreshAgreements()
        {
            AgreementsGrid.ItemsSource = data.Agreements.OrderByDescending(agreement => agreement.DateAgreement).Select(agreement => 
                new AgreementBox(agreement.NumberAgreement, GetClientName(agreement.Client), agreement is ObjectAgreement ? "Объект" : "Мероприятие", agreement.DateEndContract,
                $"{agreement.Employees.Count} из {agreement.RequiredStaffCount}")).ToList();
        }

        private void SaveAgreementClick(object sender, RoutedEventArgs e)
        {
            try
            {
                EventAgreement agreement = ReadForm();
                data.Agreements.Add(agreement);

                try
                {
                    storage.Save(dataPath, data);
                }
                catch
                {
                    data.Agreements.Remove(agreement);
                    throw;
                }

                RefreshAgreements();
                NumberBox.Clear();
                NumberBox.Focus();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось сохранить договор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private EventAgreement ReadForm()
        {
            if (ClientBox.SelectedItem is not ClientChoice choice)
                throw new ArgumentException("Выбери клиента из списка");
            string number = NumberBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(number))
                throw new ArgumentException("Укажи номер договора.");
            if (data.Agreements.Any(agreement => string.Equals(agreement.NumberAgreement, number, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Договор с таким номеро уже существует.");
            
            DateTime contractStart = ContractStartPicker.SelectedDate ?? throw new ArgumentException("Укажите дату заключения договора.");
            DateTime contractEnd = ContractEndPicker.SelectedDate ?? throw new ArgumentException("Укажите дату окончания договора.");

            return new EventAgreement(
                choice.Client,
                number,
                contractStart,
                contractEnd,
                AddressBox.Text,
                ReadEventTime(EventStartBox.Text, "Начало мероприятия"),
                ReadEventTime(EventEndBox.Text, "Окончание мероприятия"),
                EventNatureBox.Text,
                ReadPositiveInt(ParticipantsBox.Text, "Количество участников"),
                ReadPositiveInt(
                    ParticipantsPerGuardBox.Text, "Участников на одного охранника"),
                ReadHourlyRate(HourlyRateBox.Text));
        }

        private static DateTime ReadEventTime(string text, string field)
        {
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy HH:mm", RussianCulture, DateTimeStyles.None, out DateTime value))
                return value;
            throw new ArgumentException($"Заполните поле {field} в формате дд.мм.гггг чч:мм");
        }

        private static decimal ReadHourlyRate(string text)
        {
            if (decimal.TryParse(text, NumberStyles.Number, RussianCulture, out decimal value) && value > 0)
                return value;
            throw new ArgumentException("Укажите положительный тариф, например 500,00.");
        }
        private static int ReadPositiveInt(string text, string field)
        {
            if (int.TryParse(text, out int value) && value > 0)
                return value;
            throw new ArgumentException($"Поле {field} должно быть целым числом, больше 0");
        }
    }
}