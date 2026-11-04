using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Security_agent
{
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
                SaveClientsAndAgreements(file, data);
                SaveInventory(file, data);
                file.Employees.Add(saved);
            }
            SaveClientsAndAgreements(file, data);
            SaveSchedule(file, data);

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

            if ((file.Version < 1 || file.Version > 4) || file.Positions == null || file.Employees == null)
                throw new InvalidDataException("Неизвестный формат файла.");
            
            var data = new AgencyData();
            var positions = new Dictionary<int, Position>();
            // Сначала создаём должности
            foreach (PositionData saved in file.Positions)
            {
                if (saved == null || saved.Id <=0 || positions.ContainsKey(saved.Id))
                    throw new InvalidDataException("Некорректный идентификатор должности");
                    
                if (string.IsNullOrWhiteSpace(saved.Name))

                    throw new InvalidDataException($"У должности {saved.Id} не указано название.");

                if (saved.BaseSalary < 0)
                    throw new InvalidDataException($"У должности {saved.Id} указан отрицательный оклад.");     

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

                if (file.Version < 4)
                    LoadLegacyEquipment(saved, employee, data);
                
                data.Employees.Add(employee);
            }
            if (file.Version >=4) 
                LoadInventory(file, data);
            
            // Увольнение восстанавливаем после сохраненной выдачи снаряжения
            // Это позволяет прочитать исторические записи старых файлов
            for (int i = 0; i < file.Employees.Count; i++)
            {
                if (file.Employees[i].DismissalDate is DateTime date)
                    data.Employees[i].Dismiss(date);
            }
            LoadClientsAndAgreements(file, data);
            LoadSchedule(file, data);
            return data;
        }

        // Сохранение свободного и выданного снаряжения
        private void SaveInventory (FileData file, AgencyData data)
        {
            // Выданные предметы должны находитсья в общих списках
            foreach (Staff employee in data.Employees)
            {
                if (employee.AssignedWeapon != null && !data.Weapons.Contains(employee.AssignedWeapon))
                    throw new InvalidOperationException("Выданное оружие отсутствует в обще списке оружия.");
                if (employee.AssignedEquipment.Any(item => !data.SpecialEquipmentItems.Contains(item)))
                    throw new InvalidOperationException("Выданное спецсредство отсутствует в общем списке спецсредств.");
                
                file.Weapons = new List<WeaponData>();

                foreach (Weapon weapon in data.Weapons.Distinct())
                {
                    if (weapon == null)
                        throw new InvalidOperationException("Пустая запись оружия.");
                    
                    file.Weapons.Add(new WeaponData
                    {
                        Brand = weapon.Brand,
                        RegistrationNumber = weapon.RegistrationNumber,
                        AssignedEmployeeId = GetInventoryOwnerId(data, weapon)
                    });
                }
                
                file.SpecialEquipmentItems = new List<EquipmentsData>();

                foreach (SpecialEquipment item in data.SpecialEquipmentItems.Distinct())
                {
                    if (item == null)
                        throw new InvalidOperationException("Пустая запись спецсредства.");
                    
                    file.SpecialEquipmentItems.Add(new EquipmentsData
                    {
                        Name = item.Name,
                        InventoryNumber = item.InventoryNumber,
                        AssignedEmployeeId = GetInventoryOwnerId(data, item)
                    });
                }
            }
        }

        // Получение номера сотрудника, которому выдан предмет
        private int? GetInventoryOwnerId(AgencyData data, SecurityItem item)
        {
            Staff? owner = item.AssignedTo;

            if (owner == null) return null;

            int index = data.Employees.IndexOf(owner);

            if (index < 0)
                throw new InvalidOperationException("Владелец предмета отсутствует в списке сотрудников.");
            
            if ((item is Weapon weapon && owner.AssignedWeapon != weapon) ||
                (item is SpecialEquipment equipment && !owner.AssignedEquipment.Contains(equipment)))
                throw new InvalidOperationException("Нарушена связь предмета с сотрудником.");
            return index + 1;
        }
        // Сохранение клиентов, договоров, назначений и платежей
        private void SaveClientsAndAgreements(FileData file, AgencyData data)
        {
            var clients = data.Clients.Concat(data.Agreements.Select(agreement => agreement.Client)).Distinct().ToList();

            file.Clients = new List<ClientData>();
            file.Agreements = new List<AgreementData>();

            for (int i = 0; i < clients.Count; i++)
            {
                Client client = clients[i];

                var saved = new ClientData
                {
                    Id = i + 1,
                    Address = client.Address
                };

                if (client is ClientLegalEntity legal)
                {
                    saved.Type = "Legal";
                    saved.NameFirm = legal.NameFirm;
                }
                else if (client is ClientIndividual individual)
                {
                    saved.Type = "Individual";
                    saved.FirstName = individual.FirstName;
                    saved.LastName = individual.LastName;
                    saved.MiddleName = individual.MiddleName;
                    saved.Passport = individual.Passport;
                }
                else throw new InvalidOperationException("Неизвестный тип клиента.");

                file.Clients.Add(saved);
            }

            foreach (Agreement agreement in data.Agreements)
            {
                var saved = new AgreementData
                {
                    ClientId = clients.IndexOf(agreement.Client) + 1,
                    NumberAgreement = agreement.NumberAgreement,
                    DateAgreement = agreement.DateAgreement,
                    DateEndContract = agreement.DateEndContract
                };

                foreach (Staff employee in agreement.Employees)
                {
                    int index = data.Employees.IndexOf(employee);

                    if (index < 0) 
                        throw new InvalidOperationException("Назначенный сотдрудник отсутсвует отсутствует в общем списке сотрудников.");

                        saved.EmployeeIds.Add(index + 1);   
                }
                foreach (Payment payment in agreement.Payments)
                {
                    saved.Payments.Add(new PaymentData
                    {
                        Sum = payment.Sum,
                        DatePayment = payment.DatePayment,
                        NumberDocument = payment.NumberDocument
                    });

                }
                if (agreement is EventAgreement ev)
                {
                    saved.Type = "Event";
                    saved.ServiceAddress = ev.EventAddress;
                    saved.DateEvent = ev.DateEvent;
                    saved.EndEvent = ev.EndEvent;
                    saved.ServiceType = ev.EventNature;
                    saved.ParticipantsCount = ev.ParticipantsCount;
                    saved.ParticipantsPerGuard = ev.ParticipantsPerGuard;
                    saved.Rate = ev.HourlyRate;
                }
                else if (agreement is ObjectAgreement obj)
                {
                    saved.Type = "Object";
                    saved.ServiceAddress = obj.ObjectAddress;
                    saved.ServiceType = obj.ObjectType;
                    saved.Area = obj.Area;
                    saved.AreaPerGuard = obj.AreaPerGuard;
                    saved.Rate = obj.MonthlyRate;
                }
                else throw new InvalidOperationException("Неизвестный тип договора.");

                file.Agreements.Add(saved);
            }
        
        }

        // Загрузка снаряжения из старого формата
        private void LoadLegacyEquipment(EmployeeData saved, Staff employee, AgencyData data)
        {
            if (saved.Weapon != null)
            {
                var weapon = new Weapon
                {
                    Brand = saved.Weapon.Brand,
                    RegistrationNumber = saved.Weapon.RegistrationNumber
                };
                employee.AssignWeapon(weapon);
                data.Weapons.Add(weapon);
            }

            foreach (EquipmentsData savedItem in saved.Equipment)
            {
                if (savedItem == null)
                    throw new InvalidDataException("Пустая запись спецсредства.");
                
                var item = new SpecialEquipment
                {
                    Name = savedItem.Name,
                    InventoryNumber = savedItem.InventoryNumber
                };

                employee.AssignEquipment(item);
                data.SpecialEquipmentItems.Add(item);
            }
        }

        // Загрузка общих списков из нового формата
        private void LoadInventory(FileData file, AgencyData data)
        {
            if (file.Weapons == null || file.SpecialEquipmentItems == null)
                throw new InvalidDataException("В файле отсутствуют общие списки снаряжения.");
            
            foreach (WeaponData saved in file.Weapons)
            {
                if (saved == null)
                    throw new InvalidDataException("Пустая запись оружия.");
                var weapon = new Weapon
                {
                    Brand = saved.Brand,
                    RegistrationNumber = saved.RegistrationNumber
                };

                if (saved.AssignedEmployeeId.HasValue)
                {
                    Staff employee = GetEmployee(data, saved.AssignedEmployeeId.Value);
                    employee.AssignWeapon(weapon);
                }

                data.Weapons.Add(weapon);
            }
            foreach (EquipmentsData saved in file.SpecialEquipmentItems)
                {
                    if (saved == null)
                        throw new InvalidDataException("Пустая запись спецсредства.");
                    
                    var item = new SpecialEquipment
                    {
                        Name = saved.Name,
                        InventoryNumber = saved.InventoryNumber
                    };

                    if (saved.AssignedEmployeeId.HasValue)
                    {
                        Staff employee = GetEmployee(data, saved.AssignedEmployeeId.Value);
                        employee.AssignEquipment(item);
                    }
                    data.SpecialEquipmentItems.Add(item);
                }
        }
        // Восстановление клиентов и связей договоров
        private void LoadClientsAndAgreements(FileData file, AgencyData data)
        {
            if (file.Version == 1 &&
                file.Clients == null &&
                file.Agreements == null)
            {
                return;
            }

            if (file.Clients == null || file.Agreements == null)
            {
                throw new InvalidDataException(
                    "В файле отсутствуют клиенты или договоры.");
            }

            var clients = new Dictionary<int, Client>();

            foreach (ClientData saved in file.Clients)
            {
                if (saved == null ||
                    saved.Id <= 0 ||
                    clients.ContainsKey(saved.Id))
                {
                    throw new InvalidDataException(
                        "Некорректный идентификатор клиента.");
                }

                Client client;

                if (saved.Type == "Legal")
                {
                    client = new ClientLegalEntity(
                        saved.NameFirm,
                        saved.Address);
                }
                else if (saved.Type == "Individual")
                {
                    client = new ClientIndividual(
                        saved.FirstName,
                        saved.LastName,
                        saved.MiddleName,
                        saved.Address,
                        saved.Passport);
                }
                else
                {
                    throw new InvalidDataException(
                        "Неизвестный тип клиента.");
                }

                clients.Add(saved.Id, client);
                data.Clients.Add(client);
            }

            foreach (AgreementData saved in file.Agreements)
            {
                if (saved == null ||
                    saved.EmployeeIds == null ||
                    saved.Payments == null)
                {
                    throw new InvalidDataException(
                        "Неполная запись договора.");
                }

                if (!clients.TryGetValue(saved.ClientId, out Client? client))
                {
                    throw new InvalidDataException(
                        "Клиент договора не найден.");
                }

                Agreement agreement;

                if (saved.Type == "Event")
                {
                    agreement = new EventAgreement(
                        client,
                        saved.NumberAgreement,
                        saved.DateAgreement,
                        saved.DateEndContract,
                        saved.ServiceAddress,
                        saved.DateEvent,
                        saved.EndEvent,
                        saved.ServiceType,
                        saved.ParticipantsCount,
                        saved.ParticipantsPerGuard,
                        saved.Rate);
                }
                else if (saved.Type == "Object")
                {
                    if (client is not ClientLegalEntity legal)
                    {
                        throw new InvalidDataException(
                            "Объект может охраняться только по договору " +
                            "с юридическим лицом.");
                    }

                    agreement = new ObjectAgreement(
                        legal,
                        saved.NumberAgreement,
                        saved.DateAgreement,
                        saved.DateEndContract,
                        saved.ServiceAddress,
                        saved.ServiceType,
                        saved.Area,
                        saved.AreaPerGuard,
                        saved.Rate);
                }
                else
                {
                    throw new InvalidDataException(
                        "Неизвестный тип договора.");
                }

                foreach (int id in saved.EmployeeIds)
                {
                    if (id <= 0 || id > data.Employees.Count)
                    {
                        throw new InvalidDataException(
                            "Сотрудник договора не найден.");
                    }

                    agreement.RestoreEmployee(data.Employees[id - 1]);
                }

                foreach (PaymentData payment in saved.Payments)
                {
                    if (payment == null)
                    {
                        throw new InvalidDataException(
                            "Пустая запись платежа.");
                    }

                    agreement.AddPayment(new Payment(
                        payment.Sum,
                        payment.DatePayment,
                        payment.NumberDocument));
                }

                data.Agreements.Add(agreement);
            }
        }

        // Номер сотрудника в общем списке, начиная с 1
        private int GetEmployeeId(AgencyData data, Staff employee)
        {
            int index = data.Employees.IndexOf(employee);

            if (index < 0)
                throw new InvalidOperationException("Участник дежурства отсутсвует в общем списке сотрудников.");
            
            return index + 1;
        }

        // Получение уже загруженного сотрудника по номеру
        private Staff GetEmployee(AgencyData data, int id)
        {
            if (id <= 0 || id > data.Employees.Count)
                throw new InvalidDataException("Сотрудник дежурства не найден.");
            return data.Employees[id - 1];
        }

        // Сохранение графика и истории замен
        private void SaveSchedule(FileData file, AgencyData data)
        {
            file.Shifts = new List<DutyShiftData>();

            foreach (DutyShift shift in data.Schedule.Shifts)
            {
                int index = data.Agreements.IndexOf(shift.Agreement);

                if (index < 0)
                    throw new InvalidOperationException("Договор дежурства отсутствует в общем списке договоров.");
                
                var saved = new DutyShiftData
                {
                    AgreementId = index + 1,
                    PlannedGuardId = GetEmployeeId(data, shift.PlannedGuard),
                    StartTime = shift.StartTime
                };

                foreach (ShiftReplacement replacement in shift.Replacements)
                {
                    saved.Replacements.Add(new ShiftReplacementData
                    {
                        PreviousGuardId = GetEmployeeId(data, replacement.PreviousGuard),
                        NewGuardId = GetEmployeeId(data, replacement.NewGuard),
                        Reason = replacement.Reason
                    });
                }
                file.Shifts.Add(saved);
            }
        }
        // Восстановление графиков после загрузки сотрудников и договоров
        private void LoadSchedule(FileData file, AgencyData data)
        {
            if (file.Version < 3 && file.Shifts == null) return;

            if (file.Shifts == null)
                throw new InvalidDataException("В файле отсутствует график дежурств.");
            
            foreach (DutyShiftData saved in file.Shifts)
            {
                if (saved == null || saved.Replacements == null)
                    throw new InvalidDataException("Неполная запись дежурств.");
                
                if (saved.AgreementId <= 0 || saved.AgreementId > data.Agreements.Count ||
                data.Agreements[saved.AgreementId - 1] is not ObjectAgreement agreement)
                    throw new InvalidDataException("Договор охраны объекта для дежурства не найден.");
                
                Staff plannedGuard = GetEmployee(data, saved.PlannedGuardId);
                var history = new List<ShiftReplacement>();

                foreach (ShiftReplacementData replacement in saved.Replacements)
                {
                    if (replacement == null)
                        throw new InvalidDataException("Пустая запись замены.");

                    history.Add(new ShiftReplacement(
                        GetEmployee(data, replacement.PreviousGuardId),
                        GetEmployee(data, replacement.NewGuardId),
                        replacement.Reason));
                }
                try
                {
                    DutyShift shift = DutyShift.Restore(
                        agreement,
                        plannedGuard,
                        saved.StartTime,
                        history);
                    
                    // Проверяем отдых охранника с учётом итоговой замены
                    data.Schedule.AddShift(shift);

                }
                catch (ArgumentException error)
                {
                    throw new InvalidDataException("Некорректные данные дежурства.", error);
                }
                catch (InvalidOperationException error)
                {
                    throw new InvalidDataException("Некорректный график или история замен.", error);
                }
            }
        }
    }
}
