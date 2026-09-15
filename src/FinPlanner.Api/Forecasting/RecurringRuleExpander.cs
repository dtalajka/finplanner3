using FinPlanner.Api.Domain;

namespace FinPlanner.Api.Forecasting;

public static class RecurringRuleExpander
{
    public static IEnumerable<DateOnly> Expand(RecurringRule rule, DateOnly from, DateOnly to)
    {
        var windowStart = rule.StartDate > from ? rule.StartDate : from;
        var windowEnd = rule.EndDate is { } end && end < to ? end : to;
        if (windowStart > windowEnd) yield break;

        foreach (var date in GenerateDates(rule, windowStart, windowEnd))
            yield return date;
    }

    private static IEnumerable<DateOnly> GenerateDates(RecurringRule rule, DateOnly windowStart, DateOnly windowEnd)
    {
        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
                    yield return date;
                break;

            case RecurrenceFrequency.Weekly:
                var daysSinceStart = windowStart.DayNumber - rule.StartDate.DayNumber;
                var offset = ((daysSinceStart % 7) + 7) % 7;
                for (var date = windowStart.AddDays(offset == 0 ? 0 : 7 - offset); date <= windowEnd; date = date.AddDays(7))
                    yield return date;
                break;

            case RecurrenceFrequency.Monthly:
                foreach (var date in MonthlyDates(rule.DayOfMonth ?? rule.StartDate.Day, windowStart, windowEnd))
                    yield return date;
                break;

            case RecurrenceFrequency.Yearly:
                foreach (var date in YearlyDates(rule.StartDate.Month, rule.StartDate.Day, windowStart, windowEnd))
                    yield return date;
                break;
        }
    }

    private static IEnumerable<DateOnly> MonthlyDates(int anchorDay, DateOnly windowStart, DateOnly windowEnd)
    {
        var year = windowStart.Year;
        var month = windowStart.Month;
        while (true)
        {
            var day = Math.Min(anchorDay, DateTime.DaysInMonth(year, month));
            var candidate = new DateOnly(year, month, day);
            if (candidate > windowEnd) yield break;
            if (candidate >= windowStart) yield return candidate;
            month++;
            if (month > 12) { month = 1; year++; }
        }
    }

    private static IEnumerable<DateOnly> YearlyDates(int anchorMonth, int anchorDay, DateOnly windowStart, DateOnly windowEnd)
    {
        var year = windowStart.Year;
        while (true)
        {
            var day = Math.Min(anchorDay, DateTime.DaysInMonth(year, anchorMonth));
            var candidate = new DateOnly(year, anchorMonth, day);
            if (candidate > windowEnd) yield break;
            if (candidate >= windowStart) yield return candidate;
            year++;
        }
    }
}
