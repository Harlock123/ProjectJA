// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Host.Components.Timeline;

/// <summary>One node in the WBS hierarchy of a project — derived purely from
/// each issue's <c>wbs:*</c> labels (set during xlsx import). Issues that
/// don't carry a wbs label become depth-1 orphans so they still appear on the
/// timeline; the hierarchy is best-effort, not a domain invariant.</summary>
public sealed record WbsNode(
    Guid IssueId,
    string Wbs,                    // "1.2.3" — empty for unlabelled issues
    int Depth,                     // 1-based; orphans = 1
    Guid? ParentId,
    IReadOnlyList<Guid> ChildrenIds);

/// <summary>Builds a WBS tree from a flat issue list, sorted in pre-order
/// (parents immediately followed by their children). Pure helper — no
/// DbContext, no Razor. Unit-testable.</summary>
public static class WbsTree
{
    private const string WbsLabelPrefix = "wbs:";

    /// <summary>Returns nodes in pre-order traversal order (depth-first, parent
    /// before children, sorted by WBS within each level). Issues without a
    /// wbs label are appended at the end as depth-1 orphans in title-stable
    /// order (the caller can re-sort if it wants).</summary>
    public static IReadOnlyList<WbsNode> Build(IEnumerable<Issue> issues)
    {
        var all = issues.ToList();

        // First pass: per-issue WBS string (or null when no label).
        var wbsByIssue = new Dictionary<Guid, string?>(capacity: all.Count);
        foreach (var i in all)
            wbsByIssue[i.Id] = ExtractWbs(i);

        // Sort dated WBS issues by their dot-segment tuple — "1.10" sorts
        // AFTER "1.2", matching MS Project's natural outline order.
        var withWbs = all
            .Where(i => wbsByIssue[i.Id] is not null)
            .OrderBy(i => wbsByIssue[i.Id]!, NaturalWbsComparer.Instance)
            .ToList();
        var orphans = all
            .Where(i => wbsByIssue[i.Id] is null)
            .OrderBy(i => i.Number)
            .ToList();

        // Walk the sorted list and build parent links. A wbs of "x.y.z" has
        // parent "x.y" if that exists in the same project, else the next
        // shorter prefix that does, else null.
        //
        // Dupe-WBS tolerance: an xlsx import can legally produce two issues
        // with the same WBS (the import warns but still creates both). The
        // first one we see becomes the canonical parent for child-WBS lookup;
        // the duplicate still renders as its own row, sibling to the canonical.
        var byWbs = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in withWbs)
            byWbs.TryAdd(wbsByIssue[i.Id]!, i.Id);
        var childrenByParent = new Dictionary<Guid, List<Guid>>();
        var parentByChild = new Dictionary<Guid, Guid?>();

        foreach (var i in withWbs)
        {
            var wbs = wbsByIssue[i.Id]!;
            var parentId = FindParentId(wbs, byWbs);
            parentByChild[i.Id] = parentId;
            if (parentId is { } pid)
            {
                if (!childrenByParent.TryGetValue(pid, out var list))
                    childrenByParent[pid] = list = new();
                list.Add(i.Id);
            }
        }

        // Build nodes for WBS issues.
        var nodes = new Dictionary<Guid, WbsNode>(capacity: all.Count);
        foreach (var i in withWbs)
        {
            var wbs = wbsByIssue[i.Id]!;
            nodes[i.Id] = new WbsNode(
                IssueId: i.Id,
                Wbs: wbs,
                Depth: DepthOf(wbs, parentByChild[i.Id], nodes),
                ParentId: parentByChild[i.Id],
                ChildrenIds: childrenByParent.GetValueOrDefault(i.Id, new()).AsReadOnly());
        }

        // Build nodes for orphans (no wbs label) — all depth 1, no parent.
        foreach (var i in orphans)
            nodes[i.Id] = new WbsNode(i.Id, Wbs: "", Depth: 1, ParentId: null, ChildrenIds: Array.Empty<Guid>());

        // Pre-order walk: roots first, then their children DFS, then orphans.
        var result = new List<WbsNode>(capacity: nodes.Count);
        var roots = withWbs.Where(i => parentByChild[i.Id] is null).Select(i => i.Id).ToList();
        foreach (var rootId in roots)
            AddSubtree(rootId, nodes, childrenByParent, wbsByIssue, result);
        foreach (var o in orphans) result.Add(nodes[o.Id]);
        return result;
    }

    private static void AddSubtree(
        Guid id,
        Dictionary<Guid, WbsNode> nodes,
        Dictionary<Guid, List<Guid>> childrenByParent,
        Dictionary<Guid, string?> wbsByIssue,
        List<WbsNode> sink)
    {
        sink.Add(nodes[id]);
        if (!childrenByParent.TryGetValue(id, out var kids)) return;
        var ordered = kids.OrderBy(k => wbsByIssue[k]!, NaturalWbsComparer.Instance).ToList();
        foreach (var k in ordered) AddSubtree(k, nodes, childrenByParent, wbsByIssue, sink);
    }

    private static Guid? FindParentId(string wbs, IDictionary<string, Guid> byWbs)
    {
        // Walk back through dot-stripped prefixes until one resolves to a
        // known WBS. Tolerates gaps (e.g. 1.1.1 with no 1.1 row) by climbing
        // to the next ancestor that exists.
        var dot = wbs.LastIndexOf('.');
        while (dot > 0)
        {
            var prefix = wbs[..dot];
            if (byWbs.TryGetValue(prefix, out var id)) return id;
            dot = prefix.LastIndexOf('.');
        }
        return null;
    }

    private static int DepthOf(string wbs, Guid? parentId, Dictionary<Guid, WbsNode> built)
    {
        // Cheap & honest: depth = parent depth + 1, or count dots + 1 for roots.
        if (parentId is null) return wbs.Count(c => c == '.') + 1;
        return built.TryGetValue(parentId.Value, out var parent) ? parent.Depth + 1 : 1;
    }

    private static string? ExtractWbs(Issue issue)
    {
        foreach (var label in issue.Labels)
        {
            if (label.StartsWith(WbsLabelPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var v = label[WbsLabelPrefix.Length..].Trim();
                if (v.Length > 0) return v;
            }
        }
        return null;
    }

    /// <summary>Natural dotted-number compare: "1.2" before "1.10". Falls back
    /// to ordinal compare on non-numeric segments so "1.a" still sorts.</summary>
    private sealed class NaturalWbsComparer : IComparer<string>
    {
        public static readonly NaturalWbsComparer Instance = new();
        public int Compare(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a is null) return -1;
            if (b is null) return 1;
            var ap = a.Split('.');
            var bp = b.Split('.');
            for (var i = 0; i < Math.Min(ap.Length, bp.Length); i++)
            {
                var an = int.TryParse(ap[i], out var av);
                var bn = int.TryParse(bp[i], out var bv);
                int cmp = (an, bn) switch
                {
                    (true, true) => av.CompareTo(bv),
                    (true, false) => -1,
                    (false, true) => 1,
                    _ => string.CompareOrdinal(ap[i], bp[i]),
                };
                if (cmp != 0) return cmp;
            }
            return ap.Length.CompareTo(bp.Length);
        }
    }
}
