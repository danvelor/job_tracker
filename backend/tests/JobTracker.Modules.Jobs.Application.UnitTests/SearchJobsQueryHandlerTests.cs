using FluentAssertions;
using JobTracker.Modules.Jobs.Application.Jobs.SearchJobs;
using JobTracker.Modules.Jobs.Domain;
using Moq;

namespace JobTracker.Modules.Jobs.Application.UnitTests;

public sealed class SearchJobsQueryHandlerTests
{
    private readonly Mock<IJobRepository> _repository = new();

    private SearchJobsQueryHandler Handler() => new(_repository.Object);

    private static JobSearchResult ARow(Guid id, string title = "Roof repair") =>
        new(id, title, JobStatus.Scheduled, new DateOnly(2026, 3, 14),
            Guid.NewGuid(), "J. Ortiz", "12 Elm St", "Springfield", "IL", 0);

    private static SearchJobsQuery AQuery(int limit = 2) =>
        new(Guid.NewGuid(), null, null, null, null, null, JobSortField.ScheduledDate, null, limit);

    private void Returns(params JobSearchResult[] rows) =>
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    [Fact]
    public async Task It_asks_the_repository_for_one_row_more_than_the_page()
    {
        JobSearchCriteria? captured = null;
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<JobSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync([]);

        await Handler().Handle(AQuery(2), CancellationToken.None);

        captured!.Limit.Should().Be(3);
    }

    [Fact]
    public async Task A_full_page_reports_the_last_returned_row_as_the_next_cursor()
    {
        var second = Guid.NewGuid();
        Returns(ARow(Guid.NewGuid()), ARow(second), ARow(Guid.NewGuid()));

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().Be(second.ToString());
        result.Value.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task The_extra_row_is_never_returned_to_the_caller()
    {
        var overflow = Guid.NewGuid();
        Returns(ARow(Guid.NewGuid()), ARow(Guid.NewGuid()), ARow(overflow));

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.Items.Select(item => item.Id).Should().NotContain(overflow);
    }

    [Fact]
    public async Task A_partial_page_reports_no_next_cursor()
    {
        Returns(ARow(Guid.NewGuid()));

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.NextCursor.Should().BeNull();
        result.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task An_exactly_full_page_with_nothing_beyond_reports_no_cursor()
    {
        Returns(ARow(Guid.NewGuid()), ARow(Guid.NewGuid()));

        var result = await Handler().Handle(AQuery(2), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task An_empty_result_is_a_success_rather_than_a_not_found()
    {
        Returns();

        var result = await Handler().Handle(AQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task It_passes_every_filter_through_to_the_criteria()
    {
        JobSearchCriteria? captured = null;
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<JobSearchCriteria>(), It.IsAny<CancellationToken>()))
            .Callback<JobSearchCriteria, CancellationToken>((criteria, _) => captured = criteria)
            .ReturnsAsync([]);

        var assignee = Guid.NewGuid();
        var query = new SearchJobsQuery(
            Guid.NewGuid(), "ridge", [JobStatus.Completed],
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            assignee, JobSortField.Title, "cursor-1", 5);

        await Handler().Handle(query, CancellationToken.None);

        captured!.Text.Should().Be("ridge");
        captured.Statuses.Should().Equal(JobStatus.Completed);
        captured.AssigneeId.Should().Be(assignee);
        captured.Sort.Should().Be(JobSortField.Title);
        captured.Cursor.Should().Be("cursor-1");
    }

    [Fact]
    public async Task It_renders_the_status_as_text_rather_than_as_an_ordinal()
    {
        Returns(ARow(Guid.NewGuid()));

        var result = await Handler().Handle(AQuery(), CancellationToken.None);

        result.Value.Items.Single().Status.Should().Be("Scheduled");
    }
}
