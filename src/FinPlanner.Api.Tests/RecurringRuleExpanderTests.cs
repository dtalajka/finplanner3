using FinPlanner.Api.Domain;
using FinPlanner.Api.Forecasting;
using Xunit;

namespace FinPlanner.Api.Tests;

public class RecurringRuleExpanderTests
{
    private static RecurringRule Rule(RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate = null, short? dayOfMonth = null) =>
        new() { FamilyId = 1, PlanId = 1, Name = "Rule", Amount = 100m, Frequency = frequency, StartDate = startDate, EndDate = endDate, DayOfMonth = dayOfMonth, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    [Fact]
    public void Monthly_Day31_ClampsToLastDayOfShortMonths()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 31), dayOfMonth: 31);

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30)).ToList();

        Assert.Equal([new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)], dates);
    }

    [Fact]
    public void Monthly_Day31_ClampsToFeb29InLeapYear()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new DateOnly(2024, 1, 31), dayOfMonth: 31);

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 29)).ToList();

        Assert.Equal([new DateOnly(2024, 2, 29)], dates);
    }

    [Fact]
    public void Yearly_LeapDayStart_RoundTripsThroughNonLeapAndLeapYears()
    {
        var rule = Rule(RecurrenceFrequency.Yearly, new DateOnly(2024, 2, 29));

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2024, 1, 1), new DateOnly(2028, 12, 31)).ToList();

        Assert.Equal([new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 28), new DateOnly(2026, 2, 28), new DateOnly(2027, 2, 28), new DateOnly(2028, 2, 29)], dates);
    }

    [Fact]
    public void Weekly_StaysOnCorrectWeekday_AcrossMonthBoundary()
    {
        var rule = Rule(RecurrenceFrequency.Weekly, new DateOnly(2026, 1, 7)); // a Wednesday

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2026, 1, 20), new DateOnly(2026, 2, 10)).ToList();

        Assert.Equal([new DateOnly(2026, 1, 21), new DateOnly(2026, 1, 28), new DateOnly(2026, 2, 4)], dates);
        Assert.All(dates, date => Assert.Equal(DayOfWeek.Wednesday, date.DayOfWeek));
    }

    [Fact]
    public void Daily_CrossesYearBoundary()
    {
        var rule = Rule(RecurrenceFrequency.Daily, new DateOnly(2025, 12, 30));

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2025, 12, 30), new DateOnly(2026, 1, 2)).ToList();

        Assert.Equal([new DateOnly(2025, 12, 30), new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)], dates);
    }

    [Fact]
    public void EndDate_StopsGeneration()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new DateOnly(2026, 1, 1), endDate: new DateOnly(2026, 3, 1));

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).ToList();

        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 1)], dates);
    }

    [Fact]
    public void StartDateInFuture_FirstOccurrenceIsStartDate()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new DateOnly(2026, 6, 15));

        var dates = RecurringRuleExpander.Expand(rule, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 31)).ToList();

        Assert.Equal([new DateOnly(2026, 6, 15), new DateOnly(2026, 7, 15), new DateOnly(2026, 8, 15)], dates);
    }

    [Fact]
    public void StartDateInPast_NeverGeneratesBeforeWindowStart()
    {
        var rule = Rule(RecurrenceFrequency.Monthly, new DateOnly(2020, 1, 15));
        var t0 = new DateOnly(2026, 9, 14);

        var dates = RecurringRuleExpander.Expand(rule, t0, new DateOnly(2026, 10, 31)).ToList();

        Assert.All(dates, date => Assert.True(date >= t0));
        Assert.Equal([new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 15)], dates);
    }
}
