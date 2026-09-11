namespace SmartRFQ.API.Services;

public interface IBusinessDayCalculator
{
    int CountBusinessDays(DateTime acceptedAtUtc, DateTime? finishedAtUtc, IReadOnlySet<DateOnly> holidays);
    DateOnly GetFirstBusinessDay(DateTime fromUtc, IReadOnlySet<DateOnly> holidays);
}

public class BusinessDayCalculator : IBusinessDayCalculator
{
    private static readonly TimeZoneInfo ThaiZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok");

    private static DateOnly ToEffectiveDate(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, ThaiZone);
        var date = DateOnly.FromDateTime(local.Date);
        return local.Hour >= 16 ? date.AddDays(1) : date;
    }

    private static bool IsBusinessDay(DateOnly d, IReadOnlySet<DateOnly> holidays) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !holidays.Contains(d);

    public DateOnly GetFirstBusinessDay(DateTime fromUtc, IReadOnlySet<DateOnly> holidays)
    {
        var d = ToEffectiveDate(fromUtc).AddDays(1);
        while (!IsBusinessDay(d, holidays))
            d = d.AddDays(1);
        return d;
    }


    public int CountBusinessDays(DateTime fromUtc, DateTime? toUtc, IReadOnlySet<DateOnly> holidays)
    {
        // Start date
       var startDate = GetFirstBusinessDay(fromUtc, holidays); 

        // End date
        var endDate = toUtc.HasValue
        ? ToEffectiveDate(toUtc.Value)
        : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ThaiZone).Date);

        if (endDate < startDate) return 0;

        int count = 0;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (holidays.Contains(date)) continue;
            count++;
        }
        return count;
    }

}