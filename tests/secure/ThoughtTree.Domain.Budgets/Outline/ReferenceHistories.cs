using ThoughtTree.Domain.Clock;
using ThoughtTree.Domain.Outline;

namespace ThoughtTree.Domain.Budgets.Outline;

/// <summary>Event logs of a given size and shape, the same in every run (architecture.md section 8, complexity checks).</summary>
internal static class ReferenceHistories
{
    private static readonly DeviceId Laptop = new("a0000000-0000-0000-0000-000000000001");
    private static readonly DeviceId Phone = new("b0000000-0000-0000-0000-000000000002");

    public static IReadOnlyList<TreeEvent> BranchingTree(int events)
    {
        var log = new List<TreeEvent>(events);
        var created = 0;
        for (var i = 0; i < events; i++)
        {
            var clock = new EventClock(i + 1);
            var kind = i % 20;
            if (created < 2 || kind < 13)
            {
                var parent = created == 0 ? (ItemId?)null : ItemNumber((created - 1) / 10);
                log.Add(new ItemCreated(EventNumber(i), Laptop, clock, ActionNumber(i), ItemNumber(created), parent, null, new Title($"t{i}")));
                created++;
            }
            else if (kind < 17)
            {
                log.Add(new ItemTitleChanged(EventNumber(i), Laptop, clock, ActionNumber(i), ItemNumber(i * 7919 % created), new Title($"r{i}")));
            }
            else if (kind < 19)
            {
                log.Add(new ItemMoved(EventNumber(i), Laptop, clock, ActionNumber(i), ItemNumber(i * 7919 % created), ItemNumber(i * 104729 % created), null));
            }
            else
            {
                log.Add(new ItemDeleted(EventNumber(i), Laptop, clock, ActionNumber(i), ItemNumber(i * 7919 % created)));
            }
        }

        return log;
    }

    public static IReadOnlyList<TreeEvent> WideParent(int events)
    {
        var log = new List<TreeEvent>(events)
        {
            new ItemCreated(EventNumber(0), Laptop, new EventClock(1), ActionNumber(0), ItemNumber(0), null, null, new Title("inbox")),
        };
        for (var i = 1; i < events; i++)
        {
            var after = i == 1 ? (ItemId?)null : ItemNumber(i - 1);
            log.Add(new ItemCreated(EventNumber(i), Laptop, new EventClock(i + 1), ActionNumber(i), ItemNumber(i), ItemNumber(0), after, new Title($"t{i}")));
        }

        return log;
    }

    public static (IReadOnlyList<TreeEvent> Applied, IReadOnlyList<TreeEvent> ArrivingLate) SyncAfterOfflineWork(int events)
    {
        var applied = BranchingTree(events - (events / 10));
        var late = Enumerable.Range(0, events / 10)
            .Select(i => (TreeEvent)new ItemTitleChanged(EventNumber(events + i), Phone, new EventClock((i * 9) + 1), ActionNumber(events + i), ItemNumber(0), new Title($"p{i}")))
            .ToList();
        return (applied, late);
    }

    private static ItemId ItemNumber(int n) => new($"c0000000-0000-0000-0000-{n:D12}");

    private static EventId EventNumber(int n) => new($"e0000000-0000-0000-0000-{n:D12}");

    private static ActionId ActionNumber(int n) => new($"d0000000-0000-0000-0000-{n:D12}");
}
