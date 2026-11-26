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
        for (var index = 0; index < events; index++)
        {
            var clock = new EventClock(index + 1);
            var kind = index % 20;
            if (created < 2 || kind < 13)
            {
                var parent = created == 0 ? (ItemId?)null : ItemNumber((created - 1) / 10);
                log.Add(new ItemCreated(EventNumber(index), Laptop, clock, ActionNumber(index), ItemNumber(created), parent, null, new Title($"t{index}")));
                created++;
            }
            else if (kind < 17)
            {
                log.Add(new ItemTitleChanged(EventNumber(index), Laptop, clock, ActionNumber(index), ItemNumber(index * 7919 % created), new Title($"r{index}")));
            }
            else if (kind < 19)
            {
                log.Add(new ItemMoved(EventNumber(index), Laptop, clock, ActionNumber(index), ItemNumber(index * 7919 % created), ItemNumber(index * 104729 % created), null));
            }
            else
            {
                log.Add(new ItemDeleted(EventNumber(index), Laptop, clock, ActionNumber(index), ItemNumber(index * 7919 % created)));
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
        for (var index = 1; index < events; index++)
        {
            var after = index == 1 ? (ItemId?)null : ItemNumber(index - 1);
            log.Add(new ItemCreated(EventNumber(index), Laptop, new EventClock(index + 1), ActionNumber(index), ItemNumber(index), ItemNumber(0), after, new Title($"t{index}")));
        }

        return log;
    }

    public static (IReadOnlyList<TreeEvent> Applied, IReadOnlyList<TreeEvent> ArrivingLate) SyncAfterOfflineWork(int events)
    {
        var applied = BranchingTree(events - (events / 10));
        var late = Enumerable.Range(0, events / 10)
            .Select(index => (TreeEvent)new ItemTitleChanged(EventNumber(events + index), Phone, new EventClock((index * 9) + 1), ActionNumber(events + index), ItemNumber(0), new Title($"p{index}")))
            .ToList();
        return (applied, late);
    }

    private static ItemId ItemNumber(int number) => new($"c0000000-0000-0000-0000-{number:D12}");

    private static EventId EventNumber(int number) => new($"e0000000-0000-0000-0000-{number:D12}");

    private static ActionId ActionNumber(int number) => new($"d0000000-0000-0000-0000-{number:D12}");
}
