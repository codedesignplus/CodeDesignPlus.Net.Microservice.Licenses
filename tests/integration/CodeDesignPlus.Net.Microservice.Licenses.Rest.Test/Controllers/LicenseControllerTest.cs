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
            && x.BillingType == license.BillingType
            && x.Price == license.Price
            && x.Currency.Name == license.Currency.Name
            && x.Currency.Code == license.Currency.Code
            && x.Currency.Symbol == license.Currency.Symbol
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
        Assert.Equal(licenseCreated.BillingType, license.BillingType);
        Assert.Equal(licenseCreated.Price, license.Price);
        Assert.Equal(licenseCreated.Currency.Name, license.Currency.Name);
        Assert.Equal(licenseCreated.Currency.Code, license.Currency.Code);
        Assert.Equal(licenseCreated.Currency.Symbol, license.Currency.Symbol);
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
            BillingType = BillingTypeEnum.Monthly,
            Currency = Currency.Create("United States Dollar", "USD", "$"),
            Price = 100,
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
        Assert.Equal(data.BillingType, license.BillingType);
        Assert.Equal(data.Price, license.Price);
        Assert.Equal(data.Currency.Name, license.Currency.Name);
        Assert.Equal(data.Currency.Code, license.Currency.Code);
        Assert.Equal(data.Currency.Symbol, license.Currency.Symbol);
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
        Assert.Equal(data.BillingType, license.BillingType);
        Assert.Equal(data.Price, license.Price);
        Assert.Equal(data.Currency.Name, license.Currency.Name);
        Assert.Equal(data.Currency.Code, license.Currency.Code);
        Assert.Equal(data.Currency.Symbol, license.Currency.Symbol);
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
            BillingType = BillingTypeEnum.Annualy,
            Currency = Currency.Create("United States Dollar", "USD", "$"),
            Price = 200,
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
        Assert.Equal(data.BillingType, license.BillingType);
        Assert.Equal(data.Price, license.Price);
        Assert.Equal(data.Currency.Name, license.Currency.Name);
        Assert.Equal(data.Currency.Code, license.Currency.Code);
        Assert.Equal(data.Currency.Symbol, license.Currency.Symbol);
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
        Assert.Equal(data.BillingType, license.BillingType);
        Assert.Equal(data.Price, license.Price);
        Assert.Equal(data.Currency.Name, license.Currency.Name);
        Assert.Equal(data.Currency.Code, license.Currency.Code);
        Assert.Equal(data.Currency.Symbol, license.Currency.Symbol);
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
            BillingType = BillingTypeEnum.Monthly,
            Currency = Currency.Create("United States Dollar", "USD", "$"),
            Price = 100,
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
