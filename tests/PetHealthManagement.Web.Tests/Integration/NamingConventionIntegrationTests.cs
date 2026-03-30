using System.Net;
using Microsoft.EntityFrameworkCore;
using PetHealthManagement.Web.Data;
using PetHealthManagement.Web.Models;
using PetHealthManagement.Web.Tests.Infrastructure;

namespace PetHealthManagement.Web.Tests.Integration;

public class NamingConventionIntegrationTests
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 3, 30, 9, 0, 0, TimeSpan.FromHours(9));

    [Fact]
    public async Task PetsIndex_UsesLowerCamelCaseQueryKeys()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerWithPets(dbContext, petCount: 11);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");
        using var response = await client.GetAsync("/Pets?nameKeyword=Mugi&speciesFilter=DOG&page=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await ReadDecodedHtmlAsync(response);
        Assert.Contains("name=\"nameKeyword\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"speciesFilter\"", html, StringComparison.Ordinal);
        Assert.Contains("/Pets/Create?returnUrl=%2FPets%3FnameKeyword%3DMugi%26speciesFilter%3DDOG%26page%3D1", html, StringComparison.Ordinal);
        Assert.Contains("/Pets/Details/1?returnUrl=%2FPets%3FnameKeyword%3DMugi%26speciesFilter%3DDOG%26page%3D1", html, StringComparison.Ordinal);
        Assert.Contains("/Pets?nameKeyword=Mugi&speciesFilter=DOG&page=2", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"NameKeyword\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"SpeciesFilter\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("?ReturnUrl=", html, StringComparison.Ordinal);
        Assert.DoesNotContain("&Page=", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AccountAndPetDetails_UseLowerCamelCaseHiddenAndLinkKeys()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerWithPets(dbContext, petCount: 1);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");

        using (var editProfileResponse = await client.GetAsync("/Account/EditProfile?returnUrl=%2FPets%3Fpage%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, editProfileResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(editProfileResponse);
            Assert.Contains("name=\"returnUrl\" value=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"ReturnUrl\"", html, StringComparison.Ordinal);
        }

        using (var petDetailsResponse = await client.GetAsync("/Pets/Details/1?returnUrl=%2FPets%3Fpage%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, petDetailsResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(petDetailsResponse);
            Assert.Contains("name=\"returnUrl\" value=\"/Pets?page=2\"", html, StringComparison.Ordinal);
            Assert.Contains("/HealthLogs?petId=1", html, StringComparison.Ordinal);
            Assert.Contains("/ScheduleItems?petId=1", html, StringComparison.Ordinal);
            Assert.Contains("/Visits?petId=1", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"ReturnUrl\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("?PetId=", html, StringComparison.Ordinal);
            Assert.DoesNotContain("&PetId=", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ActivityDetails_UseLowerCamelCaseHiddenKeys()
    {
        await using var factory = new IntegrationTestWebApplicationFactory();
        await factory.ResetDatabaseAsync(dbContext =>
        {
            SeedOwnerWithActivityRecords(dbContext);
            return Task.CompletedTask;
        });

        using var client = factory.CreateAuthenticatedClient("owner-user");

        using (var healthLogResponse = await client.GetAsync("/HealthLogs/Details/10?returnUrl=%2FHealthLogs%3FpetId%3D1%26page%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, healthLogResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(healthLogResponse);
            Assert.Contains("name=\"petId\" value=\"1\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"returnUrl\" value=\"/HealthLogs?petId=1&page=2\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"PetId\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"ReturnUrl\"", html, StringComparison.Ordinal);
        }

        using (var scheduleResponse = await client.GetAsync("/ScheduleItems/Details/20?returnUrl=%2FScheduleItems%3FpetId%3D1%26page%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(scheduleResponse);
            Assert.Contains("name=\"isDone\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"petId\" value=\"1\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"returnUrl\" value=\"/ScheduleItems/Details/20?returnUrl=%2FScheduleItems%3FpetId%3D1%26page%3D2\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"IsDone\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"PetId\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"ReturnUrl\"", html, StringComparison.Ordinal);
        }

        using (var visitResponse = await client.GetAsync("/Visits/Details/30?returnUrl=%2FVisits%3FpetId%3D1%26page%3D2"))
        {
            Assert.Equal(HttpStatusCode.OK, visitResponse.StatusCode);

            var html = await ReadDecodedHtmlAsync(visitResponse);
            Assert.Contains("name=\"petId\" value=\"1\"", html, StringComparison.Ordinal);
            Assert.Contains("name=\"returnUrl\" value=\"/Visits?petId=1&page=2\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"PetId\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"ReturnUrl\"", html, StringComparison.Ordinal);
        }
    }

    private static void SeedOwnerWithPets(ApplicationDbContext dbContext, int petCount)
    {
        dbContext.Users.Add(new ApplicationUser
        {
            Id = "owner-user",
            UserName = "owner-user",
            Email = "owner@example.com"
        });

        var pets = Enumerable.Range(1, petCount)
            .Select(index => new Pet
            {
                Id = index,
                OwnerId = "owner-user",
                Name = $"Mugi {index:00}",
                SpeciesCode = "DOG",
                IsPublic = false,
                CreatedAt = SeedTimestamp.AddMinutes(-index),
                UpdatedAt = SeedTimestamp.AddMinutes(-index)
            });

        dbContext.Pets.AddRange(pets);
    }

    private static void SeedOwnerWithActivityRecords(ApplicationDbContext dbContext)
    {
        SeedOwnerWithPets(dbContext, petCount: 1);

        dbContext.HealthLogs.Add(new HealthLog
        {
            Id = 10,
            PetId = 1,
            RecordedAt = SeedTimestamp,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        dbContext.ScheduleItems.Add(new ScheduleItem
        {
            Id = 20,
            PetId = 1,
            DueDate = new DateTime(2026, 3, 31),
            Type = ScheduleItemTypeCatalog.Other,
            Title = "Checkup",
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });

        dbContext.Visits.Add(new Visit
        {
            Id = 30,
            PetId = 1,
            VisitDate = new DateTime(2026, 3, 30),
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp
        });
    }

    private static async Task<string> ReadDecodedHtmlAsync(HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
}
