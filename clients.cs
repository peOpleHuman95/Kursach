using System;
using System.Collections.Generic;
using System.Linq;

namespace Security_agent
{
    // Общие данные физических и юридических лиц
    public abstract class Client
    {
        public string Address {get; private set;} = "";
        protected Client (string address)
        {
            ChangeAddress(address);
        }
        // Изменение адреса с проверкой заполнения
        public void ChangeAddress(string newAddress)
        {
            if (string.IsNullOrWhiteSpace(newAddress)) throw new ArgumentException("Адрес не может быть пустым", nameof(newAddress));
            newAddress = newAddress.Trim();
            if (newAddress == Address) return;

            Address = newAddress;

        }
    }
    // Клиент - Юридическое лицо
    public class ClientLegalEntity : Client
    {

        public string NameFirm {get; private set;} = "";
        
        public ClientLegalEntity(string nameFirm,string address) :base(address)
        {
           ChangeNameFirm(nameFirm);
        
        }
       // Изменение названия фирмы с проверкой заполнения
        public void ChangeNameFirm(string newNameFirm)
        {
            if (string.IsNullOrWhiteSpace(newNameFirm)) throw new ArgumentException("Название не может быть пустым", nameof(newNameFirm));
            newNameFirm = newNameFirm.Trim();
            if (newNameFirm == NameFirm) return;

            NameFirm = newNameFirm;
        }
    } 
    // Клиент - Физическое лицо
    public class ClientIndividual : Client
    {
        public string FirstName {get; private set;} = "";
        public string LastName {get; private set;} = "";
        public string MiddleName {get; private set;} = "";
        public string Passport {get; private set;} = "";

        public ClientIndividual(
            string firstName,
            string lastName,
            string middleName,
            string address,
            string passport)
            : base(address)
        {
            UpdatePersonalData(firstName, lastName, middleName, passport);
        }
        // Обновление ФИО и паспортных данных клиента
        public void UpdatePersonalData(
            string firstName,
            string lastName,
            string middleName,
            string passport)
        {
            if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("Имя не должно быть пустым.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Фамилия не должна быть пустой.", nameof(lastName));
            if (string.IsNullOrWhiteSpace(passport)) throw new ArgumentException("Паспортные данные не должны быть пустыми.", nameof(passport));

            FirstName = firstName.Trim();
            LastName = lastName.Trim();
            MiddleName = (middleName ?? "").Trim();
            Passport = passport.Trim();
        }
    }

    // Один платеж по договору
    public class Payment
    {
        public decimal Sum {get;}
        public DateTime DatePayment {get;}
        public string NumberDocument {get;}
        public Payment (decimal sum, DateTime datePayment, string numberDocument)
        {
            if (sum <= 0) throw new ArgumentException("Сумма платежа должна быть положительной.", nameof(sum));
            if (string.IsNullOrWhiteSpace(numberDocument)) throw new ArgumentException("Номер платежного документа не должен быть пустым.", nameof(numberDocument));
            Sum = sum;
            DatePayment = datePayment;
            NumberDocument = numberDocument.Trim();
        }
    }

    // Общие данные договора и учет платежей
    public abstract class Agreement
    {
        public Client Client {get;}
        public string NumberAgreement {get;}
        public DateTime DateAgreement {get; }
        public DateTime DateEndContract {get; }
        
        private readonly List<Payment> payments = new List<Payment>();
        public IReadOnlyList<Payment> Payments => payments.AsReadOnly();

        //Сумма всех внесенных платежей по договору
        public decimal PaidAmount => payments.Sum(payment => payment.Sum);

        protected Agreement(
            Client client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(numberAgreement)) throw new ArgumentException("Номер договора не может быть пустым", nameof(numberAgreement));
            if (dateEndContract < dateAgreement) throw new ArgumentException("Дата окончания не может быть раньше даты заключения.", nameof(dateEndContract));

            Client = client;
            NumberAgreement = numberAgreement.Trim();
            DateAgreement = dateAgreement;
            DateEndContract = dateEndContract;

        }
        // Добавление платежа с проверкой повтора по номеру и дате документа
        public void AddPayment(Payment payment)
        {
            if (payment == null)
                throw new ArgumentNullException(nameof(payment));

            if (payments.Any(existing =>
                existing.NumberDocument == payment.NumberDocument &&
                existing.DatePayment.Date == payment.DatePayment.Date))
            {
                throw new InvalidOperationException(
                    "Платёж с таким номером и датой уже добавлен.");
            }

            payments.Add(payment);
        }

        private readonly List<Staff> employees = new List<Staff>();
        public IReadOnlyList<Staff> Employees => employees.AsReadOnly();

        // Необходимое число охранников по норме выбранного типа услуг
        public abstract int RequiredStaffCount {get;}

        // Полная стоимость охранны по договору
        public abstract decimal CalculateCost();

