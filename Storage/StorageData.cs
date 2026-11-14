using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Security_agent
{
    // Простые записи для JSON без взаимных ссылок объектов
    public class FileData
    {
        [JsonRequired]
        public int Version {get; set;} = 4;

        [JsonRequired]
        public List<PositionData> Positions {get; set;} = new();

        [JsonRequired]
        public List<EmployeeData> Employees {get; set;} = new();
        
        public List<ClientData>? Clients { get; set; }
        public List<AgreementData>? Agreements { get; set; }
        
        public List<DutyShiftData>? Shifts {get; set;}

        public List<WeaponData>? Weapons {get; set;}
        public List<EquipmentsData>? SpecialEquipmentItems {get; set;}
    }

    // Хранит параметры должности для сохранения в JSON.
    public class PositionData
    {
        public int Id {get; set;}
        public string Name {get; set;} = "";
        public decimal BaseSalary {get; set;}
        public bool IsGuard {get; set;}
    }
    // Хранит данные сотрудника и ссылку на должность.
    public class EmployeeData
    {
        public int PositionId {get; set;}
        public string FirstName {get; set;} = "";
        public string LastName {get; set;} = "";
        public string MiddleName {get; set;} = "";
        public string Address {get; set;} = "";
        public decimal PersonalAllowance {get;set;}
        public bool CanCarryWeapon {get; set;}
        public string CertificateNumber {get; set;} = "";
        public string LicenseNumber {get; set;} = "";
        public string Inn {get; set;} = "";
        public string PfrNumber {get; set;} = "";
        public DateTime? DismissalDate {get; set;}

        [JsonRequired]
        public DocumentsData Documents {get; set;} = new();

        public WeaponData? Weapon {get; set;}

        public List<EquipmentsData> Equipment {get; set;} = new();
    }
    // Хранит общие и специальные реквизиты клиента.
    public class ClientData
    {
        public int Id {get; set;}
        public string Type {get; set;} = "";
        public string Address {get; set;} = "";
        public string NameFirm {get; set;} = "";
        public string FirstName {get; set;} = "";
        public string LastName {get; set;} = "";
        public string MiddleName {get; set;} = "";
        public string Passport {get; set;} = ""; 
    }
    // Хранит условия договора, назначения и платежи.
    public class AgreementData
    {
         public string Type { get; set; } = "";
        public int ClientId { get; set; }
        public string NumberAgreement { get; set; } = "";
        public DateTime DateAgreement { get; set; }
        public DateTime DateEndContract { get; set; }

        // Адрес, тип услуги и тариф
        public string ServiceAddress { get; set; } = "";
        public string ServiceType { get; set; } = "";
        public decimal Rate { get; set; }

        // Данные мероприятия
        public DateTime DateEvent { get; set; }
        public DateTime EndEvent { get; set; }
        public int ParticipantsCount { get; set; }
        public int ParticipantsPerGuard { get; set; }

        // Данные объекта
        public decimal Area { get; set; }
        public decimal AreaPerGuard { get; set; }

        // Номера сотрудников в массиве Employees файла, начиная с 1
        public List<int> EmployeeIds { get; set; } = new List<int>();

        public List<PaymentData> Payments { get; set; } =
            new List<PaymentData>();
    }
    // Хранит сумму, дату и номер документа одного платежа.
    public class PaymentData
    {
        public decimal Sum { get; set; }
        public DateTime DatePayment { get; set; }
        public string NumberDocument { get; set; } = "";
    }
    // Хранит реквизиты документов сотрудника.
    public class DocumentsData
    {
        public string Passport {get; set;} = "";
        public bool MilitaryIdCard { get; set; }
        public string EmploymentRecordBook { get; set; } = "";
        public string EducationalDocument { get; set; } = "";
        public bool CriminalRecord { get; set; }
        public bool MedicalReport { get; set; }
        public string MedicalBook { get; set; } = "";
    }
    // Хранит марку, номер и сведения о владельце оружия.
    public class WeaponData
    {
        public string Brand {get; set;} = "";
        public string RegistrationNumber {get; set;} = "";

        // null означает, что оружие свободно
        public int? AssignedEmployeeId {get; set;}
    }
    // Хранит название, номер и сведения о владельце спецсредства.
    public class EquipmentsData
    {
    public string Name {get; set;} = "";
    public string InventoryNumber {get; set;} = "";

    // null означает, что спецсредство свободно
    public int? AssignedEmployeeId {get; set;}
    }
    
    // Хранит параметры дежурства и историю его замен.
    public class DutyShiftData
    {
        public int AgreementId {get; set;}
        public int PlannedGuardId {get; set;}
        
        [JsonRequired]
        public DateTime StartTime {get; set;}

        [JsonRequired]
        public List<ShiftReplacementData> Replacements {get; set;} = new();
    }

    // Хранит ссылки на охранников и причину замены.
    public class ShiftReplacementData
    {
        public int PreviousGuardId {get; set;}
        public int NewGuardId {get; set;}
        public string Reason {get; set;} = "";
    }
}
