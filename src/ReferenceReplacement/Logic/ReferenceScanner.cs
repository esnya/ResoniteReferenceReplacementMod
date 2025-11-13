using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Elements.Core;
using FrooxEngine;
using ReferenceReplacement.Infrastructure;

namespace ReferenceReplacement.Logic;

internal static class ReferenceScanner
{
    public static ReferenceScanResult Scan(Slot root, IWorldElement source, IWorldElement target)
    {
        ArgumentNullException.ThrowIfNull(root);

        var traversal = new SyncReferenceTraversal(source, target);
        try
        {
            traversal.VisitSlot(root);
            return traversal.BuildResult();
        }
        finally
        {
            traversal.Dispose();
        }
    }

    internal static ReferenceScanResult Scan(HierarchyBlueprint blueprintRoot, IWorldElement source, IWorldElement target)
    {
        ArgumentNullException.ThrowIfNull(blueprintRoot);

        var traversal = new SyncReferenceTraversal(source, target);
        try
        {
            traversal.VisitBlueprint(blueprintRoot, new TraversalPath(blueprintRoot.Label));
            return traversal.BuildResult();
        }
        finally
        {
            traversal.Dispose();
        }
    }

    private sealed class SyncReferenceTraversal : IDisposable
    {
        private readonly ReferenceMatchAccumulator _accumulator;

        internal SyncReferenceTraversal(IWorldElement source, IWorldElement target)
        {
            _accumulator = new ReferenceMatchAccumulator(source, target);
        }

        internal void VisitSlot(Slot slot)
        {
            if (slot == null)
            {
                return;
            }

            TraversalPath path = TraversalPath.FromSlot(slot);
            VisitWorker(slot, path);
            VisitComponents(slot, path);
            VisitChildren(slot, path);
        }

        internal void VisitBlueprint(HierarchyBlueprint node, TraversalPath path)
        {
            foreach (ISyncMember member in node.Members)
            {
                VisitMember(member, path.NextMember(member.Name));
            }

            foreach (HierarchyBlueprint child in node.Children)
            {
                VisitBlueprint(child, path.NextChild(child.Label));
            }
        }

        internal ReferenceScanResult BuildResult() => _accumulator.BuildResult();

        public void Dispose()
        {
            _accumulator.Dispose();
        }

        private void VisitComponents(Slot slot, TraversalPath parentPath)
        {
            foreach (Component component in slot.Components)
            {
                TraversalPath componentPath = parentPath.NextComponent(component);
                VisitWorker(component, componentPath);
            }
        }

        private void VisitChildren(Slot slot, TraversalPath parentPath)
        {
            foreach (Slot child in slot.Children)
            {
                TraversalPath childPath = parentPath.NextChild(TraversalPath.DescribeSlot(child));
                VisitSlot(child, childPath);
            }
        }

        private void VisitSlot(Slot slot, TraversalPath path)
        {
            VisitWorker(slot, path);
            VisitComponents(slot, path);
            VisitChildren(slot, path);
        }

        private void VisitWorker(Worker worker, TraversalPath path)
        {
            if (worker == null)
            {
                return;
            }

            foreach (ISyncMember member in worker.SyncMembers)
            {
                VisitMember(member, path.NextMember(member.Name));
            }
        }

        private void VisitMember(ISyncMember member, TraversalPath path)
        {
            if (_accumulator.TryCaptureDirect(member, path))
            {
                return;
            }

            if (member is IEnumerable enumerable && _accumulator.ShouldVisitEnumerable(enumerable))
            {
                VisitEnumerable(enumerable, path);
            }

            VisitKnownCollections(member, path);
        }

        private void VisitKnownCollections(ISyncMember member, TraversalPath path)
        {
            switch (member)
            {
                case ISyncList syncList:
                    VisitEnumerableProperty(() => syncList.Elements, path, nameof(ISyncList.Elements));
                    break;
                case ISyncBag syncBag:
                    VisitEnumerableProperty(() => syncBag.Elements, path, nameof(ISyncBag.Elements));
                    VisitEnumerableProperty(() => syncBag.Values, path, nameof(ISyncBag.Values));
                    break;
                case ISyncDictionary syncDictionary:
                    VisitEnumerableProperty(() => syncDictionary.BoxedEntries, path, nameof(ISyncDictionary.BoxedEntries));
                    VisitEnumerableProperty(() => syncDictionary.Values, path, nameof(ISyncDictionary.Values));
                    break;
                case ISyncArray syncArray:
                    VisitSyncArray(syncArray, path);
                    break;
            }
        }

