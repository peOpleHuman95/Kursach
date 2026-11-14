
namespace Security_agent
{
    // Запись о замене охранника
    public class ShiftReplacement
    {
        public Staff PreviousGuard {get;}
        public Staff NewGuard {get;}
        public string Reason {get;}

        // Записывает прежнего и нового охранника и причину замены.
        internal ShiftReplacement(
            Staff previousGuard,
            Staff newGuard,
            string reason)
        {
            PreviousGuard = previousGuard;
            NewGuard = newGuard;
            Reason = reason;
        }
    }

    // Одно суточное дежурство
     public class DutyShift
    {
        public ObjectAgreement Agreement {get;}
        public DateTime StartTime { get; private set; }
        public DateTime EndTime => StartTime.AddDays(1);

        // Первоначально назначенный охранник
        public Staff PlannedGuard {get;}
        // охранник, назначенный на дежурство с учётом замен
        public Staff ActualGuard {get; private set;}
        private readonly List<ShiftReplacement> replacements = new List<ShiftReplacement>();
        public IReadOnlyList<ShiftReplacement> Replacements => replacements.AsReadOnly();

        // Создание дежурства с проверкой периода договора и охранника
        public DutyShift (
            ObjectAgreement agreement,
            Staff guard,
            DateTime startTime)
            : this(agreement, guard, startTime, false)
        {
        }
        // Создаёт суточное дежурство на объекте с указанным охранником.
        private DutyShift(
            ObjectAgreement agreement,
            Staff guard,
            DateTime startTime,
            bool restoring)
            {
            if (agreement == null) throw new ArgumentNullException(nameof(agreement));
            if (guard == null) throw new ArgumentNullException(nameof(guard));

            CheckPeriod(agreement, startTime);

            StartTime = startTime;

            if (!restoring)
                CheckGuard(guard, startTime);

            Agreement = agreement;
            PlannedGuard = guard;
            ActualGuard = guard;
        }

        // Проверяет новый период дежурства и меняет время начала.
        internal void ApplyReschedule(DateTime startTime)
        {
            CheckPeriod(Agreement, startTime);
            CheckGuard(ActualGuard, startTime);
            StartTime = startTime;
        }

        // Проверяет, что суточная смена полностью входит в срок договора.
        private static void CheckPeriod(ObjectAgreement agreement, DateTime startTime)
        {
            DateTime lastDutyDate = startTime.AddDays(1).AddTicks(-1).Date;
            if (startTime.Date < agreement.DateAgreement.Date ||
                lastDutyDate > agreement.DateEndContract.Date)
            {
                throw new ArgumentException(
                    "Дежурство должно полностью входить в период договора.", nameof(startTime));
            }
        }

        // Запись замены и её причины в истории дежурства
        internal void ApplyReplaceGuard(Staff newGuard, string reason)
        {
            CheckGuard(newGuard, StartTime);

            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Должна быть причина замены.", nameof(reason));
            if (newGuard == ActualGuard) throw new InvalidOperationException("Этот охранник уже назначен на дежурство.");
            
            replacements.Add(new ShiftReplacement(ActualGuard, newGuard, reason.Trim()));
            ActualGuard = newGuard;
        }

        // Проверка должности и увольнения на дату начала дежурства
        private static void CheckGuard(Staff guard, DateTime startTime)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (!guard.Position.IsGuard) throw new InvalidOperationException("Дежурить может только охранник.");
            if (guard.DismissalDate.HasValue && startTime.Date >= guard.DismissalDate.Value.Date) 
                throw new InvalidOperationException("Охранник уже уволен на момент дежурства.");
        }

