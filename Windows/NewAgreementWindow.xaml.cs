using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    // Показывает форму создания и редактирования договора.
    public partial class NewAgreementWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;
        private readonly Agreement? editingAgreement;

        private static readonly CultureInfo RussianCulture =
            CultureInfo.GetCultureInfo("ru-RU");

        public Agreement? SavedAgreement { get; private set; }

        // Подключает разметку окна и подготавливает его к работе.
        public NewAgreementWindow(AgencyData data, Storage storage, string dataPath, Agreement? editingAgreement = null)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;
            this.editingAgreement = editingAgreement;

            InitializeComponent();
            ServiceTypeBox.SelectedIndex = 0;

            var clients = data.Clients
                .Where(client => editingAgreement is not ObjectAgreement || client is ClientLegalEntity)
                .Select(client => new ClientChoice(client, GetClientName(client)))
                .OrderBy(item => item.Name)
                .ToList();

            ClientBox.ItemsSource = clients;
            if (editingAgreement != null)
            {
                ClientBox.SelectedItem = clients.FirstOrDefault(item => item.Client == editingAgreement.Client);
                FillEditForm(editingAgreement);
            }

        }

        // Хранит клиента и подпись для списка выбора.
        private sealed record ClientChoice(Client Client, string Name);

        // Формирует подпись клиента по его типу.
        private static string GetClientName(Client client)
        {
            if (client is ClientLegalEntity legal)
                return legal.NameFirm;
            
            var person = (ClientIndividual)client;
            return $"{person.LastName} {person.FirstName} {person.MiddleName}".Trim();
        }

        // Заполняет поля формы данными существующего договора.
        private void FillEditForm(Agreement agreement)
        {
            Title = "Редактирование договора";
            FormTitle.Text = Title;
            NumberBox.Text = agreement.NumberAgreement;
            ContractStartPicker.SelectedDate = agreement.DateAgreement;
            ContractEndPicker.SelectedDate = agreement.DateEndContract;
            ServiceTypeBox.SelectedIndex = agreement is ObjectAgreement ? 1 : 0;
            ServiceTypeBox.IsEnabled = false;

            if (agreement is EventAgreement item)
            {
                AddressBox.Text = item.EventAddress;
                EventStartBox.Text = item.DateEvent.ToString("dd.MM.yyyy HH:mm", RussianCulture);
                EventEndBox.Text = item.EndEvent.ToString("dd.MM.yyyy HH:mm", RussianCulture);
                EventNatureBox.Text = item.EventNature;
                ParticipantsBox.Text = item.ParticipantsCount.ToString(RussianCulture);
                ParticipantsPerGuardBox.Text = item.ParticipantsPerGuard.ToString(RussianCulture);
                HourlyRateBox.Text = item.HourlyRate.ToString(RussianCulture);
            }
            else if (agreement is ObjectAgreement obj)
            {
                AddressBox.Text = obj.ObjectAddress;
                ObjectTypeBox.Text = obj.ObjectType;
                AreaBox.Text = obj.Area.ToString(RussianCulture);
                AreaPerGuardBox.Text = obj.AreaPerGuard.ToString(RussianCulture);
                MonthlyRateBox.Text = obj.MonthlyRate.ToString(RussianCulture);
            }
        }

        // Проверяет и сохраняет введённые условия договора.
        private void SaveAgreementClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Agreement agreement = ReadForm();
                if (editingAgreement != null)
                {
                    editingAgreement.ApplyEdit(agreement, data.Schedule);
                    storage.Save(dataPath, data);
                    SavedAgreement = editingAgreement;
                    DialogResult = true;
                    return;
                }

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

                SavedAgreement = agreement;
                DialogResult = true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось сохранить договор", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Создаёт договор нужного вида из полей формы.
        private Agreement ReadForm()
        {
            if (ClientBox.SelectedItem is not ClientChoice choice)
                throw new ArgumentException("Выбери клиента из списка.");

            string number = NumberBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(number))
                throw new ArgumentException("Укажи номер договора.");

            if (data.Agreements.Any(agreement => agreement != editingAgreement && string.Equals(
                agreement.NumberAgreement, number, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Договор с таким номером уже существует.");
            }

            DateTime contractStart = ContractStartPicker.SelectedDate
                ?? throw new ArgumentException("Укажи дату заключения договора.");

            DateTime contractEnd = ContractEndPicker.SelectedDate
                ?? throw new ArgumentException("Укажи дату окончания договора.");

            if (ServiceTypeBox.SelectedIndex == 1)
            {
                if (choice.Client is not ClientLegalEntity legal)
                    throw new ArgumentException("Охрана объекта доступна только организациям.");

                return new ObjectAgreement(
                    legal,
                    number,
                    contractStart,
                    contractEnd,
                    AddressBox.Text,
                    ObjectTypeBox.Text,
                    ReadPositiveDecimal(AreaBox.Text, "Площадь объекта"),
                    ReadPositiveDecimal(AreaPerGuardBox.Text, "Площадь на одного охранника"),
                    ReadPositiveDecimal(MonthlyRateBox.Text, "Месячный тариф"));
            }

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

        // Преобразует текст в дату и время мероприятия.
        private static DateTime ReadEventTime(string text, string field)
        {
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy HH:mm", RussianCulture, DateTimeStyles.None, out DateTime value))
                return value;
            throw new ArgumentException($"Заполните поле {field} в формате дд.мм.гггг чч:мм");
        }

        // Читает и проверяет положительное дробное число.
        private static decimal ReadPositiveDecimal(string text, string field)
        {
            if (decimal.TryParse(text, NumberStyles.Number, RussianCulture, out decimal value) && value > 0)
                return value;
            throw new ArgumentException($"Поле {field} должно содержать число больше нуля.");
        }

        // Читает часовой тариф из поля формы.
        private static decimal ReadHourlyRate(string text)
        {
            if (decimal.TryParse(text, NumberStyles.Number, RussianCulture, out decimal value) && value > 0)
                return value;
            throw new ArgumentException("Укажите положительный тариф, например 500,00.");
        }
        // Читает и проверяет положительное целое число.
        private static int ReadPositiveInt(string text, string field)
        {
            if (int.TryParse(text, out int value) && value > 0)
                return value;
            throw new ArgumentException($"Поле {field} должно быть целым числом, больше 0");
        }

        // Подставляет площадь на одного охранника по типу объекта.
        private void ObjectTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AreaPerGuardBox == null ||
                ObjectTypeBox.SelectedItem is not ComboBoxItem selected)
                return;

            string? suggestedArea = selected.Content switch
            {
                "Магазин" or "Торговый центр" => "150",
                "Офис" or "Бизнес-центр" => "300",
                "Склад" or "Производство" => "1500",
                "Банк" => "150",
                _ => null
            };

            if (suggestedArea != null)
                AreaPerGuardBox.Text = suggestedArea;
        }

        // Показывает поля выбранной услуги и проверяет доступность охраны объекта.
        private void AgreementFormChanged(object sender, SelectionChangedEventArgs e)
        {
            bool canOrderObject = ClientBox.SelectedItem is ClientChoice choice && choice.Client is ClientLegalEntity;

            ObjectServiceOption.IsEnabled = canOrderObject;

            if (!canOrderObject && ServiceTypeBox.SelectedIndex == 1)
                ServiceTypeBox.SelectedIndex = 0;
            
            bool isObject = ServiceTypeBox.SelectedIndex == 1;

            EventFields.Visibility = isObject ? Visibility.Collapsed : Visibility.Visible;

            ObjectFields.Visibility = isObject ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
