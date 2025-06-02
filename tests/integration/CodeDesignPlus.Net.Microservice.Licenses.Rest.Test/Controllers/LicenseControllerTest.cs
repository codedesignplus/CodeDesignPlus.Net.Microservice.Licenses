using System;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using NodaTime.Serialization.SystemTextJson;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Test.Controllers;


public class LicenseControllerTest : ServerBase<Program>, IClassFixture<Server<Program>>
{
    private readonly System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    }.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, BillingModel.FlatRate);
    private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Annualy, Currency.Create("United States Dollar", "USD", "$"), 1000, BillingModel.FlatRate);

    private readonly ModuleDto module = new()
    {
        Id = Guid.NewGuid(),
        Name = "Module Test",
    };

    public LicenseControllerTest(Server<Program> server) : base(server)
    {
        server.InMemoryCollection = (x) =>
        {
            x.Add("Vault:Enable", "false");
            x.Add("Vault:Address", "http://localhost:8200");
            x.Add("Vault:Token", "root");
            x.Add("Solution", "CodeDesignPlus");
            x.Add("AppName", "my-test");
            x.Add("RabbitMQ:UserName", "guest");
            x.Add("RabbitMQ:Password", "guest");
            x.Add("Security:ValidAudiences:0", Guid.NewGuid().ToString());
        };
    }

    [Fact]
    public async Task GetLicenses_ReturnOk()
    {
        var license = await this.CreateLicenseAsync();

        var response = await this.RequestAsync("http://localhost/api/License", null, HttpMethod.Get);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        var pagination = System.Text.Json.JsonSerializer.Deserialize<Pagination<LicenseDto>>(json, this.options);
        var licenses = pagination?.Data;

        Assert.NotNull(licenses);
        Assert.NotEmpty(licenses);
        Assert.Contains(licenses, x =>
            x.Id == license.Id
            && x.Name == license.Name
            && x.Description == license.Description
            && x.Prices.FirstOrDefault(o => o.BillingType == this.PriceMonthly.BillingType && o.Currency.Code == this.PriceMonthly.Currency.Code && o.Currency.Name == this.PriceMonthly.Currency.Name && o.Currency.Symbol == this.PriceMonthly.Currency.Symbol) != null
            && x.Prices.FirstOrDefault(o => o.BillingType == this.PriceAnnualy.BillingType && o.Currency.Code == this.PriceAnnualy.Currency.Code && o.Currency.Name == this.PriceAnnualy.Currency.Name && o.Currency.Symbol == this.PriceAnnualy.Currency.Symbol) != null
            && x.IdLogo != Guid.Empty
            && x.TermsOfService == license.TermsOfService
            && x.Modules.Any(y => y.Id == module.Id && y.Name == module.Name)
        );
    }

    [Fact]
    public async Task GetLicenseById_ReturnOk()
    {
        var licenseCreated = await this.CreateLicenseAsync();

        var response = await this.RequestAsync($"http://localhost/api/License/{licenseCreated.Id}", null, HttpMethod.Get);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        var license = System.Text.Json.JsonSerializer.Deserialize<LicenseDto>(json, this.options);

        Assert.NotNull(license);
        Assert.Equal(licenseCreated.Id, license.Id);
        Assert.Equal(licenseCreated.Name, license.Name);
        Assert.Equal(licenseCreated.Description, license.Description);
        Assert.Contains(licenseCreated.Modules, x =>
            x.Id == module.Id
            && x.Name == module.Name
        );
    }

    [Fact]
    public async Task CreateLicense_ReturnNoContent()
    {
        var data = new CreateLicenseDto()
        {
            Id = Guid.NewGuid(),
            Name = "License Test",
            Description = "License Test Description",
            Attributes = new Dictionary<string, string>()
            {
                { "UserLimit", "3" },
                { "InvoiceLimit", "5" }
            },
            Prices =
            [
                PriceMonthly,
                PriceAnnualy
            ],
            IdLogo = Guid.NewGuid(),
            TermsOfService = "Terms of service for License Test",
            Modules = [module]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(data, this.options);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await this.RequestAsync("http://localhost/api/License", content, HttpMethod.Post);

        var license = await this.GetRecordAsync(data.Id);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(data.Id, license.Id);
        Assert.Equal(data.Name, license.Name);
        Assert.Equal(data.Description, license.Description);
        Assert.Contains(license.Prices, x => x.Pricing == PriceMonthly.Pricing && x.BillingType == PriceMonthly.BillingType && x.Currency.Code == PriceMonthly.Currency.Code && x.Currency.Name == PriceMonthly.Currency.Name && x.Currency.Symbol == PriceMonthly.Currency.Symbol);
        Assert.Contains(license.Prices, x => x.Pricing == PriceAnnualy.Pricing && x.BillingType == PriceAnnualy.BillingType && x.Currency.Code == PriceAnnualy.Currency.Code && x.Currency.Name == PriceAnnualy.Currency.Name && x.Currency.Symbol == PriceAnnualy.Currency.Symbol);
        Assert.Equal(data.IdLogo, license.IdLogo);
        Assert.Equal(data.TermsOfService, license.TermsOfService);
        Assert.Contains(data.Modules, x =>
            x.Id == module.Id
            && x.Name == module.Name
        );
    }

    [Fact]
    public async Task AddModules_ReturnNoContent()
    {
        var data = await this.CreateLicenseAsync();

        var moduleNew = new AddModuleDto()
        {
            IdModule = Guid.NewGuid(),
            Name = "Module Test New",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(moduleNew, this.options);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await this.RequestAsync($"http://localhost/api/License/{data.Id}/module", content, HttpMethod.Post);

        var license = await this.GetRecordAsync(data.Id);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(data.Id, license.Id);
        Assert.Equal(data.Name, license.Name);
        Assert.Equal(data.Description, license.Description);

        Assert.Contains(license.Modules, x =>
            x.Id == moduleNew.IdModule
            && x.Name == moduleNew.Name
        );
    }

    [Fact]
    public async Task UpdateLicense_ReturnNoContent()
    {
        var licenseCreated = await this.CreateLicenseAsync();

        var data = new UpdateLicenseDto()
        {
            Id = licenseCreated.Id,
            Name = "License Test Updated",
            Description = "License Test Description Updated",
            Modules = [module]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(data, this.options);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await this.RequestAsync($"http://localhost/api/License/{licenseCreated.Id}", content, HttpMethod.Put);

        var license = await this.GetRecordAsync(data.Id);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(data.Id, license.Id);
        Assert.Equal(data.Name, license.Name);
        Assert.Equal(data.Description, license.Description);
        Assert.Contains(data.Modules, x =>
            x.Id == module.Id
            && x.Name == module.Name
        );
    }

    [Fact]
    public async Task DeleteLicense_ReturnNoContent()
    {
        var licenseCreated = await this.CreateLicenseAsync();

        var response = await this.RequestAsync($"http://localhost/api/License/{licenseCreated.Id}", null, HttpMethod.Delete);

        Assert.NotNull(response);
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    
    [Fact]
    public async Task RemoveModules_ReturnNoContent()
    {
        var data = await this.CreateLicenseAsync();

        var moduleNew = new AddModuleDto()
        {
            IdModule = Guid.NewGuid(),
            Name = "Module Test New",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(moduleNew, this.options);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await this.RequestAsync($"http://localhost/api/License/{data.Id}/module", content, HttpMethod.Post);

        var responseRemove = await this.RequestAsync($"http://localhost/api/License/{data.Id}/module/{moduleNew.IdModule}", null, HttpMethod.Delete);

        var license = await this.GetRecordAsync(data.Id);

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.NotNull(responseRemove);
        Assert.Equal(HttpStatusCode.NoContent, responseRemove.StatusCode);

        Assert.Equal(data.Id, license.Id);
        Assert.Equal(data.Name, license.Name);
        Assert.Equal(data.Description, license.Description);
        Assert.DoesNotContain(license.Modules, x =>
            x.Id == moduleNew.IdModule
            && x.Name == moduleNew.Name
        );
    }


    private async Task<CreateLicenseDto> CreateLicenseAsync()
    {
        var data = new CreateLicenseDto()
        {
            Id = Guid.NewGuid(),
            Name = "License Test",
            Description = "License Test Description",
            Attributes = new Dictionary<string, string>()
            {
                { "UserLimit", "3" },
                { "InvoiceLimit", "5" }
            },
            Prices =
            [
                PriceMonthly,
                PriceAnnualy
            ],
            IdLogo = Guid.NewGuid(),
            TermsOfService = "Terms of service for License Test",
            Modules = [module]
        };

        var json = System.Text.Json.JsonSerializer.Serialize(data, this.options);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await this.RequestAsync("http://localhost/api/License", content, HttpMethod.Post);

        return data;
    }

    private async Task<LicenseDto> GetRecordAsync(Guid id)
    {
        var response = await this.RequestAsync($"http://localhost/api/License/{id}", null, HttpMethod.Get);

        var json = await response.Content.ReadAsStringAsync();

        return System.Text.Json.JsonSerializer.Deserialize<LicenseDto>(json, this.options)!;
    }

    private async Task<HttpResponseMessage> RequestAsync(string uri, HttpContent? content, HttpMethod method)
    {
        var httpRequestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri(uri),
            Content = content,
            Method = method
        };
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("TestAuth");

        var response = await Client.SendAsync(httpRequestMessage);

        if (!response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadAsStringAsync();
            throw new Exception(data);
        }

        return response;
    }

}
