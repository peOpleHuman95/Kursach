using System;
using System.Collections.Generic;
using System.Linq;

namespace Security_agent
{
    // Один платеж по договору
    public class Payment
    {
        public decimal Sum {get;}
        public DateTime DatePayment {get;}
        public string NumberDocument {get;}
        // Проверяет сумму и номер документа и сохраняет данные платежа.
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
        public Client Client { get; private set; }
        public string NumberAgreement { get; private set; }
        public DateTime DateAgreement { get; private set; }
        public DateTime DateEndContract { get; private set; }
        
        private readonly List<Payment> payments = new List<Payment>();
        public IReadOnlyList<Payment> Payments => payments.AsReadOnly();

        //Сумма всех внесенных платежей по договору
        public decimal PaidAmount => payments.Sum(payment => payment.Sum);

        // Проверяет клиента, номер и даты при создании договора.
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
            ArgumentNullException.ThrowIfNull(payment);

            if (payment.DatePayment.Date > DateTime.Today)
                throw new ArgumentException("Дата платежа не может быть в будущем.");

            if (decimal.Round(payment.Sum, 2) != payment.Sum)
                throw new ArgumentException("Укажите сумму с точностью до копеек.");

            CheckStaffing();
            decimal total = CalculateCost();
            decimal remaining = total - PaidAmount;

            if (remaining <= 0)
                throw new InvalidOperationException("Договор уже оплачен.");

            if (this is EventAgreement && (Payments.Count > 0 || payment.Sum != total))
                throw new InvalidOperationException("Мероприятие нужно оплатить одним платежом на полную сумму.");

            if (payment.Sum > remaining)
                throw new ArgumentException("Сумма платежа превышает остаток.");

            RestorePayment(payment);
        }

        // Восстановление ранее записанного платежа с проверкой дублей.
        internal void RestorePayment(Payment payment)
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

        public virtual bool CanCalculateCost => Employees.Count == RequiredStaffCount;

        public bool CanEdit => payments.Count == 0;

        // Проверяет допустимость правок и обновляет условия договора.
        internal void ApplyEdit(Agreement edited, DutySchedule schedule)
        {
            if (!CanEdit)
                throw new InvalidOperationException("Договор с платежами нельзя редактировать.");

            if (edited.GetType() != GetType())
                throw new InvalidOperationException("Вид существующего договора изменить нельзя.");

            if (employees.Count > edited.RequiredStaffCount)
                throw new InvalidOperationException("Перед изменением нормы снимите лишних охранников с договора.");

            if (schedule.Shifts.Any(shift => shift.Agreement == this &&
                (shift.StartTime.Date < edited.DateAgreement.Date ||
                 shift.EndTime.AddTicks(-1).Date > edited.DateEndContract.Date)))
            {
                throw new InvalidOperationException("Новые даты должны включать все дежурства договора.");
            }

            CopyServiceTermsFrom(edited);
            Client = edited.Client;
            NumberAgreement = edited.NumberAgreement;
            DateAgreement = edited.DateAgreement;
            DateEndContract = edited.DateEndContract;
        }

        // Копирует параметры услуги из изменённого договора того же вида.
        protected abstract void CopyServiceTermsFrom(Agreement edited);


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