        // Назначение охранника на договор
        public void AssignEmployee(Staff employee)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));
            if (!employee.Position.IsGuard || employee.IsDismissed)
                throw new InvalidOperationException("Назначить можно только работающего охранника.");
            if (employees.Contains(employee))
                throw new InvalidOperationException("Этот сотрудник уже назначен.");
            if (employees.Count >= RequiredStaffCount)
                throw new InvalidOperationException("Назначенное количество охранников уже назначено.");
            
            employees.Add(employee);
        }

        // Проверка укомплектованности охраны
        protected void CheckStaffing()
        {
            if (employees.Count != RequiredStaffCount)
                throw new InvalidOperationException("Сначала назначьте необходимое число охранников.");
        }

        // Восстановление исторического назначения из файла.
        // После назначения сотрудник мог уволиться или сменить должность.
        internal void RestoreEmployee(Staff employee)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));
            
            if (employees.Contains(employee) || employees.Count >= RequiredStaffCount)
                throw new InvalidOperationException("Некоррекьный состав охраны в файле.");
            
            employees.Add(employee);
        }
    }

    // Договор охраны для разового мероприятия для любого вида клиента
    public class EventAgreement : Agreement
    {
        public string EventAddress {get;}
        public DateTime DateEvent {get;}
        public DateTime EndEvent {get;}
        public string EventNature {get;}
        public int ParticipantsCount {get;}

        // Норма участников на одного охранника
        public int ParticipantsPerGuard {get;}
        // Стоимость часа работы одного охранника для клиента
        public decimal HourlyRate {get;}
        // Деление с округление вверх
        public override int RequiredStaffCount => 1 + (ParticipantsCount - 1) / ParticipantsPerGuard;
        public EventAgreement(
            Client client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract,
            string eventAddress,
            DateTime dateEvent,
            DateTime endEvent,
            string eventNature,
            int participantsCount,
            int participantsPerGuard,
            decimal hourlyRate)
            :base(client, numberAgreement, dateAgreement, dateEndContract)
        {
            if (string.IsNullOrWhiteSpace(eventAddress)) throw new ArgumentException("Укажите адрес мероприятия.", nameof(eventAddress));
            if (string.IsNullOrWhiteSpace(eventNature)) throw new ArgumentException("Укажите характер мероприятия.",nameof(eventNature));
            if (endEvent <=dateEvent) throw new ArgumentException("Окончание должно быть позже начала.",nameof(endEvent));

            // Окончание ровно в полночь относится к предыдущему дню
            if (dateEvent.Date < dateAgreement.Date || endEvent.AddTicks(-1).Date > dateEndContract.Date)
                throw new ArgumentException("Мероприятие должно входить в период договора.");
            if (participantsCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(participantsCount));
            if (participantsPerGuard <= 0)
                throw new ArgumentOutOfRangeException(nameof(participantsPerGuard));
            if (hourlyRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(hourlyRate));
            
            EventAddress = eventAddress.Trim();
            DateEvent = dateEvent;
            EndEvent = endEvent;
            EventNature = eventNature.Trim();
            ParticipantsCount = participantsCount;
            ParticipantsPerGuard = participantsPerGuard;
            HourlyRate = hourlyRate;
        }
        // Часы * число назначенных охранников * часовой тариф
        public override decimal CalculateCost()
        {
            CheckStaffing();

            decimal hours = (decimal)(EndEvent - DateEvent).Ticks / TimeSpan.TicksPerHour;
            return Math.Round(hours * Employees.Count * HourlyRate, 2, MidpointRounding.AwayFromZero);
        }
    }

    // Договор охраны объекта юридического лица
    public class ObjectAgreement : Agreement
    {
        public string ObjectAddress {get;}
        public string ObjectType {get;}

        // Площадь объекта в квадратных метрах
        public decimal Area {get;}

        // Нормы площади на одного выделенного охранника
        public decimal AreaPerGuard {get;}

        // Стоимость месяца охраны одним выделенным охраником
        public decimal MonthlyRate {get;}

        public override int RequiredStaffCount => (int)Math.Ceiling(Area / AreaPerGuard);


        public ObjectAgreement(
            ClientLegalEntity client,
            string numberAgreement,
            DateTime dateAgreement,
            DateTime dateEndContract,
            string objectAddress,
            string objectType,
            decimal area,
            decimal areaPerGuard,
            decimal monthlyRate)
            :base(client, numberAgreement, dateAgreement, dateEndContract)
        {
            if (string.IsNullOrWhiteSpace(objectAddress)) throw new ArgumentException("Укажите адрес объекта.", nameof(objectAddress));
            if (string.IsNullOrWhiteSpace(objectType)) throw new ArgumentException("Укажите тип объекта.", nameof(objectType));

            if (area <= 0)
                throw new ArgumentOutOfRangeException(nameof(area));
            if (areaPerGuard <= 0)
                throw new ArgumentOutOfRangeException(nameof(areaPerGuard));
            if (monthlyRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(monthlyRate));
            
            if (Math.Ceiling(area / areaPerGuard) > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(area), "Слишком большое число охранников.");

            ObjectAddress = objectAddress.Trim();
            ObjectType = objectType.Trim();
            Area = area;
            AreaPerGuard = areaPerGuard;
            MonthlyRate = monthlyRate;
        }

        // Полная стоимость за период договора, включая дату окончания
        public override decimal CalculateCost()
        {
            CheckStaffing();

            decimal total = 0;
            DateTime current = DateAgreement.Date;

            while (true)
            {
                int daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                DateTime monthEnd = new DateTime(current.Year, current.Month, daysInMonth);
                DateTime end = monthEnd < DateEndContract.Date ? monthEnd : DateEndContract.Date;

                int days = (end - current).Days + 1;

                total += MonthlyRate * Employees.Count * days / daysInMonth;

                if (end == DateEndContract.Date) break;
                
                current = end.AddDays(1);

            }
            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}