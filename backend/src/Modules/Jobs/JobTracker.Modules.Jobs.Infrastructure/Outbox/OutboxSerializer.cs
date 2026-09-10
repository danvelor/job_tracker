using System.Text.Json;
using JobTracker.Common.Domain;
using JobTracker.Modules.Jobs.Domain;

namespace JobTracker.Modules.Jobs.Infrastructure.Outbox;

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
            : throw new InvalidOperationException($"No domain event type named '{type}'.");
}