        // Восстановление дежурства и всей истории замен
        internal static DutyShift Restore(
            ObjectAgreement agreement,
            Staff plannedGuard,
            DateTime startTime,
            IEnumerable<ShiftReplacement> history)
        {
            if (history == null)
                throw new ArgumentNullException(nameof(history));
            
            var shift = new DutyShift(agreement, plannedGuard, startTime, true);

            foreach (ShiftReplacement replacement in history)
            {
                if (replacement == null || replacement.PreviousGuard != shift.ActualGuard ||  replacement.NewGuard == null || replacement.NewGuard == shift.ActualGuard ||
                string.IsNullOrWhiteSpace(replacement.Reason))
                    throw new InvalidOperationException("Некорректная история замен.");
                
                shift.replacements.Add(new ShiftReplacement(
                    shift.ActualGuard,
                    replacement.NewGuard,
                    replacement.Reason.Trim()));
                shift.ActualGuard = replacement.NewGuard;
            }
            return shift;
        }
    }

    // Общий график дежурств по объектам
    public class DutySchedule
    {
        private readonly List<DutyShift> shifts = new List<DutyShift>();
        public IReadOnlyList<DutyShift> Shifts => shifts.AsReadOnly();

        // Добавление дежурства с проверкой отдыха охранника
        public void AddShift(DutyShift shift)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (shifts.Contains(shift)) throw new InvalidOperationException("Это дежурство уже добавлено в график.");
            CheckRest(
                shift.ActualGuard,
                shift.StartTime,
                shift.EndTime,
                null);
            shifts.Add(shift);
        }

        // Переносит смену с проверкой отдыха фактически назначенного охранника.
        public void RescheduleShift(DutyShift shift, DateTime startTime)
        {
            ArgumentNullException.ThrowIfNull(shift);

            if (!shifts.Contains(shift))
                throw new InvalidOperationException("Дежурство отсутствует в графике.");

            if (shift.StartTime == startTime)
                return;

            CheckRest(shift.ActualGuard, startTime, startTime.AddDays(1), shift);
            shift.ApplyReschedule(startTime);
        }

        // Замена охранника с проверкой отдыха охранника до и после дежурства
        public void ReplaceGuard(
            DutyShift shift,
            Staff newGuard,
            string reason)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (newGuard == null) throw new ArgumentNullException(nameof(newGuard));
            if (!shifts.Contains(shift)) throw new InvalidOperationException("Дежурство отсутствует в графике.");

            CheckRest(
                newGuard,
                shift.StartTime,
                shift.EndTime,
                shift);
            shift.ApplyReplaceGuard(newGuard, reason);
        }

        // Продлевает график до указанной даты и проверяет состав каждой новой смены.
        public IReadOnlyList<DutyShift> FillThrough(
            ObjectAgreement agreement, DateTime firstStart, DateTime throughDate)
        {
            ArgumentNullException.ThrowIfNull(agreement);

            if (throughDate.Date > agreement.DateEndContract.Date)
                throw new ArgumentException("Дата заполнения выходит за срок договора.", nameof(throughDate));

            DateTime start = shifts.Where(shift => shift.Agreement == agreement)
                .Select(shift => shift.EndTime)
                .DefaultIfEmpty(firstStart)
                .Max();
            if (start.Date > throughDate.Date)
                throw new ArgumentException($"Следующая смена начинается {start:dd.MM.yyyy HH:mm}. Выберите эту дату или позже.", nameof(throughDate));

            var guards = agreement.Employees
                .Where(guard => guard.Position.IsGuard && !guard.IsDismissed)
                .ToList();
            var pending = new List<DutyShift>();
            var schedule = shifts.Concat(pending);

            for (; start.Date <= throughDate.Date; start = start.AddDays(1))
            {
                DateTime end = start.AddDays(1);
                if (end.AddTicks(-1).Date > agreement.DateEndContract.Date)
                    break;

                var available = guards
                    .Where(guard => HasRequiredRest(guard, start, end, null, schedule))
                    .OrderBy(guard => schedule
                        .Where(shift => shift.ActualGuard == guard && shift.EndTime <= start)
                        .Select(shift => shift.EndTime)
                        .DefaultIfEmpty(DateTime.MinValue)
                        .Max())
                    .Take(agreement.GuardsPerShift)
                    .ToList();

                if (available.Count < agreement.GuardsPerShift)
                    throw new InvalidOperationException(
                        $"На {start:dd.MM.yyyy HH:mm} нужны {agreement.GuardsPerShift} охранников, доступны {available.Count}. Новые смены не добавлены.");

                foreach (Staff guard in available)
                    pending.Add(new DutyShift(agreement, guard, start));
            }

            if (pending.Count == 0)
                throw new InvalidOperationException("До окончания договора нет места для полной суточной смены.");

            shifts.AddRange(pending);
            return pending.AsReadOnly();
        }

        // Проверяет, подходит ли сотрудник для замены на выбранной смене.
        public bool CanReplaceGuard(DutyShift shift, Staff guard)
        {
            return shifts.Contains(shift)
                && guard != shift.ActualGuard
                && guard.Position.IsGuard
                && !guard.IsDismissed
                && HasRequiredRest(guard, shift.StartTime, shift.EndTime, shift);
        }

        // Отклоняет назначение при пересечении смен или недостаточном отдыхе.
        private void CheckRest(Staff guard, DateTime startTime, DateTime endTime, DutyShift? excludedShift)
        {
            if (!HasRequiredRest(guard, startTime, endTime, excludedShift))
                throw new InvalidOperationException("Дежурства пересекаются или между ними меньше двух дней отдыха.");
        }

        // Проверяет перерыв между новой и другими сменами охранника.
        private bool HasRequiredRest(
            Staff guard, DateTime startTime, DateTime endTime, DutyShift? excludedShift,
            IEnumerable<DutyShift>? schedule = null)
        {
            TimeSpan requiredRest = TimeSpan.FromDays(2);

            foreach (DutyShift existingShift in schedule ?? shifts)
            {
                if (existingShift == excludedShift || existingShift.ActualGuard != guard)
                    continue;

                TimeSpan restBefore = startTime - existingShift.EndTime;
                TimeSpan restAfter = existingShift.StartTime - endTime;

                if (restBefore < requiredRest && restAfter < requiredRest)
                    return false;
            }

            return true;
        }
    }
}
