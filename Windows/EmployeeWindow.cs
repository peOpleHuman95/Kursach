using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Security_agent
{
    // Показывает форму создания, редактирования или просмотра сотрудника.
    public partial class EmployeeWindow : Window
    {
        private readonly AgencyData data;
        private readonly Storage storage;
        private readonly string path;
        private readonly Staff? editedEmployee;

        public Staff? CreatedEmployee {get; private set;}

        // Подключает разметку окна и подготавливает его к работе.
        public EmployeeWindow(AgencyData data, Storage storage, string path, Staff? employee = null)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (string.IsNullOrWhiteSpace(path)) 
                throw new ArgumentException("Не указан путь сохранения.", nameof(path));

            this.path = path;
            InitializeComponent();

            PositionBox.ItemsSource = data.Positions;
            editedEmployee = employee;
            if (employee != null)
            {
                Title = employee.IsDismissed ? "Архивная карточка сотрудника" : "Карточка сотрудника";
                Heading.Text = Title;
                LastNameBox.Text = employee.LastName;
                FirstNameBox.Text = employee.FirstName;
                MiddleNameBox.Text = employee.MiddleName;
                AddressBox.Text = employee.Address;
                PositionBox.SelectedItem = employee.Position;
                AllowanceBox.Text = employee.PersonalAllowance.ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));
                WeaponCheck.IsChecked = employee.CanCarryWeapon;
                CertificateBox.Text = employee.CertificateNumber;
                LicenseBox.Text = employee.LicenseNumber;
                InnBox.Text = employee.Inn;
                PfrBox.Text = employee.PfrNumber;
                PassportBox.Text = employee.Documents.Passport;
                EmploymentBookBox.Text = employee.Documents.EmploymentRecordBook;
                EducationBox.Text = employee.Documents.EducationalDocument;
                MedicalBookBox.Text = employee.Documents.MedicalBook;
                MilitaryCheck.IsChecked = employee.Documents.MilitaryIdCard;
                MedicalCheck.IsChecked = employee.Documents.MedicalReport;
                CriminalCheck.IsChecked = employee.Documents.CriminalRecord;
                if (employee.IsDismissed)
                {
                    EditorTabs.IsEnabled = false;
                    SaveButton.Visibility = Visibility.Collapsed;
                    DetailsText.Text = $"Уволен: {employee.DismissalDate:dd.MM.yyyy} • Хранить до: {employee.KeepUntil:dd.MM.yyyy}";
                }
                else
                    DetailsText.Text = "Измените данные и нажмите «Сохранить».";
            }
        }

        // Подстановка данных выбранной должности
        private void PositionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PositionBox.SelectedItem is Position position)
            {
                SalaryBox.Text = position.BaseSalary.ToString(
                    "0.##",
                    CultureInfo.GetCultureInfo("ru-RU"));

                SalaryBox.IsReadOnly = true;

                GuardCheck.IsChecked = position.IsGuard;
                GuardCheck.IsEnabled = false;
            }
            else
            {
                SalaryBox.IsReadOnly = false;
                GuardCheck.IsEnabled = true;
            }
        } 

        // Проверка обязательного текстового поля
        private static string Required(string text, string field)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException($"Заполните поле «{field}»");
                
            }
            return text.Trim();
        }  

        // Чтение денежной суммы
        private static decimal ReadMoney(string text, string field)
        {
            if (!decimal.TryParse(text,NumberStyles.Number,CultureInfo.GetCultureInfo("ru-RU"), out decimal value) || value < 0)
            {
                 throw new ArgumentException(
                    $"Поле «{field}» должно содержать " +
                    "неотрицательное число. Например: 45000,50.");
            }
            return value;
        }

        // Создание сотрудника без изменения общих списоков
        private Staff BuildEmployee()
        {
            string lastName = Required(LastNameBox.Text, "Фамилия");
            string firstName = Required(FirstNameBox.Text, "Имя");
            string address = Required(AddressBox.Text, "Адрес");
            string positionName = Required(PositionBox.Text, "Должность");
            decimal salary = ReadMoney(SalaryBox.Text, "Оклад");
            decimal allowance = ReadMoney(AllowanceBox.Text, "Надбавка");

            var documents = new EmployeeDocuments();

            documents.Update(
                PassportBox.Text.Trim(),
                MilitaryCheck.IsChecked == true,
                EmploymentBookBox.Text.Trim(),
                EducationBox.Text.Trim(),
                CriminalCheck.IsChecked == true,
                MedicalCheck.IsChecked == true,
                MedicalBookBox.Text.Trim());

            Position? position = data.Positions.Find(item =>
                string.Equals(item.Name, positionName, StringComparison.OrdinalIgnoreCase));
            
            if (position == null)
            {
                position = new Position(positionName, salary, GuardCheck.IsChecked == true);
            }
            else if (position.BaseSalary != salary || 
                position.IsGuard != (GuardCheck.IsChecked == true))
            {
                throw new InvalidOperationException("У этой должности уже задан другой оклад" + 
                    "или признак охранника. Выберите её из списка.");
            }
            var employee = new Staff(position, editedEmployee == null ? documents : new EmployeeDocuments())
            {
                LastName = lastName,
                FirstName = firstName,
                MiddleName = MiddleNameBox.Text.Trim(),
                Address = address,
                PersonalAllowance = allowance,
                CertificateNumber = CertificateBox.Text.Trim(),
                LicenseNumber = LicenseBox.Text.Trim(),
                Inn = InnBox.Text.Trim(),
                PfrNumber = PfrBox.Text.Trim()
            };
                        if (editedEmployee != null)
                employee.Documents.Update(documents.Passport, documents.MilitaryIdCard, documents.EmploymentRecordBook,
                    documents.EducationalDocument, documents.CriminalRecord, documents.MedicalReport, documents.MedicalBook);
            employee.SetWeaponPermission(WeaponCheck.IsChecked == true);

            return employee;
        }

        // Сохранение с отменой добавления при ошибки записи
        private void SaveEditedEmployee(Staff candidate)
        {
            Staff target = editedEmployee!;
            if (target.IsDismissed) throw new InvalidOperationException("Архивная карточка доступна только для просмотра.");
            if (!candidate.CanCarryWeapon && target.AssignedWeapon != null)
                throw new InvalidOperationException("Перед отменой разрешения нужно сдать оружие.");
            if (!candidate.Position.IsGuard && (target.AssignedEquipment.Count > 0 ||
                data.Schedule.Shifts.Any(x => x.ActualGuard == target && x.EndTime > DateTime.Today) ||
                data.Agreements.Any(x => x.Employees.Contains(target) && x.DateEndContract.Date >= DateTime.Today)))
                throw new InvalidOperationException("Перед сменой охранной должности сдайте спецсредства и замените сотрудника в действующих договорах и дежурствах.");
            target.ChangePosition(candidate.Position);
            target.SetWeaponPermission(candidate.CanCarryWeapon);
            target.FirstName = candidate.FirstName;
            target.LastName = candidate.LastName;
            target.MiddleName = candidate.MiddleName;
            target.Address = candidate.Address;
            target.PersonalAllowance = candidate.PersonalAllowance;
            target.CertificateNumber = candidate.CertificateNumber;
            target.LicenseNumber = candidate.LicenseNumber;
            target.Inn = candidate.Inn;
            target.PfrNumber = candidate.PfrNumber;
            var d = candidate.Documents;
            target.Documents.Update(d.Passport, d.MilitaryIdCard, d.EmploymentRecordBook,
                d.EducationalDocument, d.CriminalRecord, d.MedicalReport, d.MedicalBook);
            if (!data.Positions.Contains(target.Position)) data.Positions.Add(target.Position);
            storage.Save(path, data);
        }

        // Добавляет нового сотрудника и при необходимости новую должность.
        private void PersistEmployee(Staff employee)
        {
            bool newPosition = !data.Positions.Contains(employee.Position);

            if (newPosition)
                data.Positions.Add(employee.Position);
            
            data.Employees.Add(employee);

            try
            {
                storage.Save(path, data);
            }
            catch
            {
                data.Employees.Remove(employee);

                if (newPosition)
                    data.Positions.Remove(employee.Position);

                throw;
            }
        }
        // Проверяет форму, сохраняет карточку и закрывает окно при успехе.
        private void SaveClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Staff employee = BuildEmployee();

                if (editedEmployee == null) PersistEmployee(employee);
                else SaveEditedEmployee(employee);

                CreatedEmployee = editedEmployee ?? employee;
                DialogResult = true;
            }
            catch (Exception error)
            {
                MessageBox.Show(
                        this,
                        error.Message,
                        "Не удалось добавить сотрудника",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                
            }
        }
    }
}