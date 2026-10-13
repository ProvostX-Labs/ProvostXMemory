using ProvostXMemory.Abstractions;
using ProvostXMemory.Consolidation;

using Xunit;

namespace ProvostXMemory.ConsolidationTests;

/// <summary>ConsolidationReportMeta: report attached to / read from returned records.</summary>
public sealed class ConsolidationReportTests
{
    [Fact]
    public void Apply_attaches_report_to_every_record_without_mutating_store_copies()
    {
        var record = TestRecords.Create("a", "k", "content");
        var report = new ConsolidationReport
        {
            RecordsConsidered = 3,
            ExpiredDeleted = 1,
            DuplicatesDeleted = 1,
            SummariesEmitted = 1,
            RecordsRemaining = 2,
        };

        var applied = ConsolidationReportMeta.Apply(new[] { record }, report);

        Assert.Single(applied);
        var read = ConsolidationReportMeta.TryGet(applied[0]);
        Assert.NotNull(read);
        Assert.Equal(3, read!.RecordsConsidered);
        Assert.Equal(1, read.ExpiredDeleted);
        Assert.Equal(1, read.DuplicatesDeleted);
        Assert.Equal(1, read.SummariesEmitted);
        Assert.Equal(2, read.RecordsRemaining);
        // Original record is untouched (records are immutable; Apply rebuilds copies).
        Assert.False(record.Meta.ContainsKey(ConsolidationReportMeta.MetaKey));
    }

    [Fact]
    public void TryGet_returns_null_for_record_without_report()
    {
        var record = TestRecords.Create("a", "k", "content");
        Assert.Null(ConsolidationReportMeta.TryGet(record));
    }

    [Fact]
    public async Task End_to_end_pass_reports_expected_counts()
    {
        var store = new InMemoryMemoryStore();
        var embedder = new HashFakeEmbedder();
        var consolidator = new MemoryConsolidator(
            store,
            new ThresholdDedupDetector(embedder),
            embedder,
            new ClusterSummarizer());

        await store.AddAsync(TestRecords.Create("expired", "k0", "gone text", expiresAt: DateTimeOffset.UtcNow.AddHours(-1)));
        await store.AddAsync(TestRecords.Create("dupe-a", "k1", "alpha bravo charlie delta"));
        await store.AddAsync(TestRecords.Create("dupe-b", "k1", "alpha bravo charlie delta"));
        await store.AddAsync(TestRecords.Create("unique", "k2", "the quick brown fox jumps"));

        var result = await consolidator.ConsolidateAsync("Session", "s1", new ConsolidationOptions());

        var report = ConsolidationReportMeta.TryGet(result[0]);
        Assert.NotNull(report);
        Assert.Equal(4, report!.RecordsConsidered);
        Assert.Equal(1, report.ExpiredDeleted);
        Assert.Equal(1, report.DuplicatesDeleted);
        Assert.Equal(0, report.SummariesEmitted);
        Assert.Equal(2, report.RecordsRemaining);
    }
}