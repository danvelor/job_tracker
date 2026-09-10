namespace JobTracker.Common.Domain;

public abstract class Entity
{
    protected Entity(Guid id) => Id = id;

    protected Entity() { }

    public Guid Id { get; protected init; }

    public override bool Equals(object? obj) =>
        obj is Entity other && other.GetType() == GetType() && other.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
