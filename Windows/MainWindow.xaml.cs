using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Security_agent
{
    // Показывает список сотрудников и открывает остальные разделы программы.
    public partial class MainWindow : Window
    {
        private readonly Storage storage = new();
        private AgencyData? data;
        // Хранит значение фильтра должности и его подпись.
        private sealed record PositionFilterChoice(string? Name, string Label);
        private readonly string dataPath = GetDataPath();

        // Находит путь к файлу данных проекта.
        private static string GetDataPath()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Kursc.csproj")))
                    return Path.Combine(directory.FullName, "Data", "data.json");
                directory = directory.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "Data", "data.json");
        }

        // Подключает разметку окна и подготавливает его к работе.
        public MainWindow() { InitializeComponent(); }
        // Загружает сотрудников при открытии главного окна.
        private void Window_Loaded(object sender, RoutedEventArgs e) => LoadEmployees();
        // Перечитывает данные сотрудников по кнопке «Обновить».
        private void ReloadClick(object sender, RoutedEventArgs e) => LoadEmployees();

        // Загружает данные, обновляет счётчики и список сотрудников.
        private void LoadEmployees()
        {
            try
            {
                data = storage.Load(dataPath);
                AddEmployeeButton.IsEnabled = true;
                ActiveCount.Text = data.Employees.Count(x => !x.IsDismissed).ToString();
                GuardCount.Text = data.Employees.Count(x => !x.IsDismissed && x.Position.IsGuard).ToString();
                ArchiveCount.Text = data.Employees.Count(x => x.IsDismissed).ToString();
                RefreshPositionFilter(data);
                RefreshList();
            }
            catch (Exception error)
            {
                data = null;
                PositionFilter.ItemsSource = null;
                EmployeesGrid.ItemsSource = null;
                AddEmployeeButton.IsEnabled = EditButton.IsEnabled = DismissButton.IsEnabled = false;
                ActiveCount.Text = GuardCount.Text = ArchiveCount.Text = "—";
                EmptyText.Visibility = Visibility.Visible;
                EmptyText.Text = "Не удалось загрузить данные.";
                StatusText.Text = "Ошибка загрузки";
                ShowError(error);
            }
        }

        // Обновляет список должностей для фильтра.
        private void RefreshPositionFilter(AgencyData current)
        {
            string? selected = (PositionFilter.SelectedItem as PositionFilterChoice)?.Name;
            var choices = current.Positions
                .Select(position => position.Name)
                .Concat(current.Employees.Select(employee => employee.Position.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new PositionFilterChoice(name, name))
                .Prepend(new PositionFilterChoice(null, "Все должности"))
                .ToList();

            PositionFilter.ItemsSource = choices;
            PositionFilter.SelectedItem = choices.FirstOrDefault(choice =>
                string.Equals(choice.Name, selected, StringComparison.OrdinalIgnoreCase)) ?? choices[0];
        }

        // Применяет поиск и фильтры к списку сотрудников.
        private void RefreshList()
        {
            // События фильтров могут сработать ещё во время InitializeComponent.
            if (data == null || EmployeesGrid == null || StatusFilter == null || PositionFilter == null) return;
            string query = SearchBox.Text.Trim();
            int filter = StatusFilter.SelectedIndex;
            string? position = (PositionFilter.SelectedItem as PositionFilterChoice)?.Name;
            var employees = data.Employees
                .Where(x => filter == 2 || (filter == 1 ? x.IsDismissed : !x.IsDismissed))
                .Where(x => position == null || string.Equals(x.Position.Name, position, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrEmpty(query) ||
                    $"{x.LastName} {x.FirstName} {x.MiddleName} {x.Position.Name}".Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToList();
            EmployeesGrid.ItemsSource = employees;
            EmptyText.Visibility = employees.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyText.Text = query.Length > 0 || position != null ? "По вашему запросу ничего не найдено." :
                filter == 1 ? "Архив пока пуст." : "Сотрудников пока нет. Добавьте первую запись.";
            StatusText.Text = $"Показано: {employees.Count} из {data.Employees.Count}";
            UpdateActions();
        }
        // Обновляет список при изменении текста поиска.
        private void FilterChanged(object sender, TextChangedEventArgs e) => RefreshList();
        // Обновляет список при смене фильтра.
        private void StatusChanged(object sender, SelectionChangedEventArgs e) => RefreshList();
        // Обновляет доступность действий при выборе сотрудника.
        private void EmployeeSelected(object sender, SelectionChangedEventArgs e) => UpdateActions();
        // Включает кнопки в зависимости от выбранной записи.
        private void UpdateActions()
        {
            if (EditButton == null || DismissButton == null) return;
            var employee = EmployeesGrid.SelectedItem as Staff;
            EditButton.IsEnabled = employee != null;
            DismissButton.IsEnabled = employee != null && !employee.IsDismissed;
        }
        // Открывает карточку сотрудника двойным щелчком по строке.
        private void EmployeeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Двойной щелчок по заголовку не открывает старую выбранную запись.
            if (ItemsControl.ContainerFromElement(EmployeesGrid, e.OriginalSource as DependencyObject) is DataGridRow)
                EditClick(sender, e);
        }
        // Открывает форму добавления сотрудника.
        private void AddEmployeeClick(object sender, RoutedEventArgs e)
        {
            if (data == null) return;
            var window = new EmployeeWindow(data, storage, dataPath) { Owner = this };
            window.ShowDialog();
            LoadEmployees();
        }
        // Открывает карточку выбранного сотрудника.
        private void EditClick(object sender, RoutedEventArgs e)
        {
            if (data == null || EmployeesGrid.SelectedItem is not Staff selected) return;
            try
            {
                // Работаем с отдельным графом данных: отмена и ошибка записи не меняют таблицу.
                AgencyData working = storage.Load(dataPath);
                int index = data.Employees.IndexOf(selected);
                var window = new EmployeeWindow(working, storage, dataPath, working.Employees[index]) { Owner = this };
                window.ShowDialog();
                LoadEmployees();
            }
            catch (Exception error) { ShowError(error); }
        }
        // Запрашивает подтверждение увольнения и сохраняет запись.
        private void DismissClick(object sender, RoutedEventArgs e)
        {
            if (data == null || EmployeesGrid.SelectedItem is not Staff selected) return;
            if (MessageBox.Show(this, $"Уволить сотрудника {selected.LastName} {selected.FirstName} сегодняшним числом?\nСведения останутся в архиве.",
                "Увольнение сотрудника", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                AgencyData working = storage.Load(dataPath);
                Staff employee = working.Employees[data.Employees.IndexOf(selected)];
                if (employee.AssignedWeapon != null || employee.AssignedEquipment.Count > 0)
                    throw new InvalidOperationException("Перед увольнением сотрудник должен сдать оружие и спецсредства.");
                if (working.Schedule.Shifts.Any(x => x.ActualGuard == employee && x.EndTime > DateTime.Today) ||
                    working.Agreements.Any(x => x.Employees.Contains(employee) && x.DateEndContract.Date >= DateTime.Today))
                    throw new InvalidOperationException("Сначала замените сотрудника в текущих договорах и дежурствах.");
                employee.Dismiss(DateTime.Today);
                storage.Save(dataPath, working);
                LoadEmployees();
            }
            catch (Exception error) { ShowError(error); }
        }
        // Показывает сообщение об ошибке.
        private void ShowError(Exception error) => MessageBox.Show(this, error.Message, "Не удалось выполнить действие", MessageBoxButton.OK, MessageBoxImage.Warning);

        // Открывает раздел клиентов.
        private void OpenClientsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                AgencyData working = storage.Load(dataPath);

                var window = new ClientsWindow(working, storage, dataPath){Owner = this};

                window.ShowDialog();
                LoadEmployees();
            }
            catch (Exception error)
            {
                ShowError(error);
            }
        }
        // Открывает раздел договоров.
        private void OpenAgreementsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                AgencyData working = storage.Load(dataPath);

                var window = new AgreementsWindow(working, storage, dataPath){Owner = this};

                window.ShowDialog();
                LoadEmployees();
            }
            catch (Exception error)
            {
                ShowError(error);
            }
        }

        // Открывает график дежурств.
        private void OpenDutyClick(object sender, RoutedEventArgs e)
        {
            try
            {
                AgencyData working = storage.Load(dataPath);

                var window = new DutyWindow(working, storage, dataPath){Owner = this};

                window.ShowDialog();
                LoadEmployees();
            }
            catch (Exception error)
            {
                ShowError(error);
            }
        }

        // Открывает учёт снаряжения.
        private void OpenEquipmentClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new EquipmentWindow(storage.Load(dataPath), storage, dataPath){Owner = this};

                window.ShowDialog();
                LoadEmployees();
            }
            catch (Exception error)
            {
                ShowError(error);
            }
        }

        // Открывает финансовый отчёт.
        private void OpenReportsClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new ReportsWindow(storage.Load(dataPath)){Owner = this};

                window.ShowDialog();
            }
            catch (Exception error)
            {
                ShowError(error);
            }
        }
    }
}