        // Снятие сотрудника с договора
        public void RemoveEmployee(Staff employee)
        {
            if (employee == null)
                throw new ArgumentNullException(nameof(employee));
            
            if (!employees.Remove(employee))
                throw new InvalidOperationException("Сотрудник не назначен на этот договор.");
        
        }
        // Меняет назначенного охранника с проверкой состава договора.
        public void ReplaceEmployee(Staff previousEmployee, Staff newEmployee)
        {
            if (previousEmployee == null)
                throw new ArgumentNullException(nameof(previousEmployee));
            
            if (newEmployee == null) 
                throw new ArgumentNullException(nameof(newEmployee));

            int index = employees.IndexOf(previousEmployee);
            
            if (index < 0)
                throw new InvalidOperationException("Заменяемый сотрудник не назначен на этот договор.");
            if (previousEmployee == newEmployee)
                throw new InvalidOperationException("Для замены выберите друго сотрудника.");
            if (!newEmployee.Position.IsGuard || newEmployee.IsDismissed)
                throw new InvalidOperationException("Назначить можно только работающего охранника.");
            if (employees.Contains(newEmployee))
                throw new InvalidOperationException("Новый сотрудник уже назначен на этот договор.");
            
            employees[index] = newEmployee;
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
        public string EventAddress { get; private set; }
        public DateTime DateEvent { get; private set; }
        public DateTime EndEvent { get; private set; }
        public string EventNature { get; private set; }
        public int ParticipantsCount { get; private set; }

        // Норма участников на одного охранника
        public int ParticipantsPerGuard { get; private set; }
        // Стоимость часа работы одного охранника для клиента
        public decimal HourlyRate { get; private set; }
        // Деление с округление вверх
        public override int RequiredStaffCount => 1 + (ParticipantsCount - 1) / ParticipantsPerGuard;
        // Проверяет параметры мероприятия и сохраняет условия почасовой охраны.
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
        // Копирует параметры услуги из изменённого договора того же вида.
        protected override void CopyServiceTermsFrom(Agreement edited)
        {
            var terms = (EventAgreement)edited;
            EventAddress = terms.EventAddress;
            DateEvent = terms.DateEvent;
            EndEvent = terms.EndEvent;
            EventNature = terms.EventNature;
            ParticipantsCount = terms.ParticipantsCount;
            ParticipantsPerGuard = terms.ParticipantsPerGuard;
            HourlyRate = terms.HourlyRate;
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
    public sealed record BillingPeriod(
        DateTime StartDate,
        DateTime EndDate,
        DateTime DueDate,
        decimal Amount);

    // Описывает помесячную охрану объекта юридического лица.
    public class ObjectAgreement : Agreement
    {
        public string ObjectAddress { get; private set; }
        public string ObjectType { get; private set; }

        // Площадь объекта в квадратных метрах
        public decimal Area { get; private set; }

        // Нормы площади на одного выделенного охранника
        public decimal AreaPerGuard { get; private set; }

        // Месячный тариф за одного выделенного охранника
        public decimal MonthlyRate { get; private set; }

        // Число охранников, которые одновременно работают на объекте.
        public int GuardsPerShift => (int)Math.Ceiling(Area / AreaPerGuard);

        // При режиме сутки через двое чередуются три команды.
        private int RotationTeams =>
            Math.Min(3, (DateEndContract.Date - DateAgreement.Date).Days + 1);

        public override int RequiredStaffCount => GuardsPerShift * RotationTeams;

        public override bool CanCalculateCost => true;


        // Проверяет параметры объекта и сохраняет условия помесячной охраны.
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
            
            if (Math.Ceiling(area / areaPerGuard) > int.MaxValue / 3)
                throw new ArgumentOutOfRangeException(nameof(area), "Слишком большое число охранников.");

            ObjectAddress = objectAddress.Trim();
            ObjectType = objectType.Trim();
            Area = area;
            AreaPerGuard = areaPerGuard;
            MonthlyRate = monthlyRate;
        }

        // Копирует параметры услуги из изменённого договора того же вида.
        protected override void CopyServiceTermsFrom(Agreement edited)
        {
            var terms = (ObjectAgreement)edited;
            ObjectAddress = terms.ObjectAddress;
            ObjectType = terms.ObjectType;
            Area = terms.Area;
            AreaPerGuard = terms.AreaPerGuard;
            MonthlyRate = terms.MonthlyRate;
        }

        // Вычисляет стоимость охраны по правилам этого вида договора.
        public override decimal CalculateCost()
        {
            return GetBillingPeriods().Sum(period => period.Amount);
        }

        // Рассчитывает начисление и срок оплаты для каждого месяца договора.
        public IEnumerable<BillingPeriod> GetBillingPeriods()
        {

            DateTime current = DateAgreement.Date;
            DateTime contractEnd = DateEndContract.Date;
            decimal total = 0;
            decimal roundedTotal = 0;

            while (true)
            {
                int daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
                DateTime monthEnd = new DateTime(current.Year, current.Month, daysInMonth);
                DateTime periodEnd = monthEnd < contractEnd ? monthEnd : contractEnd;
                int days = (periodEnd - current).Days + 1;

                DateTime dueDate = days == daysInMonth
                    ? new DateTime(current.Year, current.Month, 15)
                    : current.AddDays((days - 1) / 2);

                total += MonthlyRate * RequiredStaffCount * days / daysInMonth;
                decimal newRoundedTotal = Math.Round(total, 2, MidpointRounding.AwayFromZero);

                yield return new BillingPeriod(
                    current, periodEnd, dueDate, newRoundedTotal - roundedTotal);

                roundedTotal = newRoundedTotal;

                if (periodEnd == contractEnd)
                    break;

                current = periodEnd.AddDays(1);
            }
        }
    }
}
