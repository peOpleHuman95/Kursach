using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace Security_agent
{
    public partial class DutyWindow : Window
    {
        private AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;
        private DateTime? filterFrom;
        private DateTime? filterTo;
        private int? filterGuardIndex;

        private sealed record ObjectChoice(ObjectAgreement Agreement, string Name);
        private sealed record GuardChoice(Staff Employee, string Name);
        private sealed record GuardFilterChoice(int? EmployeeIndex, string Name);
        private sealed record ShiftRow(
            DutyShift Shift,
            string Start,
            string End,
            string PlannedGuard,
            string ActualGuard);

        public DutyWindow(AgencyData data, Storage storage, string dataPath)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPath;

            InitializeComponent();
            RefreshObjects();
        }

        private static string GetGuardName(Staff employee)
        {
            return $"{employee.LastName} {employee.FirstName} {employee.MiddleName}".Trim();
        }

        private void RefreshObjects(ObjectAgreement? selected = null)
        {
            var objects = data.Agreements
                .OfType<ObjectAgreement>()
                .OrderBy(agreement => agreement.NumberAgreement)
                .Select(agreement => new ObjectChoice(
                    agreement, $"№ {agreement.NumberAgreement} — {agreement.ObjectAddress}"))
                .ToList();

            ObjectBox.ItemsSource = objects;
            ObjectBox.SelectedItem =
                objects.FirstOrDefault(item => item.Agreement == selected)
                ?? objects.FirstOrDefault();

            RefreshShifts();
        }

        private void ObjectChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshShifts();

            FillStatus.Text = string.Empty;
            if (ObjectBox.SelectedItem is ObjectChoice choice)
                ShiftDatePicker.SelectedDate = choice.Agreement.DateAgreement.Date;
        }

        private void RefreshShifts(DutyShift? selected = null)
        {
            if (ObjectBox.SelectedItem is not ObjectChoice choice)
            {
                RosterText.Text = string.Empty;
                FilterStatus.Text = string.Empty;
                FilterGuardBox.ItemsSource = null;
                GuardBox.ItemsSource = null;
                ShiftsGrid.ItemsSource = null;
                HistoryText.Text = "Выберите договор на охрану объекта.";
                return;
            }

            int assigned = choice.Agreement.Employees.Count(
                employee => employee.Position.IsGuard && !employee.IsDismissed);
            int required = choice.Agreement.RequiredStaffCount;
            int missing = Math.Max(0, required - assigned);

            RosterText.Text =
                $"В смене: {choice.Agreement.GuardsPerShift}. Для графика: {assigned} из {required}." +
                (missing > 0 ? $" Не хватает: {missing}." : "");

            GuardBox.ItemsSource = choice.Agreement.Employees
                .Where(employee => employee.Position.IsGuard && !employee.IsDismissed)
                .Select(employee => new GuardChoice(employee, GetGuardName(employee)))
                .OrderBy(item => item.Name)
                .ToList();

            var objectShifts = data.Schedule.Shifts
                .Where(shift => shift.Agreement == choice.Agreement)
                .ToList();
            var guardChoices = objectShifts
                .Select(shift => shift.ActualGuard)
                .Distinct()
                .OrderBy(GetGuardName)
                .Select(guard => new GuardFilterChoice(data.Employees.IndexOf(guard), GetGuardName(guard)))
                .Prepend(new GuardFilterChoice(null, "Все охранники"))
                .ToList();

            FilterGuardBox.ItemsSource = guardChoices;
            var selectedFilter = guardChoices.FirstOrDefault(item => item.EmployeeIndex == filterGuardIndex)
                ?? guardChoices[0];
            FilterGuardBox.SelectedItem = selectedFilter;
            filterGuardIndex = selectedFilter.EmployeeIndex;
            Staff? guardFilter = filterGuardIndex is int index ? data.Employees[index] : null;

            var rows = objectShifts
                .Where(shift => (!filterFrom.HasValue || shift.EndTime > filterFrom.Value)
                    && (!filterTo.HasValue || shift.StartTime.Date <= filterTo.Value)
                    && (guardFilter == null || shift.ActualGuard == guardFilter))
                .OrderBy(shift => shift.StartTime)
                .Select(shift => new ShiftRow(
                    shift,
                    shift.StartTime.ToString("dd.MM.yyyy HH:mm"),
                    shift.EndTime.ToString("dd.MM.yyyy HH:mm"),
                    GetGuardName(shift.PlannedGuard),
                    GetGuardName(shift.ActualGuard)))
                .ToList();

            FilterStatus.Text = $"Показано дежурств: {rows.Count} из {objectShifts.Count}.";
            ShiftsGrid.ItemsSource = rows;
            ShiftsGrid.SelectedItem = rows.FirstOrDefault(row => row.Shift == selected);
            RefreshHistory();
        }

        private void ApplyFiltersClick(object sender, RoutedEventArgs e)
        {
            DateTime? from = FilterFromPicker.SelectedDate;
            DateTime? to = FilterToPicker.SelectedDate;
            if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            {
                FilterStatus.Text = "Дата начала периода должна быть не позже даты окончания.";
                return;
            }

            filterFrom = from?.Date;
            filterTo = to?.Date;
            filterGuardIndex = (FilterGuardBox.SelectedItem as GuardFilterChoice)?.EmployeeIndex;
            RefreshShifts((ShiftsGrid.SelectedItem as ShiftRow)?.Shift);
        }

        private void ResetFiltersClick(object sender, RoutedEventArgs e)
        {
            filterFrom = null;
            filterTo = null;
            filterGuardIndex = null;
            FilterFromPicker.SelectedDate = null;
            FilterToPicker.SelectedDate = null;
            RefreshShifts((ShiftsGrid.SelectedItem as ShiftRow)?.Shift);
        }

        private void ShiftSelected(object sender, SelectionChangedEventArgs e)
        {
            RefreshHistory();
        }

        private void RefreshHistory()
        {
            if (ShiftsGrid.SelectedItem is not ShiftRow row)
            {
                ReplacementGuardBox.ItemsSource = null;
                ReplaceGuardButton.IsEnabled = false;
                ReplacementHint.Text = "Выберите смену в таблице.";
                HistoryText.Text = "Выберите смену в таблице.";
                return;
            }

            var replacements = data.Employees
                .Where(employee => data.Schedule.CanReplaceGuard(row.Shift, employee))
                .Select(employee => new GuardChoice(employee, GetGuardName(employee)))
                .OrderBy(item => item.Name)
                .ToList();

            ReplacementGuardBox.ItemsSource = replacements;
            ReplaceGuardButton.IsEnabled = replacements.Count > 0;
            ReplacementHint.Text = replacements.Count == 0
                ? "Подходящей замены нет."
                : "Показаны охранники, у которых есть 48 часов отдыха.";

            HistoryText.Text = row.Shift.Replacements.Count == 0
                ? "Замен не было."
                : string.Join("\n\n", row.Shift.Replacements.Select(replacement =>
                    $"{GetGuardName(replacement.PreviousGuard)} → " +
                    $"{GetGuardName(replacement.NewGuard)}\n" +
                    $"Причина: {replacement.Reason}"));
        }

        private void AddShiftClick(object sender, RoutedEventArgs e)
        {
            SaveShiftChange((working, agreement) =>
            {
                var shift = new DutyShift(agreement, ReadGuard(GuardBox, working), ReadShiftStart());
                working.Schedule.AddShift(shift);
                return shift;
            });
        }

        private void FillScheduleClick(object sender, RoutedEventArgs e)
        {
            int added = 0;
            FillStatus.Text = string.Empty;
            if (SaveShiftChange((working, agreement) =>
            {
                DateTime through = FillThroughDatePicker.SelectedDate
                    ?? throw new ArgumentException("Выберите дату, до которой нужно заполнить график.");
                DateTime firstStart = working.Schedule.Shifts.Any(shift => shift.Agreement == agreement)
                    ? agreement.DateAgreement.Date
                    : ReadShiftStart();

                var shifts = working.Schedule.FillThrough(agreement, firstStart, through);
                added = shifts.Count;
                return shifts[^1];
            }))
            {
                FillStatus.Text = $"Добавлено дежурств: {added}.";
            }
        }

        private void RescheduleShiftClick(object sender, RoutedEventArgs e)
        {
            SaveShiftChange((working, agreement) =>
            {
                DutyShift shift = ReadSelectedShift(working, agreement);
                working.Schedule.RescheduleShift(shift, ReadShiftStart());
                return shift;
            });
        }

        private void ReplaceGuardClick(object sender, RoutedEventArgs e)
        {
            SaveShiftChange((working, agreement) =>
            {
                DutyShift shift = ReadSelectedShift(working, agreement);
                Staff replacement = ReadGuard(ReplacementGuardBox, working);
                if (!working.Schedule.CanReplaceGuard(shift, replacement))
                    throw new InvalidOperationException("Этот охранник больше не подходит для замены.");

                working.Schedule.ReplaceGuard(shift, replacement, ReasonBox.Text);
                return shift;
            });
        }

        private DateTime ReadShiftStart()
        {
            DateTime date = ShiftDatePicker.SelectedDate
                ?? throw new ArgumentException("Выберите дату начала смены.");

            if (!TimeSpan.TryParseExact(TimeBox.Text.Trim(), @"hh\:mm",
                CultureInfo.InvariantCulture, out TimeSpan time))
            {
                throw new ArgumentException("Укажите время в формате чч:мм, например 08:00.");
            }

            return date.Date.Add(time);
        }

        private Staff ReadGuard(ComboBox box, AgencyData working)
        {
            if (box.SelectedItem is not GuardChoice selectedGuard)
                throw new ArgumentException("Выберите охранника из списка.");

            int index = data.Employees.IndexOf(selectedGuard.Employee);
            return working.Employees[index];
        }

        private DutyShift ReadSelectedShift(AgencyData working, ObjectAgreement agreement)
        {
            if (ShiftsGrid.SelectedItem is not ShiftRow row)
                throw new ArgumentException("Выберите смену в таблице.");

            int index = data.Schedule.Shifts.ToList().IndexOf(row.Shift);
            DutyShift shift = working.Schedule.Shifts[index];

            if (shift.Agreement != agreement || shift.StartTime != row.Shift.StartTime)
                throw new InvalidOperationException("Смена изменилась. Откройте график заново.");

            return shift;
        }

        private bool SaveShiftChange(Func<AgencyData, ObjectAgreement, DutyShift> change)
        {
            try
            {
                if (ObjectBox.SelectedItem is not ObjectChoice choice)
                    throw new ArgumentException("Выберите договор на охрану объекта.");
                
                int agreementIndex = data.Agreements.IndexOf(choice.Agreement);
                DateTime? selectedDate = ShiftDatePicker.SelectedDate;

                 // Обновляем отдельную копию и показываем её после записи JSON.
                AgencyData working = storage.Load(dataPath);
                var agreement = (ObjectAgreement)working.Agreements[agreementIndex];

                DutyShift shift = change(working, agreement);
                storage.Save(dataPath, working);

                data = working;
                RefreshObjects(agreement);
                ShiftDatePicker.SelectedDate = selectedDate;
                RefreshShifts(shift);
                ReasonBox.Clear();
                return true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Не удалось сохранить дежурство", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }
    }
}