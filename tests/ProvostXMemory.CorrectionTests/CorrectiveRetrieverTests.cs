using System;
using System.Linq;
using System.Threading.Tasks;

using ProvostXMemory.Abstractions;
using ProvostXMemory.Correction;

using Xunit;

namespace ProvostXMemory.CorrectionTests;

/// <summary>
/// Grader-mock driven paths of <see cref="CorrectiveRetriever"/>: pass-through, drop,
/// below-floor, rewrite retry (empty first pass), Uncertain retry, fetch-hook variants,
/// diagnostics, retry-once semantics.
/// </summary>
public sealed class CorrectiveRetrieverTests
{
    // ---- pass-through ----------------------------------------------------------------

    [Fact]
    public async Task All_relevant_candidates_pass_through_unchanged()
    {
        var first = Fixtures.Result(
            Fixtures.Rec("A", 0.9),
            Fixtures.Rec("B", 0.8));
        var hybrid = new StubHybridRetriever(first);
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("unused"));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query text"));

        Assert.Equal(new[] { "A", "B" }, result.Records.Select(r => r.Record.Id));
        Assert.Equal(0.9, result.Records[0].Score, 9);
        Assert.Equal(0.8, result.Records[1].Score, 9);
    }

    [Fact]
    public async Task Irrelevant_candidates_are_dropped()
    {
        var first = Fixtures.Result(
            Fixtures.Rec("A", 0.9),
            Fixtures.Rec("BAD", 0.7));
        var hybrid = new StubHybridRetriever(first);
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
            {
                ["BAD"] = RetrievalGrade.Irrelevant,
            }),
            new RecordingQueryRewriter("unused"));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query text"));

        Assert.Equal(new[] { "A" }, result.Records.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Below_floor_candidates_are_dropped()
    {
        var first = Fixtures.Result(
            Fixtures.Rec("A", 0.9),
            Fixtures.Rec("LOW", 0.2));
        var hybrid = new StubHybridRetriever(first);
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("unused"),
            options: new CorrectionOptions(RelevanceFloor: 0.5));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query text"));

        Assert.Equal(new[] { "A" }, result.Records.Select(r => r.Record.Id));
    }

    // ---- rewrite retry ----------------------------------------------------------------

    [Fact]
    public async Task All_irrelevant_first_pass_triggers_rewrite_retry()
    {
        var firstPass = Fixtures.Result(Fixtures.Rec("BAD", 0.7));
        var retryPass = Fixtures.Result(Fixtures.Rec("GOOD", 0.85));
        var hybrid = new StubHybridRetriever(firstPass, retryPass);
        var grader = new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
        {
            ["BAD"] = RetrievalGrade.Irrelevant,
            ["GOOD"] = RetrievalGrade.Relevant,
        });
        var rewriter = new RecordingQueryRewriter("good keywords");
        var retriever = new CorrectiveRetriever(hybrid, grader, rewriter);

        var result = await retriever.RetrieveAsync(Fixtures.Query("original query"));

        Assert.True(rewriter.Called);
        Assert.Equal("original query", rewriter.LastInput);
        // First pass replaced by retry pass; retry candidates graded against the original query.
        Assert.Equal(new[] { "GOOD" }, result.Records.Select(r => r.Record.Id));
        Assert.Equal(new[] { "original query", "good keywords" }, hybrid.QueryTexts);
        Assert.Equal(2, grader.Calls);
    }

    [Fact]
    public async Task Uncertain_grade_triggers_rewrite_retry()
    {
        var firstPass = Fixtures.Result(Fixtures.Rec("UNSURE", 0.6));
        var retryPass = Fixtures.Result(Fixtures.Rec("CLEAR", 0.8));
        var hybrid = new StubHybridRetriever(firstPass, retryPass);
        var grader = new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
        {
            ["UNSURE"] = RetrievalGrade.Uncertain,
            ["CLEAR"] = RetrievalGrade.Relevant,
        });
        var rewriter = new RecordingQueryRewriter("clarified");
        var retriever = new CorrectiveRetriever(hybrid, grader, rewriter);

        var result = await retriever.RetrieveAsync(Fixtures.Query("vague query"));

        Assert.True(rewriter.Called);
        Assert.Equal(new[] { "CLEAR" }, result.Records.Select(r => r.Record.Id));
    }

    [Fact]
    public async Task Good_first_pass_does_not_rewrite()
    {
        var first = Fixtures.Result(Fixtures.Rec("A", 0.9));
        var hybrid = new StubHybridRetriever(first);
        var rewriter = new RecordingQueryRewriter("unused");
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            rewriter);

        await retriever.RetrieveAsync(Fixtures.Query("query"));

        Assert.False(rewriter.Called);
        Assert.Single(hybrid.QueryTexts);
    }

    [Fact]
    public async Task Retry_runs_only_once_even_when_second_pass_still_empty()
    {
        var hybrid = new StubHybridRetriever(
            Fixtures.Result(Fixtures.Rec("BAD", 0.7)),
            Fixtures.Result(Fixtures.Rec("ALSO_BAD", 0.6)));
        var grader = new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
        {
            ["BAD"] = RetrievalGrade.Irrelevant,
            ["ALSO_BAD"] = RetrievalGrade.Irrelevant,
        });
        var rewriter = new RecordingQueryRewriter("rewritten once");
        var fetcher = new StubExternalFetcher("a snippet");
        var retriever = new CorrectiveRetriever(hybrid, grader, rewriter, fetcher);

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        // Retry happened exactly once (two retrievals), then the fetch hook ran once.
        Assert.Equal(2, hybrid.QueryTexts.Count);
        Assert.Equal(1, fetcher.Calls);
        Assert.Empty(result.Records);
        Assert.Contains(CorrectiveRetriever.StageNames.Rewritten, result.Diagnostics.Select(d => d.Stage));
        Assert.Contains(CorrectiveRetriever.StageNames.GradedRetry, result.Diagnostics.Select(d => d.Stage));
        Assert.Contains(CorrectiveRetriever.StageNames.Fetched, result.Diagnostics.Select(d => d.Stage));
    }

    // ---- external fetch hook -----------------------------------------------------------

    [Fact]
    public async Task Null_fetcher_is_skipped_when_nothing_survives()
    {
        var hybrid = new StubHybridRetriever(Fixtures.Result(Fixtures.Rec("BAD", 0.7)));
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
            {
                ["BAD"] = RetrievalGrade.Irrelevant,
            }),
            new RecordingQueryRewriter("rewritten"),
            fetcher: null);

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        Assert.Empty(result.Records);
        Assert.DoesNotContain(
            result.Diagnostics,
            d => string.Equals(d.Stage, CorrectiveRetriever.StageNames.Fetched, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fetch_hook_runs_once_when_nothing_survives()
    {
        var hybrid = new StubHybridRetriever(
            Fixtures.Result(Fixtures.Rec("BAD", 0.7)),
            Fixtures.Result());
        var fetcher = new StubExternalFetcher("snippet one", "snippet two");
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
            {
                ["BAD"] = RetrievalGrade.Irrelevant,
            }),
            new RecordingQueryRewriter("rewritten"),
            fetcher);

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        Assert.Equal(1, fetcher.Calls);
        Assert.Empty(result.Records);
        var fetched = Assert.Single(result.Diagnostics, d => d.Stage == CorrectiveRetriever.StageNames.Fetched);
        Assert.Equal(2, fetched.Scores.Count);
    }

    [Fact]
    public async Task Fetch_hook_skipped_when_results_survive()
    {
        var hybrid = new StubHybridRetriever(Fixtures.Result(Fixtures.Rec("A", 0.9)));
        var fetcher = new StubExternalFetcher("snippet");
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("unused"),
            fetcher);

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        Assert.Equal(0, fetcher.Calls);
        Assert.Single(result.Records);
        Assert.DoesNotContain(
            result.Diagnostics,
            d => string.Equals(d.Stage, CorrectiveRetriever.StageNames.Fetched, StringComparison.Ordinal));
    }

    [Fact]
    public async Task UseFetcherOnEmpty_false_disables_the_hook()
    {
        var hybrid = new StubHybridRetriever(Fixtures.Result());
        var fetcher = new StubExternalFetcher("snippet");
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("rewritten"),
            fetcher,
            new CorrectionOptions(UseFetcherOnEmpty: false));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        Assert.Equal(0, fetcher.Calls);
        Assert.Empty(result.Records);
    }

    // ---- diagnostics -------------------------------------------------------------------

    [Fact]
    public async Task Diagnostics_report_pass_through_stages_only()
    {
        var hybrid = new StubHybridRetriever(Fixtures.Result(Fixtures.Rec("A", 0.9)));
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("unused"));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        var stages = result.Diagnostics.Select(d => d.Stage).ToList();
        Assert.Equal(
            new[]
            {
                CorrectiveRetriever.StageNames.Hybrid,
                CorrectiveRetriever.StageNames.Graded,
            },
            stages);
        Assert.All(result.Diagnostics, d => Assert.True(d.Duration >= TimeSpan.Zero));
    }

    [Fact]
    public async Task Diagnostics_report_rewrite_and_retry_stages()
    {
        var hybrid = new StubHybridRetriever(
            Fixtures.Result(Fixtures.Rec("BAD", 0.7)),
            Fixtures.Result(Fixtures.Rec("GOOD", 0.8)));
        var retriever = new CorrectiveRetriever(
            hybrid,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>
            {
                ["BAD"] = RetrievalGrade.Irrelevant,
                ["GOOD"] = RetrievalGrade.Relevant,
            }),
            new RecordingQueryRewriter("rewritten"));

        var result = await retriever.RetrieveAsync(Fixtures.Query("query"));

        var stages = result.Diagnostics.Select(d => d.Stage).ToList();
        Assert.Equal(
            new[]
            {
                CorrectiveRetriever.StageNames.Hybrid,
                CorrectiveRetriever.StageNames.Rewritten,
                CorrectiveRetriever.StageNames.GradedRetry,
            },
            stages);
    }

    // ---- guards ------------------------------------------------------------------------

    [Fact]
    public async Task Null_query_throws()
    {
        var retriever = new CorrectiveRetriever(
            new StubHybridRetriever(),
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("x"));

        await Assert.ThrowsAsync<ArgumentNullException>(() => retriever.RetrieveAsync(null!));
    }

    [Fact]
    public void Null_constructor_dependencies_throw()
    {
        Assert.Throws<ArgumentNullException>(() => new CorrectiveRetriever(
            null!,
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            new RecordingQueryRewriter("x")));
        Assert.Throws<ArgumentNullException>(() => new CorrectiveRetriever(
            new StubHybridRetriever(),
            null!,
            new RecordingQueryRewriter("x")));
        Assert.Throws<ArgumentNullException>(() => new CorrectiveRetriever(
            new StubHybridRetriever(),
            new StubRetrievalGrader(new Dictionary<string, RetrievalGrade>()),
            null!));
    }
}