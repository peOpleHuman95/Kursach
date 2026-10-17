using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Security_agent
{
    // Данные первого этапа: должность и сотрудник
    public class AgencyData
    {
        public List<Position> Positions {get;} = new List<Position>();
        public List<Staff> Employees {get;} = new List<Staff>();
    }
    public class Storage
    {
        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        // Сохранение сотрудников и их должности
        public void Save(string path, AgencyData data)
        {
            ArgumentNullException.ThrowIfNull(data);

            string fullPath = Path.GetFullPath(path);
            
            // Добавление также должности, на которые ссылаются сотрудники
            var positions = data.Positions
                .Concat(data.Employees.Select(employee => employee.Position))
                .Distinct()
                .ToList();
            var file = new FileData();

            for (int i = 0; i < positions.Count; i++)
            {
                Position position = positions[i];

                file.Positions.Add(new PositionData
                {
                    Id = i + 1,
                    Name = position.Name,
                    BaseSalary = position.BaseSalary,
                    IsGuard = position.IsGuard
                });
            }
            foreach (Staff employee in data.Employees)
            {
                var saved = new EmployeeData
                {
                    PositionId = positions.IndexOf(employee.Position) + 1,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    MiddleName = employee.MiddleName,
                    Address = employee.Address,
                    PersonalAllowance = employee.PersonalAllowance,
                    CanCarryWeapon = employee.CanCarryWeapon,
                    CertificateNumber = employee.CertificateNumber,
                    LicenseNumber = employee.LicenseNumber,
                    Inn = employee.Inn,
                    PfrNumber = employee.PfrNumber,
                    DismissalDate = employee.DismissalDate,

                    Documents = new DocumentsData
                    {
                        Passport = employee.Documents.Passport,
                        MilitaryIdCard = employee.Documents.MilitaryIdCard,
                        EmploymentRecordBook = employee.Documents.EmploymentRecordBook,
                        EducationalDocument = employee.Documents.EducationalDocument,
                        CriminalRecord = employee.Documents.CriminalRecord,
                        MedicalReport = employee.Documents.MedicalReport,
                        MedicalBook = employee.Documents.MedicalBook
                    }
                };
                if (employee.AssignedWeapon != null)
                {
                    saved.Weapon = new WeaponData
                    {
                        Brand = employee.AssignedWeapon.Brand,
                        RegistrationNumber = employee.AssignedWeapon.RegistrationNumber
                    };
                }
                foreach (SpecialEquipment item in employee.AssignedEquipment)
                {
                    saved.Equipment.Add(new EquipmentsData
                    {
                        Name = item.Name,
                        InventoryNumber = item.InventoryNumber
                    });
                }
                file.Employees.Add(saved);
            }
            string json = JsonSerializer.Serialize(file, options);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            string temporaryPath = fullPath + "." + Guid.NewGuid()+".tmp";

            try
            {
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                // Заменяем сохранение после успешной записи
                File.Move(temporaryPath, fullPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }
        // Загрузка данных и восстановление ссылок на должности
        public AgencyData Load(string path)
        {
            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (FileNotFoundException)
            {
                return new AgencyData();
            }
            catch (DirectoryNotFoundException)
            {
                return new AgencyData();
            }
            FileData file;

            try
            {
                file = JsonSerializer.Deserialize<FileData>(json, options)
                    ?? throw new InvalidDataException("Файл не содержит данных.");
            }
            catch (JsonException error)
            {
                throw new InvalidDataException("Некорректный JSON. Файл не изменен.",error);
            }

            if (file.Version != 1 || file.Positions == null || file.Employees == null)
                throw new InvalidDataException("Неизвестный формат файла.");
            
            var data = new AgencyData();
            var positions = new Dictionary<int, Position>();
            // Сначала создаём должности
            foreach (PositionData saved in file.Positions)
            {
                if (saved == null || saved.Id <=0 || positions.ContainsKey(saved.Id))
                    throw new InvalidDataException("Некорректный идентификатор должности");
                
                var position = new Position(saved.Name, saved.BaseSalary, saved.IsGuard);

                positions.Add(saved.Id, position);
                data.Positions.Add(position);
            }
            // Затем создаём сотрудников с нужными должностями
            foreach (EmployeeData saved in file.Employees)
            {
                if (saved == null || saved.Documents == null || saved.Equipment == null)
                    throw new InvalidDataException("Неполная запись сотрудника.");
                if (!positions.TryGetValue(saved.PositionId, out Position? position))
                    throw new InvalidDataException("Должность сотрудника не найдена.");

                // Восстанавливаем существующую запись, а не оформляем новый приём на работу
                var documents = new EmployeeDocuments();

                var employee = new Staff(position, documents)
                {
                     FirstName = saved.FirstName,
                    LastName = saved.LastName,
                    MiddleName = saved.MiddleName,
                    Address = saved.Address,
                    PersonalAllowance = saved.PersonalAllowance,
                    CertificateNumber = saved.CertificateNumber,
                    LicenseNumber = saved.LicenseNumber,
                    Inn = saved.Inn,
                    PfrNumber = saved.PfrNumber
                };

                DocumentsData doc = saved.Documents;
                documents.Update(
                     doc.Passport,
                    doc.MilitaryIdCard,
                    doc.EmploymentRecordBook,
                    doc.EducationalDocument,
                    doc.CriminalRecord,
                    doc.MedicalReport,
                    doc.MedicalBook);
                employee.SetWeaponPermission(saved.CanCarryWeapon);

                if (saved.Weapon != null)
                {
                    employee.AssignWeapon(new Weapon
                    {
                        Brand = saved.Weapon.Brand,
                        RegistrationNumber = saved.Weapon.RegistrationNumber
                    });
                }
                foreach (EquipmentsData item in saved.Equipment)
                {
                    if (item == null)
                        throw new InvalidDataException("Пустая запись спецсредства.");
                    
                    employee.AssignEquipment(new SpecialEquipment
                    {
                        Name = item.Name,
                        InventoryNumber = item.InventoryNumber
                    });
                }
                if (saved.DismissalDate.HasValue)
                    employee.Dismiss(saved.DismissalDate.Value);
                
                data.Employees.Add(employee);
            }
            return data;
        }
    
    }
    // Простые записи для JSON без взаимных ссылок объектов
    public class FileData
    {
        [JsonRequired]
        public int Version {get; set;} = 1;

        [JsonRequired]
        public List<PositionData> Positions {get; set;} = new();

        [JsonRequired]
        public List<EmployeeData> Employees {get; set;} = new();
    }
    public class PositionData
    {
        public int Id {get; set;}
        public string Name {get; set;} = "";
        public decimal BaseSalary {get; set;}
        public bool IsGuard {get; set;}
    }
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
    public class WeaponData
    {
        public string Brand {get; set;} = "";
        public string RegistrationNumber {get; set;} = "";
    }
     public class EquipmentsData
    {
        public string Name {get; set;} = "";
        public string InventoryNumber {get; set;} = "";
    }
}