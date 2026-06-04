using CommTT.Domain.Models;

namespace CommTT.Domain.Events;

public class CommEventArgs : EventArgs
{
    public CommDataFrame Frame { get; init; } = null!;
}
