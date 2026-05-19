using K7.Server.Domain.Common;
using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Events;

public class DownloadReadyEvent(Download download) : BaseEvent
{
    public Download Download { get; } = download;
}
