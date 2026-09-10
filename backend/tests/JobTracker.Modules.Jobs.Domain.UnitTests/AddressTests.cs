using FluentAssertions;

namespace JobTracker.Modules.Jobs.Domain.UnitTests;

public sealed class AddressTests
{
    private static Address AnAddress(string street = "12 Elm St") =>
        Address.Create(street, "Springfield", "IL", "62701", 39.78m, -89.65m).Value;

    [Fact]
    public void Two_addresses_with_the_same_components_are_equal()
    {
        AnAddress().Should().Be(AnAddress());
    }

    [Fact]
    public void Addresses_differing_in_any_component_are_not_equal()
    {
        AnAddress().Should().NotBe(AnAddress("8 Oak Ave"));
    }

    [Fact]
    public void Equal_addresses_share_a_hash_code()
    {
        AnAddress().GetHashCode().Should().Be(AnAddress().GetHashCode());
    }

    [Fact]
    public void Equality_is_structural_rather_than_by_reference()
    {
        var left = AnAddress();
        var right = AnAddress();

        ReferenceEquals(left, right).Should().BeFalse();
        (left == right).Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Springfield", "IL", "62701")]
    [InlineData("12 Elm St", "", "IL", "62701")]
    [InlineData("12 Elm St", "Springfield", "", "62701")]
    [InlineData("12 Elm St", "Springfield", "IL", "")]
    public void Every_textual_component_is_required(
        string street, string city, string state, string zip)
    {
        Address.Create(street, city, state, zip, 39.78m, -89.65m).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void Coordinates_outside_the_globe_are_refused(decimal latitude, decimal longitude)
    {
        Address.Create("12 Elm St", "Springfield", "IL", "62701", latitude, longitude)
            .IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void The_extremes_of_the_globe_are_accepted(decimal latitude, decimal longitude)
    {
        Address.Create("12 Elm St", "Springfield", "IL", "62701", latitude, longitude)
            .IsSuccess.Should().BeTrue();
    }
}
