using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

using FrooxEngine;

namespace ReferenceReplacement.Logic;

internal static class ReferenceScanner
{
    public static ReferenceScanResult Scan(Slot root, IWorldElement source, IWorldElement target, Slot? excludedSlot = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        ReferenceScanSession session = new(source, target, excludedSlot);
        session.VisitSlot(root, TraversalPath.FromSlot(root));
        return session.BuildResult();
    }

    internal static ReferenceScanResult Scan(HierarchyBlueprint blueprintRoot, IWorldElement source, IWorldElement target)
    {
        ArgumentNullException.ThrowIfNull(blueprintRoot);

        ReferenceScanSession session = new(source, target, excludedSlot: null);
        session.VisitBlueprint(blueprintRoot, new(blueprintRoot.Label));
        return session.BuildResult();
    }

    private sealed class ReferenceScanSession
    {
        private readonly IWorldElement _source;
        private readonly IWorldElement _target;
        private readonly Slot? _excludedSlot;
        private readonly List<SyncReferenceMatch> _matches = new();
        private readonly HashSet<ISyncRef> _visitedRefs = new();
        private readonly HashSet<object> _visitedEnumerables = new(ReferenceEqualityComparer.Instance);

        private int _visitedMembers;
        private int _incompatibleCount;
        private string? _lastPath;

        internal ReferenceScanSession(IWorldElement source, IWorldElement target, Slot? excludedSlot)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _excludedSlot = excludedSlot;
        }

        internal void VisitSlot(Slot? slot, TraversalPath path)
        {
            if (slot == null || ReferenceEquals(slot, _excludedSlot))
            {
                return;
            }

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

        internal ReferenceScanResult BuildResult()
        {
            return new ReferenceScanResult(_matches.ToArray(), _incompatibleCount, _visitedMembers, _lastPath);
        }

        private void VisitComponents(Slot slot, TraversalPath parentPath)
        {
            foreach (Component component in slot.Components)
            {
                VisitWorker(component, parentPath.NextComponent(component));
            }
        }

        private void VisitChildren(Slot slot, TraversalPath parentPath)
        {
            foreach (Slot child in slot.Children)
            {
                VisitSlot(child, parentPath.NextChild(TraversalPath.DescribeSlot(child)));
            }
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
            if (member == null)
            {
                return;
            }

            _visitedMembers++;

            if (TryCapture(member, path))
            {
                return;
            }

            if (member is IEnumerable enumerable && ShouldVisitEnumerable(enumerable))
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
            catch (Exception ex) when (ShouldIgnore(ex))
            {
                return;
            }

            if (enumerable == null)
            {
                return;
            }

            if (ShouldVisitEnumerable(enumerable))
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
            catch (Exception ex) when (ShouldIgnore(ex))
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
                catch (Exception ex) when (ShouldIgnore(ex))
                {
                    continue;
                }

                VisitValue(element, itemsPath.NextIndex(index));
            }
        }

        private void VisitEnumerable(IEnumerable enumerable, TraversalPath path)
        {
            int index = 0;
            foreach (object? item in enumerable)
            {
                VisitValue(item, path.NextIndex(index));
                index++;
            }
        }

        private void VisitValue(object? value, TraversalPath path)
        {
            if (value == null)
            {
                return;
            }

            if (value is ISyncRef syncRef)
            {
                TryCapture(syncRef, path);
                return;
            }

            if (value is DictionaryEntry entry && entry.Value is ISyncRef entryRef)
            {
                TryCapture(entryRef, path);
                return;
            }

            if (value is ISyncMember member)
            {
                VisitMember(member, path);
                return;
            }

            if (value is IEnumerable enumerable && ShouldVisitEnumerable(enumerable))
            {
                VisitEnumerable(enumerable, path);
                return;
            }

            ISyncRef? extracted = TryExtractSyncRef(value);
            if (extracted != null)
            {
                TryCapture(extracted, path);
            }
        }

        private bool TryCapture(ISyncMember member, TraversalPath path)
        {
            if (member is not ISyncRef syncRef)
            {
                return false;
            }

            TryCapture(syncRef, path);
            return true;
        }

        private void TryCapture(ISyncRef syncRef, TraversalPath path)
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

        private bool ShouldVisitEnumerable(IEnumerable enumerable)
        {
            if (enumerable == null || enumerable is string)
            {
                return false;
            }

            return _visitedEnumerables.Add(enumerable);
        }

        private static bool ShouldIgnore(Exception ex)
        {
            return ex is NotSupportedException or InvalidOperationException;
        }

        private static ISyncRef? TryExtractSyncRef(object candidate)
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
        IReadOnlyList<ISyncMember> memberList = members is IReadOnlyList<ISyncMember> readyMembers
            ? readyMembers
            : new List<ISyncMember>(members ?? Array.Empty<ISyncMember>());
        IReadOnlyList<HierarchyBlueprint> childList = children is IReadOnlyList<HierarchyBlueprint> readyChildren
            ? readyChildren
            : new List<HierarchyBlueprint>(children ?? Array.Empty<HierarchyBlueprint>());
        return new HierarchyBlueprint(label, memberList, childList);
    }
}

internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new();

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

    public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
