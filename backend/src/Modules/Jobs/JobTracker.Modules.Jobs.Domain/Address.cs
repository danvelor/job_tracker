using JobTracker.Common.Domain;

namespace JobTracker.Modules.Jobs.Domain;

public sealed class Address : ValueObject
{
    private Address(
        string street, string city, string state, string zipCode,
        decimal latitude, decimal longitude)
    {
        Street = street;
        City = city;
        State = state;
        ZipCode = zipCode;
        Latitude = latitude;
        Longitude = longitude;
    }

    private Address() { }

    public string Street { get; private init; } = string.Empty;
    public string City { get; private init; } = string.Empty;
    public string State { get; private init; } = string.Empty;
    public string ZipCode { get; private init; } = string.Empty;
    public decimal Latitude { get; private init; }
    public decimal Longitude { get; private init; }

    public static Result<Address> Create(
        string street, string city, string state, string zipCode,
        decimal latitude, decimal longitude)
    {
        if (string.IsNullOrWhiteSpace(street)
            || string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(state)
            || string.IsNullOrWhiteSpace(zipCode))
        {
            return Result.Failure<Address>(JobErrors.AddressIncomplete);
        }

        if (latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return Result.Failure<Address>(JobErrors.CoordinatesOffGlobe);
        }

        return Result.Success(new Address(street, city, state, zipCode, latitude, longitude));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Latitude;
        yield return Longitude;
    }
}
