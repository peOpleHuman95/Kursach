using System;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace Security_agent
{
    public sealed partial class EquipmentWindow : Window
    {
        private AgencyData data;
        private readonly Storage storage;
        private readonly string dataPath;

        private sealed record EquipmentRow(bool isWeapon, string Name, string Number, string AssignedTo);

        private sealed record EmployeeChoice(int Index, string Name);

        public EquipmentWindow(AgencyData data, Storage storage, string dataPaht)
        {
            this.data = data;
            this.storage = storage;
            this.dataPath = dataPaht;

            InitializeComponent();
            Refresh();
        }

        private void Refresh()
        {
            var weapons = data.Weapons.Select(weapon => new EquipmentRow(true, $"Оружие - {weapon.Brand}", weapon.RegistrationNumber, EmployeeName(weapon.AssignedTo)));
            var specialEquipment = data.SpecialEquipmentItems.Select(item => new EquipmentRow(false, $"Спецсредство - {item.Name}", item.InventoryNumber, EmployeeName(item.AssignedTo)));

            itemsGrid.ItemsSource = weapons.Concat(specialEquipment).OrderBy(item => item.Name).ThenBy(item => item.Number).ToList();

            employeeBox.ItemsSource = data.Employees.Select((employee, index) => new {Employee = employee, Index = index}).Where(item => !item.Employee.IsDismissed).Select(
                item => new EmployeeChoice(item.Index, EmployeeName(item.Employee))).OrderBy(item => item.Name).ToList();
        }

        private void AddItem(object sender, RoutedEventArgs e)
        {
            bool saved = SaveChange(working =>
            {
                string name = nameBox.Text.Trim();
                string number = numberBox.Text.Trim();

                if (name.Length == 0 || number.Length == 0)
                    throw new ArgumentException("Заполните название и номер предмета.");
                
                if (typeBox.SelectedIndex == 0)
                {
                    if (working.Weapons.Any(weapon => SameNumber(weapon.RegistrationNumber, number)))
                        throw new InvalidOperationException("Этот регистрационный номер уже используется.");
                    
                    working.Weapons.Add(new Weapon
                    {
                        Brand = name,
                        RegistrationNumber = number
                    });
                }
                else
                {
                    if (working.SpecialEquipmentItems.Any(item => SameNumber(item.InventoryNumber, number)))
                        throw new InvalidOperationException("Этот инвентарный номер уже используется.");
                    
                    working.SpecialEquipmentItems.Add(new SpecialEquipment
                    {
                        Name = name,
                        InventoryNumber = number
                    });
                }
            });

            if (saved)
            {
                nameBox.Clear();
                numberBox.Clear();
            }
        }

        private void IssueItem(object sender, RoutedEventArgs e)
        {
            SaveChange(working =>
            {
                SecurityItem item = ReadSelectedItem(working);

                if (item.AssignedTo != null)
                    throw new InvalidOperationException("Предмет уже выдан сотруднику.");
                
                if (employeeBox.SelectedItem is not EmployeeChoice choice)
                    throw new InvalidOperationException("Выберите сотрудника для выдачи.");
                
                Staff employee = working.Employees[choice.Index];

                if (item is Weapon weapon)
                    employee.AssignWeapon(weapon);
                else if (item is SpecialEquipment equipment)
                    employee.AssignEquipment(equipment);
            });
        }

        private void ReturnItem(object sender, RoutedEventArgs e)
        {
            SaveChange(working =>
            {
                SecurityItem item = ReadSelectedItem(working);
                Staff employee = item.AssignedTo ?? throw new InvalidOperationException("Этот предмет уже свободен.");

                if (item is Weapon)
                    employee.ReturnWeapon();
                else if (item is SpecialEquipment equipment)
                    employee.ReturnEquipment(equipment);
            });
        }

        private SecurityItem ReadSelectedItem(AgencyData working)
        {
            if (itemsGrid.SelectedItem is not EquipmentRow row)
                throw new InvalidOperationException("Выберите предмет в таблице.");
            SecurityItem? item = row.isWeapon ? working.Weapons.FirstOrDefault(weapon => SameNumber(
                weapon.RegistrationNumber, row.Number)) 
                : working.SpecialEquipmentItems.FirstOrDefault(equipment => SameNumber(equipment.InventoryNumber, row.Number));
            
            return item ?? throw new InvalidOperationException("Предмет не найден в сохраненных данных.");
        }

        private bool SaveChange(Action<AgencyData> change)
        {
            try
            {
                AgencyData working = storage.Load(dataPath);
                change(working);
                storage.Save(dataPath, working);

                data = working;
                Refresh();
                return true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "Снаряжение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private static string EmployeeName(Staff? employee)
        {
            return employee == null ? "Свободен" : $"{employee.LastName} {employee.FirstName} {employee.MiddleName}".Trim();
        }

        private static bool SameNumber(string first, string second)
        {
            return string.Equals(first.Trim(), second.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}