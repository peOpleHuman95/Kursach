
namespace Security_agent
{
    // Запись о замене охранника
    public class ShiftReplacement
    {
        public Staff PreviousGuard {get;}
        public Staff NewGuard {get;}
        public string Reason {get;}

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
        public DateTime StartTime {get;}
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
        {
            if (agreement == null) throw new ArgumentNullException(nameof(agreement));

            // Считаем дату окончания договора включительно
            DateTime contractEnd = agreement.DateEndContract.Date.AddDays(1);

            if (startTime < agreement.DateAgreement.Date || startTime.AddDays(1) > contractEnd)
                throw new ArgumentException("Дежурство должно полностью входить в период договора.", nameof(startTime));
            StartTime = startTime;
            CheckGuard(guard);
            Agreement = agreement;
            PlannedGuard = guard;
            ActualGuard = guard;
        }

        // Запись замены и её причины в истории дежурства
        internal void ApplyReplaceGuard(Staff newGuard, string reason)
        {
            CheckGuard(newGuard);

            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Должна быть причина замены.", nameof(reason));
            if (newGuard == ActualGuard) throw new InvalidOperationException("Этот охранник уже назначен на дежурство.");
            
            replacements.Add(new ShiftReplacement(ActualGuard, newGuard, reason.Trim()));
            ActualGuard = newGuard;
        }

        // Проверка должности и увольнения на дату начала дежурства
        private void CheckGuard(Staff guard)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            if (!guard.Position.IsGuard) throw new InvalidOperationException("Дежурить может только охранник.");
            if (guard.DismissalDate.HasValue && StartTime.Date >= guard.DismissalDate.Value.Date) 
                throw new InvalidOperationException("Охранник уже уволен на момент дежурства.");
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

        // Проверка пересечений и минимум двух суток между дежурствами
        private void CheckRest(
            Staff guard,
            DateTime startTime,
            DateTime endTime,
            DutyShift? excludedShift)
            {
                TimeSpan requireRest = TimeSpan.FromDays(2);

                foreach(DutyShift existingShift in shifts)
                {
                    if (existingShift == excludedShift) continue;
                    if (existingShift.ActualGuard != guard) continue;
                    TimeSpan restBefore = startTime - existingShift.EndTime;
                    TimeSpan restAfter = existingShift.StartTime - endTime;
                    if (restBefore < requireRest && restAfter < requireRest)
                        throw new InvalidOperationException("Дежурства пересекаются или между ними меньше двух дней отдыха.");


                }
            }
    }
}