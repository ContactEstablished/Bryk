using System.Globalization;
using Bryk.Application.Wellness;
using Bryk.Application.Wellness.Validators;
using FluentAssertions;
using Xunit;

namespace Bryk.Application.Tests.Wellness;

public class WellnessEntryRequestValidatorTests
{
    private static readonly WellnessEntryRequestValidator Validator = new();
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // A minimal valid entry: a real date plus exactly one metric.
    private static WellnessEntryRequest Entry() => new() { Date = Today, RestingHr = 50 };

    private static decimal Dec(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    [Fact]
    public void Date_Default_IsRejectedWithADateMessage()
    {
        var request = new WellnessEntryRequest { Date = default, RestingHr = 50 };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        // Exactly one message: the future rule is guarded off so a default date does not fire twice.
        result.Errors.Should().ContainSingle();
        result.Errors[0].ErrorMessage.Should().StartWith("Date:");
    }

    [Fact]
    public void Date_Today_IsAccepted()
    {
        var request = Entry();
        request.Date = Today;

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Date_Yesterday_IsAccepted()
    {
        var request = Entry();
        request.Date = Today.AddDays(-1);

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Date_Tomorrow_IsRejected()
    {
        var request = Entry();
        request.Date = Today.AddDays(1);

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Date:"));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("16", true)]
    [InlineData("-0.01", false)]
    [InlineData("16.01", false)]
    public void SleepHours_BoundsAreInclusive(string value, bool expected)
    {
        var request = Entry();
        request.SleepHours = Dec(value);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("SleepHours:"));
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void SleepQuality_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.SleepQuality = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("SleepQuality:"));
        }
    }

    [Theory]
    [InlineData(25, true)]
    [InlineData(120, true)]
    [InlineData(24, false)]
    [InlineData(121, false)]
    public void RestingHr_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.RestingHr = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("RestingHr:"));
        }
    }

    [Theory]
    [InlineData("30", true)]
    [InlineData("250", true)]
    [InlineData("29.99", false)]
    [InlineData("250.01", false)]
    public void WeightKg_BoundsAreInclusive(string value, bool expected)
    {
        var request = Entry();
        request.WeightKg = Dec(value);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("WeightKg:"));
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void Soreness_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.Soreness = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Soreness:"));
        }
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(250, true)]
    [InlineData(9, false)]
    [InlineData(251, false)]
    public void HrvMs_BoundsAreInclusive(int value, bool expected)
    {
        var request = Entry();
        request.HrvMs = value;

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("HrvMs:"));
        }
    }

    [Fact]
    public void SingleMetric_IsAccepted()
    {
        // Partial entries are the norm — one metric is a complete, valid day.
        var request = new WellnessEntryRequest { Date = Today, Soreness = 4 };

        Validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AllMetricsNull_IsRejected()
    {
        var request = new WellnessEntryRequest { Date = Today };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Entry:"));
    }

    [Fact]
    public void NotesOnly_IsRejected()
    {
        // Notes is not a metric: prose feeds no tile and no average.
        var request = new WellnessEntryRequest { Date = Today, Notes = "felt rough" };

        var result = Validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Entry:"));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Notes_Over1000Characters_IsRejected(int length, bool expected)
    {
        var request = Entry();
        request.Notes = new string('x', length);

        var result = Validator.Validate(request);

        result.IsValid.Should().Be(expected);
        if (!expected)
        {
            result.Errors.Should().Contain(e => e.ErrorMessage.StartsWith("Notes:"));
        }
    }
}
