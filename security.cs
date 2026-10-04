using System;

namespace Security_agent
{
    // Оружие
    public class Weapon : SecurityItem
    {
        public string Brand {get; set;} = "";
        public string RegistrationNumber {get; set;} = "";

    }
    // Спецсредство
    public class SpecialEquipment : SecurityItem
    {
        public string Name {get; set;} = "";
        public string InventoryNumber {set; get;} = "";
    }
    // Общие данные оружия и спецсредств
    public abstract class SecurityItem 
    {
        // Сотрудник, за которым закреплён предмет, null - предмет свободен
        public Staff? AssignedTo {get; private set;}
        
        // Закрепление свободного предмета за сотрудником
        internal void AssignTo (Staff employee)
        {
            if (AssignedTo != null)
            {
                throw new InvalidOperationException("Предмет уже выдан сотруднику.");
            }
            AssignedTo = employee;
        }

        // Возврат предмета сотрудником, за которым он закреплён
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