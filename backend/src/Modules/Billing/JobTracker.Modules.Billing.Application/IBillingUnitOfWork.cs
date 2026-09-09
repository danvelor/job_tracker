using JobTracker.Common.Application;

namespace JobTracker.Modules.Billing.Application;

/// <summary>
/// Billing's own unit of work, named rather than keyed.
///
/// It was a keyed registration of the shared <see cref="IUnitOfWork"/>, and the
/// handler asked for the unkeyed one: it resolved Jobs' unit of work, saved
/// Jobs' context, and the invoice was added to Billing's and never written. The
/// build was green and the pipeline silently did nothing. A distinct type makes
/// that mistake a compile error instead.
/// </summary>
public interface IBillingUnitOfWork : IUnitOfWork;
