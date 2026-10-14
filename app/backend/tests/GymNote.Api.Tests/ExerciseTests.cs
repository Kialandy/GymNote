using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymNote.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GymNote.Api.Tests;

public class ExerciseTests(GymNoteApiFactory factory) : IClassFixture<GymNoteApiFactory>
{
    private static object NewExercise(string name = "Squat") =>
        new { name, suggestedSets = 4, suggestedRepsPerSet = 8, suggestedRestSeconds = 90 };

    [Fact]
    public async Task Endpoints_require_authentication()
    {
        var client = factory.CreateClient();

        var list = await client.GetAsync("/api/exercises");
        var create = await client.PostAsJsonAsync("/api/exercises", NewExercise());
        var delete = await client.DeleteAsync($"/api/exercises/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/exercises");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_and_exercise_appears_in_list()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/exercises", NewExercise("Deadlift"));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ExerciseResponse>();
        Assert.Equal("Deadlift", created!.Name);
        Assert.Equal(4, created.SuggestedSets);
        Assert.Equal(8, created.SuggestedRepsPerSet);
        Assert.Equal(90, created.SuggestedRestSeconds);

        var list = await client.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises");
        Assert.Equal([created], list);
    }

    [Fact]
    public async Task List_is_empty_for_new_user()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var list = await client.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises");

        Assert.Empty(list!);
    }

    [Theory]
    [InlineData("", 3, 10, 60, "name")]
    [InlineData("This name is way too long xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 3, 10, 60, "name")]
    [InlineData("Row", 0, 10, 60, "suggestedSets")]
    [InlineData("Row", 21, 10, 60, "suggestedSets")]
    [InlineData("Row", 3, 0, 60, "suggestedRepsPerSet")]
    [InlineData("Row", 3, 101, 60, "suggestedRepsPerSet")]
    [InlineData("Row", 3, 10, -1, "suggestedRestSeconds")]
    [InlineData("Row", 3, 10, 601, "suggestedRestSeconds")]
    public async Task Create_with_invalid_data_returns_validation_problem(
        string name, int sets, int reps, int rest, string field)
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/exercises",
            new { name, suggestedSets = sets, suggestedRepsPerSet = reps, suggestedRestSeconds = rest });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Create_with_missing_fields_returns_validation_problem()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/exercises", new { name = "Row" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("suggestedSets", problem!.Errors.Keys);
        Assert.Contains("suggestedRepsPerSet", problem.Errors.Keys);
        Assert.Contains("suggestedRestSeconds", problem.Errors.Keys);
    }

    [Fact]
    public async Task Delete_removes_exercise()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/exercises", NewExercise()))
            .Content.ReadFromJsonAsync<ExerciseResponse>();

        var delete = await client.DeleteAsync($"/api/exercises/{created!.Id}");
        var deleteAgain = await client.DeleteAsync($"/api/exercises/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises"))!);
    }

    [Fact]
    public async Task Users_can_only_see_and_delete_their_own_exercises()
    {
        var alice = await factory.CreateAuthenticatedClientAsync();
        var bob = await factory.CreateAuthenticatedClientAsync();
        var aliceExercise = await (await alice.PostAsJsonAsync("/api/exercises", NewExercise("Alice Squat")))
            .Content.ReadFromJsonAsync<ExerciseResponse>();
        await bob.PostAsJsonAsync("/api/exercises", NewExercise("Bob Press"));

        var bobList = await bob.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises");
        var bobDelete = await bob.DeleteAsync($"/api/exercises/{aliceExercise!.Id}");
        var aliceList = await alice.GetFromJsonAsync<List<ExerciseResponse>>("/api/exercises");

        Assert.Equal(["Bob Press"], bobList!.Select(e => e.Name));
        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);
        Assert.Equal(["Alice Squat"], aliceList!.Select(e => e.Name));
    }
}
