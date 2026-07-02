using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bryk.API.Tests.Fixtures;
using Bryk.Application.Events;
using Bryk.Application.Onboarding;
using Bryk.Application.Training;
using Bryk.Domain.Entities;
using Bryk.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bryk.API.Tests.Events;

public class EventsControllerGetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static EventDto MakeEvent(string name, DateOnly date) => new()
    {
        Name = name,
        EventDate = date,
        Sport = Sport.Run,
        Priority = EventPriority.A,
        Notes = $"{name} notes"
    };

    // Direct DbContext seed — the write API's EventDtoValidator forbids past EventDate, so past and
    // foreign-athlete rows (needed to exercise the upcoming filter and the ownership 404) can only be
    // set up below the endpoint. Shares the factory's InMemory store with the HTTP request pipeline.
    private static async Task<Guid> SeedEventDirectAsync(
        BrykWebApplicationFactory factory, Guid athleteId, string name, DateOnly date)
    {
        var id = Guid.NewGuid();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.Add(new Event
        {
            Id = id,
            AthleteId = athleteId,
            Name = name,
            EventDate = date,
            Sport = Sport.Run,
            Priority = EventPriority.A,
            Notes = $"{name} notes"
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task GetAll_ReturnsEventsOrderedByDateAscending_WithNotes()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Late Race", Today.AddDays(20)));
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Early Race", Today.AddDays(10)));

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events", JsonOptions);

        events.Should().NotBeNull();
        events!.Select(e => e.Name).Should().Equal("Early Race", "Late Race");
        events![0].Notes.Should().Be("Early Race notes");
    }

    [Fact]
    public async Task GetAll_UpcomingTrue_ExcludesPast_IncludesTodayAndFuture()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        await SeedEventDirectAsync(factory, BrykWebApplicationFactory.TestAthleteId, "Past Race", Today.AddDays(-5));
        await SeedEventDirectAsync(factory, BrykWebApplicationFactory.TestAthleteId, "Today Race", Today);
        await SeedEventDirectAsync(factory, BrykWebApplicationFactory.TestAthleteId, "Future Race", Today.AddDays(5));

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events?upcoming=true", JsonOptions);

        events.Should().NotBeNull();
        events!.Select(e => e.Name).Should().Equal("Today Race", "Future Race");
    }

    [Fact]
    public async Task GetAll_LinkedPlan_AppearsInLinkedPlans_UnlinkedEventHasEmptyList()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Linked Race", Today.AddDays(30)));
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);
        await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Unlinked Race", Today.AddDays(40)));

        var planRequest = new TrainingPlanRequest
        {
            Name = "Race Plan",
            Methodology = MethodologyChoice.Polarized,
            StartDate = Today,
            EndDate = Today.AddDays(30),
            EventId = createdEvent!.Id
        };
        var planResponse = await client.PostAsJsonAsync("/api/v1/trainingplans", planRequest);
        planResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var events = await client.GetFromJsonAsync<List<EventListItemResponse>>("/api/v1/events", JsonOptions);

        var linked = events!.Single(e => e.Name == "Linked Race");
        linked.LinkedPlans.Should().ContainSingle();
        linked.LinkedPlans[0].Name.Should().Be("Race Plan");

        var unlinked = events!.Single(e => e.Name == "Unlinked Race");
        unlinked.LinkedPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ReturnsEventWithLinkedPlans()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var eventResponse = await client.PostAsJsonAsync("/api/v1/events", MakeEvent("Solo Race", Today.AddDays(10)));
        var createdEvent = await eventResponse.Content.ReadFromJsonAsync<EventResponse>(JsonOptions);

        var response = await client.GetAsync($"/api/v1/events/{createdEvent!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<EventListItemResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdEvent.Id);
        body.LinkedPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/events/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_AnotherAthletesEvent_Returns404()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        // Seed an event owned by a different athlete directly into the same store, then read it as the
        // test athlete — exercises the service's ownership check (entity.AthleteId != current) → null → 404.
        var foreignEventId = await SeedEventDirectAsync(factory, Guid.NewGuid(), "Owner's Race", Today.AddDays(10));

        var response = await client.GetAsync($"/api/v1/events/{foreignEventId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_FreshAthlete_ReturnsEmptyArray()
    {
        await using var factory = new BrykWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<EventListItemResponse>>(JsonOptions);
        events.Should().NotBeNull().And.BeEmpty();
    }
}
