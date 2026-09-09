using System.Text.Json;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

/// <summary>
/// Type names resolve against an allowlist built from the module's own
/// assembly, rather than through <c>TypeNameHandling</c>.
///
/// The rows are ones this application wrote, so the input is not untrusted
/// today. But a deserialiser that will construct any type its input names is a
/// gadget waiting for the day something else can write that column, and the
/// allowlist costs one dictionary built once.
/// </summary>
internal static class OutboxSerializer
{
    private static readonly IReadOnlyDictionary<string, Type> Known =
        typeof(Job).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true }
                           && type.IsAssignableTo(typeof(IDomainEvent)))
            .ToDictionary(type => type.FullName!);

    public static string NameOf(IDomainEvent domainEvent) => domainEvent.GetType().FullName!;

    public static string Serialize(IDomainEvent domainEvent) =>
        JsonSerializer.Serialize(domainEvent, domainEvent.GetType());

    public static IDomainEvent Deserialize(string type, string content) =>
        Known.TryGetValue(type, out var clrType)
            ? (IDomainEvent)JsonSerializer.Deserialize(content, clrType)!
            // A row naming a type this build does not have is a deployment
            // problem. Failing loudly leaves the row unprocessed for the build
            // that does have it, which is the right outcome.
            : throw new InvalidOperationException($"No domain event type named '{type}'.");
}
