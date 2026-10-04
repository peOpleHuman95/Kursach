using System;
using System.Collections.Generic;

namespace Security_agent
{
    public class Position
    {  
        // Название должности
        public string Name {get; private set;}
        // Оклад
        public decimal BaseSalary {get; private set;}
        // Является ли должность охранной
        public bool IsGuard {get; private set;}
        public Position(string name, decimal baseSalary, bool isGuard)
        {
            Name = name;
            BaseSalary = baseSalary;
            IsGuard = isGuard;

        }
    }
    public class Staff
    {
        // Личные данные
        public string FirstName {get; set;} = "";
        public string LastName {get; set;} = "";
        public string MiddleName {get; set;} = "";
        public string Address {get; set;} = "";

        // Должность и зарплата
        public Position Position {get; private set;}
        // Персональная надбавка к окладу
        public decimal PersonalAllowance {get; set;}
        // Зарплата: оклад + надбавка
        public decimal Salary => Position.BaseSalary  + PersonalAllowance;

        // Право на ношение оружия
        public bool CanCarryWeapon {get; private set;}
        public Weapon? AssignedWeapon { get; private set; }

        // Спецсредства
        private readonly List<SpecialEquipment> equipment  = new List<SpecialEquipment>();
         public IReadOnlyList<SpecialEquipment> AssignedEquipment =>
            equipment.AsReadOnly();

        // Документы
        public string CertificateNumber {get; set;} = "";
        public string LicenseNumber {get; set;} = "";
        public string Inn {get; set;} = "";
        public string PfrNumber {get; set;} = "";

        // Дата увольнения
        public DateTime? DismissalDate {get; private set;}
        // Указана дата увольнения
        public bool IsDismissed => DismissalDate.HasValue;
        // Дата окончания обязательного пятилетнего хранения сведений
        public DateTime? KeepUntil => DismissalDate?.AddYears(5);
        
        public EmployeeDocuments Documents {get;}

        // Создание сотрудника с проверкой должности, документов и судимости
        public Staff(Position position, EmployeeDocuments documents)
        {
           if (position == null) throw new ArgumentNullException(nameof(position));
           if (documents == null) throw new ArgumentNullException(nameof(documents));
           if (documents.CriminalRecord)
            {
                throw new InvalidOperationException("Нельзя принять сотрудника с судимостью.");
            }
            Position = position;
            Documents = documents;
        }
        // Смена должности
        public void ChangePosition(Position position)
        {
            if (position == null)
            {
                throw new ArgumentNullException(nameof(position));
            }
            

            if (!position.IsGuard && equipment.Count > 0)
            {
                throw new InvalidOperationException("Перед сменой должности нужно сдать спецсредства.");
            }
            Position = position;
        }
        // Настройка права на ношение оружия
        public void SetWeaponPermission(bool allowed)
        {
            if (!allowed && AssignedWeapon != null)
            {
                throw new InvalidOperationException("Перед отменой разрешения нужно сдать оружие.");
            }
            CanCarryWeapon = allowed;
        }
        // Выдача оружия сотруднику с проверкой разрешения
        public void AssignWeapon(Weapon weapon)
        {
            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }
            if (IsDismissed || !CanCarryWeapon)
            {
                throw new InvalidOperationException("Сотруднику нельзя выдать оружие.");
            }
            if (AssignedWeapon != null)
            {
                throw new InvalidOperationException("За сотрудником уже закреплено оружие.");
            }
            weapon.AssignTo(this);
            AssignedWeapon = weapon;
        }
        // Возврат оружия
        public void ReturnWeapon()
        {
            AssignedWeapon?.ReturnFrom(this);
            AssignedWeapon = null;
        }
        // Выдача спецсредства работающему охраннику
        public void AssignEquipment(SpecialEquipment item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            if (IsDismissed || !Position.IsGuard)
            {
                throw new InvalidOperationException("Спецсредства можно выдать только работающим охранникам.");
            }
            if (!equipment.Contains(item))
            {
                item.AssignTo(this);
                equipment.Add(item);
            }
        }
        // Возврат спецсредства
        public void ReturnEquipment(SpecialEquipment item)
        {
             if (equipment.Contains(item))
             {
             item.ReturnFrom(this);
             equipment.Remove(item);
             }
        }
        // Фиксация даты увольнения
        public void Dismiss (DateTime date)
        {
            if (IsDismissed)
            {
                throw new InvalidOperationException("Сотрудник уже уволен.");
            }
            DismissalDate = date.Date;
        }
    }
}