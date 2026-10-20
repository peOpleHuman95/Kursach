using System.Collections.Generic;

namespace Security_agent
{
    // Данные охранного агентства
    public class AgencyData
    {
        public List<Position> Positions {get;} = new List<Position>();
        public List<Staff> Employees {get;} = new List<Staff>();
        public List<Client> Clients {get;} = new List<Client>();
        public List<Agreement> Agreements {get;} = new List<Agreement>();
        public DutySchedule Schedule {get;} = new DutySchedule();
    }
}