        private void VisitEnumerableProperty(Func<IEnumerable?> accessor, TraversalPath parentPath, string propertyName)
        {
            IEnumerable? enumerable;
            try
            {
                enumerable = accessor();
            }
            catch (Exception ex) when (IsSafeToIgnore(ex))
            {
                return;
            }

            if (enumerable == null)
            {
                return;
            }

            if (_accumulator.ShouldVisitEnumerable(enumerable))
            {
                VisitEnumerable(enumerable, parentPath.NextProperty(propertyName));
            }
        }

        private void VisitSyncArray(ISyncArray array, TraversalPath parentPath)
        {
            int count;
            try
            {
                count = array.Count;
            }
            catch (Exception ex) when (IsSafeToIgnore(ex))
            {
                return;
            }

            TraversalPath itemsPath = parentPath.NextProperty("Items");
            for (int index = 0; index < count; index++)
            {
                object? element;
                try
                {
                    element = array.GetElement(index);
                }
                catch (Exception ex) when (IsSafeToIgnore(ex))
                {
                    continue;
                }

                VisitNestedItem(element, itemsPath.NextIndex(index));
            }
        }

        private void VisitNestedItem(object? item, TraversalPath path)
        {
            if (item == null)
            {
                return;
            }

            switch (item)
            {
                case ISyncMember member:
                    VisitMember(member, path);
                    break;
                case IEnumerable nested when _accumulator.ShouldVisitEnumerable(nested):
                    VisitEnumerable(nested, path);
                    break;
            }
        }

        private static bool IsSafeToIgnore(Exception? exception)
        {
            return exception is NotSupportedException or InvalidOperationException;
        }

        private void VisitEnumerable(IEnumerable enumerable, TraversalPath path)
        {
            int index = 0;
            foreach (object? item in enumerable)
            {
                TraversalPath itemPath = path.NextIndex(index);
                if (_accumulator.TryCaptureFromObject(item, itemPath))
                {
                    index++;
                    continue;
                }

                VisitNestedItem(item, itemPath);

                index++;
            }
        }
    }

    private sealed class ReferenceMatchAccumulator : IDisposable
    {
        private readonly IWorldElement _source;
        private readonly IWorldElement _target;
        private readonly BorrowedList<SyncReferenceMatch> _matchesLease;
        private readonly BorrowedHashSet<ISyncRef> _visitedRefsLease;
        private readonly List<SyncReferenceMatch> _matches;
        private readonly HashSet<ISyncRef> _visitedRefs;
        private readonly HashSet<object> _visitedEnumerables = new(ReferenceEqualityComparer.Instance);

        private int _visitedMembers;
        private int _incompatibleCount;
        private string? _lastPath;

        internal ReferenceMatchAccumulator(IWorldElement source, IWorldElement target)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            _source = source;
            _target = target;
            _matchesLease = BorrowedList<SyncReferenceMatch>.Rent(out _matches);
            _visitedRefsLease = BorrowedHashSet<ISyncRef>.Rent(out _visitedRefs);
        }

        internal bool TryCaptureDirect(ISyncMember member, TraversalPath path)
        {
            if (member == null)
            {
                return false;
            }

            _visitedMembers++;

            if (member is not ISyncRef syncRef)
            {
                return false;
            }

            Capture(syncRef, path);
            return true;
        }

