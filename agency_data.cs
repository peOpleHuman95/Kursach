using System.Collections.Generic;

namespace Security_agent
{
    // Все данные охранного агентства
    public class AgencyData
    {
        // Должности
        public List<Position> Positions {get;} = new List<Position>();
        
        // Сотрудники
        public List<Staff> Employees {get;} = new List<Staff>();

        // Клиенты
        public List<Client> Clients {get;} = new List<Client>();

        // Договоры
        public List<Agreement> Agreements {get;} = new List<Agreement>();

        // Все оружие агенства: свобдное и выданное
        public List<Weapon> Weapons {get;} = new List<Weapon>();

        // Все спецсредства агенства: свобдное и выданное
        public List<SpecialEquipment> SpecialEquipmentItems {get;} = new List<SpecialEquipment>();

        // График дежурства
        public DutySchedule Schedule {get;} = new DutySchedule();

    }
}
