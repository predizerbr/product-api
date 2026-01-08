namespace Pruduct.Common.Entities;

public class BaseEntity<TKey> : IBaseEntity<TKey>
    where TKey : new()
{
    public TKey Id { get; set; } = new();
}
