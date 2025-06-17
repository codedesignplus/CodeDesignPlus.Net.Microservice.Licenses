using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Location
{
    public Country Country { get; private set; }
    public State State { get; private set; }
    public City City { get; private set; }
    public Locality Locality { get; private set; }
    public Neighborhood Neighborhood { get; private set; }
    public string Address { get; private set; } = null!;
    public string PostalCode { get; private set; } = null!;

    [JsonConstructor]
    public Location(Country country, State state, City city, Locality locality, Neighborhood neighborhood, string address, string postalCode)
    {
        DomainGuard.IsNull(country, Errors.CountryIsNull);
        DomainGuard.IsNull(state, Errors.StateIsNull);
        DomainGuard.IsNull(city, Errors.CityIsNull);
        DomainGuard.IsNull(locality, Errors.LocalityIsNull);
        DomainGuard.IsNull(neighborhood, Errors.NeighborhoodIsNull);
        DomainGuard.IsNullOrEmpty(address, Errors.AddressIsRequired);
        DomainGuard.IsNullOrEmpty(postalCode, Errors.PostalCodeIsRequired);

        this.Country = country;
        this.State = state;
        this.City = city;
        this.Locality = locality;
        this.Neighborhood = neighborhood;
        this.Address = address;
        this.PostalCode = postalCode;
    }

    public static Location Create(Country country, State state, City city, Locality locality, Neighborhood neighborhood, string address, string postalCode)
    {
        return new Location(country, state, city, locality, neighborhood, address, postalCode);
    }
}
