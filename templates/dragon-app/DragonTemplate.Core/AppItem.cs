namespace DragonTemplate.Core;

public sealed record AppItem(
    Guid Id,
    string Title,
    bool IsComplete,
    DateTimeOffset UpdatedAt)
{
    public static AppItem Create(string title, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new AppItem(Guid.NewGuid(), title.Trim(), false, now);
    }
}

