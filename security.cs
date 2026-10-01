using System;

namespace Security_agent
{
    // Оружие
    public class Weapon : SecurityItem
    {
        public string Brand {get; set;} = "";
        public string RegistrationNumber {get; set;} = "";

    }
    // Спесредство
    public class SpecialEquipment : SecurityItem
    {
        public string Name {get; set;} = "";
        public string InventoryNumber {set; get;} = "";
    }
    public abstract class SecurityItem 
    {
        public Staff? AssignedTo {get; private set;}
        internal void AssignTo (Staff employee)
        {
            if (AssignedTo != null)
            {
                throw new InvalidOperationException("Предмет уже выдан сотруднику.");
            }
            AssignedTo = employee;
        }
        internal void ReturnFrom(Staff employee)
        {
            if (AssignedTo != employee)
            {
                throw new InvalidOperationException("Предмет уже закреплен за другим сотрудником.");
            }
            AssignedTo = null;
        }
    }
}