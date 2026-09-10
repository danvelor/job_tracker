using FluentAssertions;
using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain.UnitTests.Common;

public sealed class ValueObjectTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;
        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void Two_values_with_the_same_components_are_equal()
    {
        new Money(10m, "USD").Should().Be(new Money(10m, "USD"));
    }

    [Fact]
    public void Two_values_differing_in_any_component_are_not_equal()
    {
        new Money(10m, "USD").Should().NotBe(new Money(10m, "EUR"));
        new Money(10m, "USD").Should().NotBe(new Money(11m, "USD"));
    }

    [Fact]
    public void Equal_values_share_a_hash_code()
    {
        new Money(10m, "USD").GetHashCode()
            .Should().Be(new Money(10m, "USD").GetHashCode());
    }

    [Fact]
    public void A_value_is_not_equal_to_null_or_to_another_type()
    {
        new Money(10m, "USD").Equals(null).Should().BeFalse();
        new Money(10m, "USD").Equals("USD").Should().BeFalse();
    }

    [Fact]
    public void The_equality_operators_agree_with_Equals()
    {
        var left = new Money(10m, "USD");
        var right = new Money(10m, "USD");

        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
    }

    [Fact]
    public void Two_nulls_are_equal_and_a_null_differs_from_a_value()
    {
        Money? nothing = null;
        Money? alsoNothing = null;

        (nothing == alsoNothing).Should().BeTrue();
        (nothing == new Money(10m, "USD")).Should().BeFalse();
    }
}
