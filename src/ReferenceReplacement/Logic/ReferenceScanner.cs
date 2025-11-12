using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FrooxEngine;

namespace ReferenceReplacement.Logic;

internal static class ReferenceScanner
{
    public static ReferenceScanResult Scan(Slot root, IWorldElement source, IWorldElement target)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        var builder = new ScanBuilder(source, target);
        TraverseSlot(root, builder, DescribeSlot(root));
        return builder.Build();
    }

    private static void TraverseSlot(Slot slot, ScanBuilder builder, string currentPath)
    {
        builder.VisitWorker(slot, currentPath);

        foreach (Component component in slot.Components)
        {
            string componentPath = $"{currentPath}::{component.GetType().Name}";
            builder.VisitWorker(component, componentPath);
        }

        foreach (Slot child in slot.Children)
        {
            string childLabel = DescribeSlot(child);
            TraverseSlot(child, builder, $"{currentPath}/{childLabel}");
        }
    }

    private static string DescribeSlot(Slot slot)
    {
        return string.IsNullOrWhiteSpace(slot?.Name) ? slot?.ReferenceID.ToString() ?? "(null)" : slot!.Name;
    }

    private sealed class ScanBuilder
    {
        private static readonly Dictionary<Type, PropertyInfo[]> EnumerablePropertyCache = new();
        private static readonly ReferenceComparer EnumerableComparer = new();

        private readonly IWorldElement _source;
        private readonly IWorldElement _target;
        private readonly List<ReferenceHit> _matches = new();
        private readonly HashSet<ISyncRef> _capturedRefs = new();
        private readonly HashSet<object> _visitedEnumerables = new(EnumerableComparer);

        private int _incompatibleCount;
        private int _visitedMembers;
        private string? _lastPath;

        public ScanBuilder(IWorldElement source, IWorldElement target)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void VisitWorker(Worker worker, string path)
        {
            if (worker == null)
            {
                return;
            }

            foreach (ISyncMember member in worker.SyncMembers)
            {
                string memberPath = $"{path}.{member.Name}";
                VisitMember(member, worker, memberPath);
            }
        }

        public ReferenceScanResult Build()
        {
            return new ReferenceScanResult(_matches.AsReadOnly(), _incompatibleCount, _visitedMembers, _lastPath);
        }

        private void VisitMember(ISyncMember member, Worker owner, string path)
        {
            if (member == null)
            {
                return;
            }

            _visitedMembers++;

            if (member is ISyncRef syncRef)
            {
                Capture(syncRef, owner, path);
                return;
            }

            if (member is IEnumerable enumerable)
            {
                VisitEnumerable(enumerable, owner, path);
            }

            foreach (PropertyInfo property in GetEnumerableProperties(member.GetType()))
            {
                object? value = property.GetValue(member);
                if (value is IEnumerable nested && value is not string)
                {
                    VisitEnumerable(nested, owner, $"{path}.{property.Name}");
                }
            }
        }

        private void VisitEnumerable(IEnumerable enumerable, Worker owner, string path)
        {
            if (enumerable == null)
            {
                return;
            }

            if (enumerable is string)
            {
                return;
            }

            if (!_visitedEnumerables.Add(enumerable))
            {
                return;
            }

            int index = 0;
            foreach (object? item in enumerable)
            {
                string childPath = $"{path}[{index}]";
                if (item is ISyncRef syncRef)
                {
                    Capture(syncRef, owner, childPath);
                }
                else if (item is ISyncMember member)
                {
                    VisitMember(member, owner, childPath);
                }
                else if (item is IEnumerable nested && item is not string)
                {
                    VisitEnumerable(nested, owner, childPath);
                }
                else if (item is DictionaryEntry entry && entry.Value is ISyncRef entryRef)
                {
                    Capture(entryRef, owner, childPath);
                }
                else if (item != null)
                {
                    ISyncRef? extracted = TryExtractSyncRef(item);
                    if (extracted != null)
                    {
                        Capture(extracted, owner, childPath);
                    }
                }

                index++;
            }
        }

        private void Capture(ISyncRef syncRef, Worker owner, string path)
        {
            if (syncRef == null)
            {
                return;
            }

            if (!_capturedRefs.Add(syncRef))
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

            _lastPath = path;
            _matches.Add(new ReferenceHit(syncRef, owner, path));
        }

        private bool MatchesSource(ISyncRef syncRef)
        {
            IWorldElement? current = syncRef.Target;
            if (current == null)
            {
                return false;
            }

            if (ReferenceEquals(current, _source))
            {
                return true;
            }

            return current.ReferenceID == _source.ReferenceID;
        }

        private bool SupportsTarget(ISyncRef syncRef)
        {
            Type? requiredType = syncRef.TargetType;
            if (requiredType == null)
            {
                return true;
            }

            return requiredType.IsInstanceOfType(_target);
        }

        private static ISyncRef? TryExtractSyncRef(object candidate)
        {
            if (candidate is ISyncRef direct)
            {
                return direct;
            }

            Type type = candidate.GetType();
            if (type.IsGenericType && type.Name.StartsWith("KeyValuePair", StringComparison.Ordinal))
            {
                PropertyInfo? valueProperty = type.GetProperty("Value");
                if (valueProperty != null && typeof(ISyncRef).IsAssignableFrom(valueProperty.PropertyType))
                {
                    return valueProperty.GetValue(candidate) as ISyncRef;
                }
            }

            return null;
        }

        private static PropertyInfo[] GetEnumerableProperties(Type type)
        {
            lock (EnumerablePropertyCache)
            {
                if (EnumerablePropertyCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                PropertyInfo[] discovered = Array.FindAll(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public),
                    prop => prop.GetIndexParameters().Length == 0 &&
                            typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                            prop.PropertyType != typeof(string));

                EnumerablePropertyCache[type] = discovered;
                return discovered;
            }
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

internal sealed record ReferenceHit(ISyncRef SyncRef, Worker Owner, string Path);

internal sealed class ReferenceScanResult
{
    public ReferenceScanResult(IReadOnlyList<ReferenceHit> matches, int incompatibleCount, int visitedMembers, string? lastHitPath)
    {
        MatchingRefs = matches;
        IncompatibleCount = incompatibleCount;
        VisitedMembers = visitedMembers;
        LastHitPath = lastHitPath;
    }

    public IReadOnlyList<ReferenceHit> MatchingRefs { get; }
    public int IncompatibleCount { get; }
    public int VisitedMembers { get; }
    public string? LastHitPath { get; }
}