        internal bool TryCaptureFromObject(object? candidate, TraversalPath path)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate is ISyncRef syncRef)
            {
                Capture(syncRef, path);
                return true;
            }

            if (candidate is DictionaryEntry entry && entry.Value is ISyncRef entryRef)
            {
                Capture(entryRef, path);
                return true;
            }

            ISyncRef? extracted = EnumerableInspector.TryExtractSyncRef(candidate);
            if (extracted != null)
            {
                Capture(extracted, path);
                return true;
            }

            return false;
        }

        internal bool ShouldVisitEnumerable(IEnumerable enumerable)
        {
            if (enumerable == null)
            {
                return false;
            }

            if (enumerable is string)
            {
                return false;
            }

            return _visitedEnumerables.Add(enumerable);
        }

        internal ReferenceScanResult BuildResult()
        {
            SyncReferenceMatch[] snapshot = _matches.ToArray();
            return new ReferenceScanResult(snapshot, _incompatibleCount, _visitedMembers, _lastPath);
        }

        public void Dispose()
        {
            _matchesLease.Dispose();
            _visitedRefsLease.Dispose();
            _visitedEnumerables.Clear();
        }

        private void Capture(ISyncRef syncRef, TraversalPath path)
        {
            if (!_visitedRefs.Add(syncRef))
            {
                return;
            }

            if (!MatchesSource(syncRef))
            {
                return;
            }

            if (!SupportsTarget(syncRef))
            {
                _incompatibleCount++;
                return;
            }

            _lastPath = path.Value;
            _matches.Add(new SyncReferenceMatch(syncRef, path.Value));
        }

        private bool MatchesSource(ISyncRef syncRef)
        {
            IWorldElement? current = syncRef.Target;
            if (current == null)
            {
                return false;
            }

            return ReferenceEquals(current, _source) || current.ReferenceID == _source.ReferenceID;
        }

        private bool SupportsTarget(ISyncRef syncRef)
        {
            Type? requiredType = syncRef.TargetType;
            return requiredType == null || requiredType.IsInstanceOfType(_target);
        }
    }

    private static class EnumerableInspector
    {
        internal static ISyncRef? TryExtractSyncRef(object candidate)
        {
            Type type = candidate.GetType();
            if (!type.IsGenericType || !type.Name.StartsWith("KeyValuePair", StringComparison.Ordinal))
            {
                return null;
            }

            PropertyInfo? valueProperty = type.GetProperty("Value");
            if (valueProperty == null)
            {
                return null;
            }

            return valueProperty.GetValue(candidate) as ISyncRef;
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

}

internal sealed record SyncReferenceMatch(ISyncRef SyncRef, string Path);

internal sealed class ReferenceScanResult
{
    public ReferenceScanResult(IReadOnlyList<SyncReferenceMatch> matches, int incompatibleCount, int visitedMembers, string? lastHitPath)
    {
        Matches = matches;
        IncompatibleCount = incompatibleCount;
        VisitedMembers = visitedMembers;
        LastHitPath = lastHitPath;
    }

    public IReadOnlyList<SyncReferenceMatch> Matches { get; }
    public int IncompatibleCount { get; }
    public int VisitedMembers { get; }
    public string? LastHitPath { get; }
}

internal sealed record TraversalPath(string Value)
{
    public static TraversalPath FromSlot(Slot slot) => new(DescribeSlot(slot));

    public TraversalPath NextComponent(Component component) => new($"{Value}::{component.GetType().Name}");
    public TraversalPath NextChild(string label) => new($"{Value}/{label}");
    public TraversalPath NextMember(string memberName) => new($"{Value}.{memberName}");
    public TraversalPath NextProperty(string propertyName) => new($"{Value}.{propertyName}");
    public TraversalPath NextIndex(int index) => new($"{Value}[{index}]");

    public static string DescribeSlot(Slot slot)
    {
        return string.IsNullOrWhiteSpace(slot?.Name) ? slot?.ReferenceID.ToString() ?? "(null)" : slot!.Name;
    }
}

internal sealed record HierarchyBlueprint(string Label, IReadOnlyList<ISyncMember> Members, IReadOnlyList<HierarchyBlueprint> Children)
{
    public static HierarchyBlueprint Create(string label, IEnumerable<ISyncMember> members, IEnumerable<HierarchyBlueprint>? children = null)
    {
        var memberList = members is IReadOnlyList<ISyncMember> readyMembers
            ? readyMembers
            : new List<ISyncMember>(members ?? Array.Empty<ISyncMember>());
        var childList = children is IReadOnlyList<HierarchyBlueprint> readyChildren
            ? readyChildren
            : new List<HierarchyBlueprint>(children ?? Array.Empty<HierarchyBlueprint>());
        return new HierarchyBlueprint(label, memberList, childList);
    }
}
