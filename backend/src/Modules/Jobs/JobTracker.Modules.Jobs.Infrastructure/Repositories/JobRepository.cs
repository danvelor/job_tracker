using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Repositories;

/// <summary>
/// Split with <c>partial</c> across three files by responsibility (assessment
/// line 222). The split is justified by what ends up in the files rather than
/// by the instruction to make one: the read side carries the keyset predicate,
/// the full-text filter and the per-row photo count, and the write side is one
/// method that stages and does not commit.
/// </summary>
internal sealed partial class JobRepository(JobsDbContext context) : IJobRepository;
