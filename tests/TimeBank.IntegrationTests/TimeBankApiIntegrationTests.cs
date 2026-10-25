using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeBank.Application.DTOs.Admin;
using TimeBank.Application.DTOs.Auth;
using TimeBank.Application.DTOs.Balances;
using TimeBank.Application.DTOs.Categories;
using TimeBank.Application.DTOs.Members;
using TimeBank.Application.DTOs.Requests;
using TimeBank.Application.DTOs.Reviews;
using TimeBank.Application.DTOs.Stats;
using TimeBank.Domain.Enums;
using TimeBank.Infrastructure.Persistence;
using TimeBank.IntegrationTests.Infrastructure;

namespace TimeBank.IntegrationTests;

/// <summary>
/// Runs the API in-process against a real SQL Server database created by the real
/// migrations, seeded by the real DataSeeder, and dropped afterwards.
/// </summary>
public sealed class TimeBankApiIntegrationTests : IClassFixture<TimeBankApiFactory>
{
    private const string MemberPassword = "Test@2026!";

    private readonly TimeBankApiFactory _factory;
    private readonly TimeBankApiClient _api;

    public TimeBankApiIntegrationTests(TimeBankApiFactory factory)
    {
        _factory = factory;
        _api = new TimeBankApiClient(factory.Client);
    }

