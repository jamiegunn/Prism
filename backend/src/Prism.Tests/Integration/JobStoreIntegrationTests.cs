using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Jobs;

namespace Prism.Tests.Integration;

[Collection("Database")]
public class JobStoreIntegrationTests
{
    private readonly DatabaseFixture _fixture;

    public JobStoreIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAndGet_PersistsJob()
    {
        await using var db = _fixture.CreateContext();
        var store = new DbJobStore(db, NullLogger<DbJobStore>.Instance);

        var job = new DurableJob
        {
            JobType = "test_job",
            TotalItems = 10,
            ParametersJson = "{\"key\":\"value\"}"
        };

        var createResult = await store.CreateAsync(job, CancellationToken.None);
        createResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetAsync(createResult.Value, CancellationToken.None);
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value.JobType.Should().Be("test_job");
        getResult.Value.Status.Should().Be(JobStatus.Queued);
        getResult.Value.TotalItems.Should().Be(10);
    }

    [Fact]
    public async Task UpdateProgress_UpdatesStatusAndProgress()
    {
        await using var db = _fixture.CreateContext();
        var store = new DbJobStore(db, NullLogger<DbJobStore>.Instance);

        var job = new DurableJob { JobType = "progress_test", TotalItems = 5 };
        var createResult = await store.CreateAsync(job, CancellationToken.None);

        var updateResult = await store.UpdateProgressAsync(
            createResult.Value, JobStatus.Running, 3, 0, null, CancellationToken.None);
        updateResult.IsSuccess.Should().BeTrue();

        var getResult = await store.GetAsync(createResult.Value, CancellationToken.None);
        getResult.Value.Status.Should().Be(JobStatus.Running);
        getResult.Value.CompletedItems.Should().Be(3);
        getResult.Value.Progress.Should().Be(60);
        getResult.Value.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteJob_SetsCompletedAt()
    {
        await using var db = _fixture.CreateContext();
        var store = new DbJobStore(db, NullLogger<DbJobStore>.Instance);

        var job = new DurableJob { JobType = "complete_test", TotalItems = 2 };
        var createResult = await store.CreateAsync(job, CancellationToken.None);

        await store.UpdateProgressAsync(
            createResult.Value, JobStatus.Complete, 2, 0, null, CancellationToken.None);

        var getResult = await store.GetAsync(createResult.Value, CancellationToken.None);
        getResult.Value.Status.Should().Be(JobStatus.Complete);
        getResult.Value.CompletedAt.Should().NotBeNull();
        getResult.Value.Progress.Should().Be(100);
    }

    [Fact]
    public async Task ListJobs_FiltersByTypeAndStatus()
    {
        await using var db = _fixture.CreateContext();
        var store = new DbJobStore(db, NullLogger<DbJobStore>.Instance);

        await store.CreateAsync(new DurableJob { JobType = "type_a" }, CancellationToken.None);
        await store.CreateAsync(new DurableJob { JobType = "type_b" }, CancellationToken.None);

        var allJobs = await store.ListAsync(null, null, CancellationToken.None);
        allJobs.Count.Should().BeGreaterThanOrEqualTo(2);

        var typeAJobs = await store.ListAsync("type_a", null, CancellationToken.None);
        typeAJobs.Should().AllSatisfy(j => j.JobType.Should().Be("type_a"));
    }
}
