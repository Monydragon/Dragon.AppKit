using DragonTemplate.Core;

namespace DragonTemplate.Application;

public sealed class StarterItemService
{
    public IReadOnlyList<AppItem> CreateStarterItems(DateTimeOffset now)
    {
        return
        [
            AppItem.Create("Replace starter brand assets", now),
            AppItem.Create("Run release candidate gate", now),
            AppItem.Create("Publish first local artifacts", now)
        ];
    }
}