    [Fact]
    public async Task Health_Swagger_And_SeededData_AreAvailable()
    {
        var health = await _api.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Contains("Healthy", await health.Content.ReadAsStringAsync());

        var swagger = await _api.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var swaggerJson = await swagger.Content.ReadAsStringAsync();
        Assert.Contains("openapi", swaggerJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/auth/login", swaggerJson);

        var categoriesResponse = await _api.GetAsync("/api/categories");
        Assert.Equal(HttpStatusCode.OK, categoriesResponse.StatusCode);
        var categories = await TimeBankApiClient.ReadAsync<List<CategoryDto>>(categoriesResponse);
        Assert.True(categories.Count >= 10);
        Assert.Contains(categories, c => c.Name == "Home Repair" && c.Skills.Count > 0);

        var statsResponse = await _api.GetAsync("/api/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        var stats = await TimeBankApiClient.ReadAsync<StatsDto>(statsResponse);
        Assert.True(stats.Members >= 10);
        Assert.True(stats.OpenRequests >= 7);
    }

    [Fact]
    public async Task MemberDirectory_ReturnsSeededProfilesOrderedByName()
    {
        var response = await _api.GetAsync("/api/members");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var directory = await TimeBankApiClient.ReadAsync<List<MemberDirectoryDto>>(response);
        Assert.True(directory.Count >= 10);
        Assert.All(directory, member => Assert.False(string.IsNullOrWhiteSpace(member.FullName)));
        Assert.All(directory, member => Assert.NotNull(member.Badges));
        Assert.Contains(directory, member => member.Skills.Count > 0);

        var expectedOrder = directory.OrderBy(member => member.FullName).Select(member => member.Id);
        Assert.Equal(expectedOrder, directory.Select(member => member.Id));
    }

    [Fact]
    public async Task Register_WithSkills_CreatesWelcomeBonusAndJwt()
    {
        var auth = await RegisterAsync("register", skillId: 6);

        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.Equal("Member", auth.Member.Role);
        Assert.False(auth.Member.IsSuspended);

        var balance = await GetBalanceAsync(auth.Token);
        Assert.Equal(2m, balance.Balance);
        Assert.Equal(0m, balance.Locked);
        Assert.Equal(2m, balance.Available);

        var profile = await GetProfileAsync(auth.Member.Id);
        Assert.Single(profile.Skills);
        Assert.Equal(6, profile.Skills[0].SkillId);
        Assert.Equal("Intermediate", profile.Skills[0].Level);

        var duplicate = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/register", new RegisterRequest
        {
            FullName = auth.Member.FullName,
            Email = auth.Member.Email,
            Password = MemberPassword,
            District = "Nasr City",
            Skills = [new SkillSelectionRequest { SkillId = 6, Level = SkillLevel.Intermediate }]
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var wrongPassword = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/login", new LoginRequest
        {
            Email = auth.Member.Email,
            Password = "WrongPassword1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task Register_WithUnknownSkill_RollsBackAndReturns400()
    {
        var email = UniqueEmail("unknown.skill");

        var response = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/register", new RegisterRequest
        {
            FullName = "Unknown Skill",
            Email = email,
            Password = MemberPassword,
            District = "Nasr City",
            Skills = [new SkillSelectionRequest { SkillId = 999_999, Level = SkillLevel.Expert }]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("BUSINESS_RULE", await TimeBankApiClient.ReadErrorCodeAsync(response));

        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();
        Assert.False(await db.Members.AnyAsync(m => m.Email == email));
    }

    [Fact]
    public async Task FullExchangeFlow_TransfersHoursAndAwardsFirstHandOnce()
    {
        var requester = await RegisterAsync("flow.requester", skillId: 1);
        var helper = await RegisterAsync("flow.helper", skillId: 1);

        var request = await CreateRequestAsync(requester.Token, categoryId: 1, hours: 1.5m);
        Assert.Equal("Open", request.Status);

        var offerId = await SendOfferAsync(helper.Token, request.Id, hours: 1.5m);

        var duplicateOffer = await _api.SendJsonAsync(HttpMethod.Post, $"/api/requests/{request.Id}/offers",
            new SendOfferRequest { Message = "A duplicate offer.", Hours = 1m }, helper.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicateOffer.StatusCode);

        var offersAsHelper = await _api.GetAsync($"/api/requests/{request.Id}/offers", helper.Token);
        Assert.Equal(HttpStatusCode.Forbidden, offersAsHelper.StatusCode);

        var accepted = await AcceptOfferAsync(requester.Token, offerId);
        Assert.Equal("InProgress", accepted.Status);
        Assert.Equal(offerId, accepted.AcceptedOfferId);

        var duplicateAccept = await _api.SendAsync(
            HttpMethod.Post, $"/api/offers/{offerId}/accept", requester.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicateAccept.StatusCode);

        var lockedBalance = await GetBalanceAsync(requester.Token);
        Assert.Equal(2m, lockedBalance.Balance);
        Assert.Equal(1.5m, lockedBalance.Locked);
        Assert.Equal(0.5m, lockedBalance.Available);

        var completed = await CompleteAsync(requester.Token, request.Id, actualHours: 1.5m);
        Assert.Equal("Completed", completed.Status);
        Assert.Equal(1.5m, completed.ActualHours);

        var duplicateComplete = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{request.Id}/complete",
            new CompleteHelpRequestRequest { ActualHours = 1.5m },
            requester.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicateComplete.StatusCode);

        var requesterBalance = await GetBalanceAsync(requester.Token);
        var helperBalance = await GetBalanceAsync(helper.Token);
        Assert.Equal(0.5m, requesterBalance.Balance);
        Assert.Equal(0m, requesterBalance.Locked);
        Assert.Equal(3.5m, helperBalance.Balance);

        var requesterReview = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{request.Id}/reviews",
            new CreateReviewRequest { Rating = 5, Comment = "Excellent work." },
            requester.Token);
        Assert.Equal(HttpStatusCode.Created, requesterReview.StatusCode);

        var duplicateReview = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{request.Id}/reviews",
            new CreateReviewRequest { Rating = 4, Comment = "A duplicate review." },
            requester.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicateReview.StatusCode);

        var helperReview = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{request.Id}/reviews",
            new CreateReviewRequest { Rating = 5, Comment = "Clear instructions." },
            helper.Token);
        Assert.Equal(HttpStatusCode.Created, helperReview.StatusCode);

        var helperProfile = await GetProfileAsync(helper.Member.Id);
        Assert.NotNull(helperProfile.AverageRating);
        Assert.Equal(5d, helperProfile.AverageRating!.Value);
        Assert.Single(helperProfile.Badges, b => b.Code == "FIRST_HAND");

        var statement = await GetStatementAsync(
            requester.Token,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        Assert.True(statement.Entries.Count >= 2);
        Assert.Equal(0m, statement.OpeningBalance);
        Assert.Equal(0.5m, statement.ClosingBalance);
        Assert.Equal(0.5m, statement.Entries[^1].RunningBalance);
        Assert.Contains(statement.Entries, e => e.Type == "WelcomeBonus");
        Assert.Contains(statement.Entries, e => e.Type == "ExchangeDebit");
    }

    [Fact]
    public async Task Member_CannotCallModeratorEndpoints_AndSuspensionInvalidatesOldJwt()
    {
        var member = await RegisterAsync("suspend.member", skillId: 1);
        var moderator = await LoginModeratorAsync();

        var forbidden = await _api.GetAsync("/api/admin/members", member.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var suspend = await _api.SendJsonAsync(
            HttpMethod.Put,
            $"/api/admin/members/{member.Member.Id}/suspend",
            new { reason = "Integration test suspension" },
            moderator.Token);
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

        var loginWhileSuspended = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/login", new LoginRequest
        {
            Email = member.Member.Email,
            Password = MemberPassword
        });
        Assert.Equal(HttpStatusCode.Forbidden, loginWhileSuspended.StatusCode);

        var oldTokenRequest = await _api.SendJsonAsync(
            HttpMethod.Post,
            "/api/requests",
            CreateRequestPayload(categoryId: 1, hours: 1m),
            member.Token);
        Assert.Equal(HttpStatusCode.Forbidden, oldTokenRequest.StatusCode);

        var oldTokenOffer = await _api.SendJsonAsync(
            HttpMethod.Post,
            "/api/requests/1/offers",
            new SendOfferRequest { Message = "Old token offer.", Hours = 1m },
            member.Token);
        Assert.Equal(HttpStatusCode.Forbidden, oldTokenOffer.StatusCode);

        var reinstate = await _api.SendAsync(
            HttpMethod.Put,
            $"/api/admin/members/{member.Member.Id}/reinstate",
            moderator.Token);
        Assert.Equal(HttpStatusCode.NoContent, reinstate.StatusCode);

        var loginAfterReinstate = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/login", new LoginRequest
        {
            Email = member.Member.Email,
            Password = MemberPassword
        });
        Assert.Equal(HttpStatusCode.OK, loginAfterReinstate.StatusCode);

        var auditResponse = await _api.GetAsync("/api/admin/audit-logs?take=100", moderator.Token);
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var logs = await TimeBankApiClient.ReadAsync<List<AuditLogDto>>(auditResponse);

        Assert.Contains(logs, log =>
            log.Action == "SuspendMember" &&
            log.Actor == moderator.Member.Id.ToString() &&
            log.Target == $"Member:{member.Member.Id}");
        Assert.Contains(logs, log =>
            log.Action == "ReinstateMember" &&
            log.Actor == moderator.Member.Id.ToString());
    }

    [Fact]
    public async Task Moderator_CategoryCrud_IsAudited()
    {
        var moderator = await LoginModeratorAsync();
        var name = $"Integration Category {Guid.NewGuid():N}";

        var create = await _api.SendJsonAsync(HttpMethod.Post, "/api/categories", new CategoryCreateRequest
        {
            Name = name,
            Description = "Created by an integration test."
        }, moderator.Token);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var category = await TimeBankApiClient.ReadAsync<CategoryDto>(create);
        Assert.Equal(name, category.Name);

        var update = await _api.SendJsonAsync(HttpMethod.Put, $"/api/categories/{category.Id}",
            new CategoryUpdateRequest
            {
                Name = $"{name} Updated",
                Description = "Updated by an integration test."
            }, moderator.Token);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var delete = await _api.SendAsync(HttpMethod.Delete, $"/api/categories/{category.Id}", moderator.Token);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var auditResponse = await _api.GetAsync("/api/admin/audit-logs?take=100", moderator.Token);
        var logs = await TimeBankApiClient.ReadAsync<List<AuditLogDto>>(auditResponse);

        Assert.Contains(logs, log => log.Action == "CreateCategory" && log.Actor == moderator.Member.Id.ToString());
        Assert.Contains(logs, log => log.Action == "UpdateCategory" && log.Actor == moderator.Member.Id.ToString());
        Assert.Contains(logs, log => log.Action == "DeleteCategory" && log.Actor == moderator.Member.Id.ToString());
    }

    [Fact]
    public async Task AcceptOffer_WithInsufficientAvailableBalance_Returns409()
    {
        var requester = await RegisterAsync("insufficient.requester", skillId: 1);
        var firstHelper = await RegisterAsync("insufficient.helper1", skillId: 1);
        var secondHelper = await RegisterAsync("insufficient.helper2", skillId: 1);

        var firstRequest = await CreateRequestAsync(requester.Token, 1, 1.5m);
        var firstOffer = await SendOfferAsync(firstHelper.Token, firstRequest.Id, 1.5m);
        await AcceptOfferAsync(requester.Token, firstOffer);

        var secondRequest = await CreateRequestAsync(requester.Token, 1, 1.5m);
        var secondOffer = await SendOfferAsync(secondHelper.Token, secondRequest.Id, 1.5m);

        var response = await _api.SendAsync(
            HttpMethod.Post, $"/api/offers/{secondOffer}/accept", requester.Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INSUFFICIENT_HOURS", await TimeBankApiClient.ReadErrorCodeAsync(response));

        var balance = await GetBalanceAsync(requester.Token);
        Assert.Equal(2m, balance.Balance);
        Assert.Equal(1.5m, balance.Locked);
        Assert.Equal(0.5m, balance.Available);
    }

    [Fact]
    public async Task ConcurrentAccepts_SerializeTheRequesterBalance()
    {
        var requester = await RegisterAsync("concurrent.requester", skillId: 1);
        var helperOne = await RegisterAsync("concurrent.helper1", skillId: 1);
        var helperTwo = await RegisterAsync("concurrent.helper2", skillId: 1);

        var requestOne = await CreateRequestAsync(requester.Token, 1, 1.5m);
        var requestTwo = await CreateRequestAsync(requester.Token, 1, 1.5m);

        var offerOne = await SendOfferAsync(helperOne.Token, requestOne.Id, 1.5m);
        var offerTwo = await SendOfferAsync(helperTwo.Token, requestTwo.Id, 1.5m);

        var first = _api.SendAsync(HttpMethod.Post, $"/api/offers/{offerOne}/accept", requester.Token);
        var second = _api.SendAsync(HttpMethod.Post, $"/api/offers/{offerTwo}/accept", requester.Token);
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var balance = await GetBalanceAsync(requester.Token);
        Assert.Equal(2m, balance.Balance);
        Assert.Equal(1.5m, balance.Locked);
        Assert.Equal(0.5m, balance.Available);
    }

    [Fact]
    public async Task Complete_WhenLedgerInsertFails_RollsBackStatusAndBothEntries()
    {
        var requester = await RegisterAsync("rollback.requester", skillId: 1);
        var helper = await RegisterAsync("rollback.helper", skillId: 1);

        var request = await CreateRequestAsync(requester.Token, 1, 1.5m);
        var offerId = await SendOfferAsync(helper.Token, request.Id, 1.5m);
        await AcceptOfferAsync(requester.Token, offerId);

        // A temporary CHECK constraint rejects the credit row (Type = 2) for this
        // request. This forces the ledger insert to fail after the request status
        // update has already been staged, proving that both are rolled back together.
        var constraintName = $"CK_Integration_FailCredit_{Guid.NewGuid():N}";

        try
        {
            using (var scope = _factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();
                // The constraint name is generated from a GUID and the request id is an
                // int from the test database, so no user-controlled text is interpolated.
#pragma warning disable EF1002
                await db.Database.ExecuteSqlRawAsync($"""
                    ALTER TABLE [dbo].[LedgerEntries] WITH CHECK
                    ADD CONSTRAINT [{constraintName}]
                    CHECK ([Type] <> 2 OR [ReferenceId] <> {request.Id});
                    """);
#pragma warning restore EF1002
            }

            var failed = await _api.SendJsonAsync(
                HttpMethod.Post,
                $"/api/requests/{request.Id}/complete",
                new CompleteHelpRequestRequest { ActualHours = 1.5m },
                requester.Token);

            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode);
            Assert.Equal("CONSTRAINT_VIOLATION", await TimeBankApiClient.ReadErrorCodeAsync(failed));

            using (var scope = _factory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();

                var status = await db.HelpRequests.AsNoTracking()
                    .Where(r => r.Id == request.Id)
                    .Select(r => r.Status)
                    .SingleAsync();
                Assert.Equal(RequestStatus.InProgress, status);

                Assert.Equal(0, await db.LedgerEntries.AsNoTracking()
                    .CountAsync(l => l.ReferenceId == request.Id));

                var requesterBalance = await db.LedgerEntries.AsNoTracking()
                    .Where(l => l.MemberId == requester.Member.Id)
                    .SumAsync(l => (decimal?)l.Hours) ?? 0m;
                Assert.Equal(2m, requesterBalance);
            }
        }
        finally
        {
            using var scope = _factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE [dbo].[LedgerEntries] DROP CONSTRAINT IF EXISTS [{constraintName}];");
#pragma warning restore EF1002
        }

        // After removing the forced failure the same operation succeeds exactly once.
        var completed = await CompleteAsync(requester.Token, request.Id, 1.5m);
        Assert.Equal("Completed", completed.Status);

        using (var scope = _factory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();
            Assert.Equal(2, await db.LedgerEntries.AsNoTracking()
                .CountAsync(l => l.ReferenceId == request.Id));
        }

        var finalBalance = await GetBalanceAsync(requester.Token);
        Assert.Equal(0.5m, finalBalance.Balance);
    }

    [Fact]
    public async Task Database_HasRequiredTablesConstraintsIndexesAndProcedure()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TimeBankDbContext>();

        var tables = await QueryStringsAsync(
            db,
            "SELECT name FROM sys.tables ORDER BY name;");
        foreach (var table in new[]
                 {
                     "Members", "Categories", "Skills", "MemberSkills", "HelpRequests",
                     "Offers", "LedgerEntries", "Reviews", "Badges", "MemberBadges", "AuditLogs"
                 })
        {
            Assert.Contains(table, tables);
        }

        var checks = await QueryStringsAsync(
            db,
            "SELECT name FROM sys.check_constraints;");
        Assert.Contains("CK_LedgerEntries_Hours_NotZero", checks);
        Assert.Contains("CK_LedgerEntries_Hours_HalfStep", checks);
        Assert.Contains("CK_HelpRequests_EstimatedHours", checks);
        Assert.Contains("CK_Offers_Hours", checks);
        Assert.Contains("CK_Reviews_Rating", checks);

        var indexes = await QueryStringsAsync(
            db,
            "SELECT name FROM sys.indexes WHERE name IS NOT NULL;");
        Assert.Contains("IX_Members_Email", indexes);
        Assert.Contains("IX_Offers_HelpRequestId_HelperId", indexes);
        Assert.Contains("IX_Reviews_HelpRequestId_ReviewerId", indexes);
        Assert.Contains("IX_HelpRequests_Status_CategoryId_District", indexes);
        Assert.Contains("IX_LedgerEntries_MemberId_CreatedAt", indexes);

        var procedures = await QueryStringsAsync(
            db,
            "SELECT name FROM sys.procedures;");
        Assert.Contains("sp_GetMemberStatement", procedures);
    }

    private async Task<AuthResponse> RegisterAsync(string prefix, int skillId)
    {
        var email = UniqueEmail(prefix);

        var response = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/register", new RegisterRequest
        {
            FullName = $"Integration {prefix}",
            Email = email,
            Password = MemberPassword,
            District = "Nasr City",
            Bio = "Created by the integration test suite.",
            Skills = [new SkillSelectionRequest { SkillId = skillId, Level = SkillLevel.Intermediate }]
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await TimeBankApiClient.ReadAsync<AuthResponse>(response);
        Assert.Equal(email, auth.Member.Email);
        return auth;
    }

    private async Task<AuthResponse> LoginModeratorAsync()
    {
        var response = await _api.SendJsonAsync(HttpMethod.Post, "/api/auth/login", new LoginRequest
        {
            Email = "mona.adel@sa3a.test",
            Password = "Sa3a@2026"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await TimeBankApiClient.ReadAsync<AuthResponse>(response);
        Assert.Equal("Moderator", auth.Member.Role);
        return auth;
    }

    private async Task<RequestSummaryDto> CreateRequestAsync(
        string token, int categoryId, decimal hours)
    {
        var response = await _api.SendJsonAsync(
            HttpMethod.Post, "/api/requests", CreateRequestPayload(categoryId, hours), token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<RequestSummaryDto>(response);
    }

    private static CreateHelpRequestRequest CreateRequestPayload(int categoryId, decimal hours) => new()
    {
        Title = $"Integration request {Guid.NewGuid():N}"[..40],
        Description = "A request created by the integration test suite.",
        CategoryId = categoryId,
        District = "Nasr City",
        EstimatedHours = hours,
        NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
    };

    private async Task<int> SendOfferAsync(string token, int requestId, decimal hours)
    {
        var response = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{requestId}/offers",
            new SendOfferRequest { Message = "I can help with this request.", Hours = hours },
            token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private async Task<RequestStatusDto> AcceptOfferAsync(string token, int offerId)
    {
        var response = await _api.SendAsync(HttpMethod.Post, $"/api/offers/{offerId}/accept", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<RequestStatusDto>(response);
    }

    private async Task<RequestStatusDto> CompleteAsync(string token, int requestId, decimal actualHours)
    {
        var response = await _api.SendJsonAsync(
            HttpMethod.Post,
            $"/api/requests/{requestId}/complete",
            new CompleteHelpRequestRequest { ActualHours = actualHours },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<RequestStatusDto>(response);
    }

    private async Task<BalanceDto> GetBalanceAsync(string token)
    {
        var response = await _api.GetAsync("/api/me/balance", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<BalanceDto>(response);
    }

    private async Task<StatementDto> GetStatementAsync(string token, DateOnly from, DateOnly to)
    {
        var response = await _api.GetAsync(
            $"/api/me/statement?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<StatementDto>(response);
    }

    private async Task<MemberProfileDto> GetProfileAsync(int memberId)
    {
        var response = await _api.GetAsync($"/api/members/{memberId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await TimeBankApiClient.ReadAsync<MemberProfileDto>(response);
    }

    private static string UniqueEmail(string prefix) =>
        $"{prefix}.{Guid.NewGuid():N}@integration.test";

    private static async Task<List<string>> QueryStringsAsync(TimeBankDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();

            var values = new List<string>();
            while (await reader.ReadAsync())
                values.Add(reader.GetString(0));

            return values;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
