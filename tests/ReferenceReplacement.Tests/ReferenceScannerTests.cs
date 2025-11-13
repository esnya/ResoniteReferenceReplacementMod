using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Elements.Core;

using FrooxEngine;

using ReferenceReplacement.Logic;

using Xunit;

namespace ReferenceReplacement.Tests;

public class ReferenceScannerTests
{
    [Fact]
    public void ScanFindsDirectMatches()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef syncRef = new("SlotRef", typeof(FakeWorldElement), source);
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create("Root", new[] { syncRef });

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        SyncReferenceMatch match = Assert.Single(result.Matches);
        Assert.Equal(syncRef, match.SyncRef);
        Assert.Equal("Root.SlotRef", match.Path);
        Assert.Equal(1, result.VisitedMembers);
        Assert.Equal(0, result.IncompatibleCount);
    }

    [Fact]
    public void ScanCountsIncompatibleTargets()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef syncRef = new("SlotRef", typeof(SpecialWorldElement), source);
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create("Root", new[] { syncRef });

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        Assert.Empty(result.Matches);
        Assert.Equal(1, result.IncompatibleCount);
        Assert.Equal(1, result.VisitedMembers);
    }

    [Fact]
    public void ScanTraversesNestedEnumerables()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef nestedRef = new("Nested", typeof(FakeWorldElement), source);
        FakeSyncEnumerable enumerableMember = new("Collection", nestedRef);
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create(
            "Root",
            new ISyncMember[] { enumerableMember },
            Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        SyncReferenceMatch match = Assert.Single(result.Matches);
        Assert.Equal("Root.Collection[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncListElements()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef nestedRef = new("Nested", typeof(FakeWorldElement), source);
        FakeSyncList syncList = new("List", nestedRef);
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { syncList }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        SyncReferenceMatch match = Assert.Single(result.Matches);
        Assert.Equal("Root.List.Elements[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncDictionaryValues()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef nestedRef = new("Nested", typeof(FakeWorldElement), source);
        FakeSyncDictionary dictionary = new("Dict", new KeyValuePair<object, ISyncMember>("Key", nestedRef));
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { dictionary }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        SyncReferenceMatch match = Assert.Single(result.Matches);
        Assert.Equal("Root.Dict.BoxedEntries[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncArrayItems()
    {
        FakeWorldElement source = new("Source");
        FakeWorldElement replacement = new("Replacement");
        FakeSyncRef nestedRef = new("Nested", typeof(FakeWorldElement), source);
        FakeSyncArray arrayMember = new("Array", nestedRef);
        HierarchyBlueprint blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { arrayMember }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        SyncReferenceMatch match = Assert.Single(result.Matches);
        Assert.Equal("Root.Array.Items[0]", match.Path);
    }

    private class FakeWorldElement : IWorldElement
    {
        public FakeWorldElement(string name)
        {
            Name = name;
        }

        public RefID ReferenceID { get; set; }
        public string Name { get; set; }
        public World World => throw new NotSupportedException();
        public IWorldElement Parent { get => _parent ?? this; set => _parent = value; }
        public bool IsLocalElement => true;
        public bool IsPersistent { get; set; }
        public bool IsRemoved { get; set; }

        public void ChildChanged(IWorldElement child)
        {
            _ = child;
        }
        public DataTreeNode Save(SaveControl control) => throw new NotSupportedException();
        public void Load(DataTreeNode data, LoadControl control) => throw new NotSupportedException();
        public string GetSyncMemberName(ISyncMember syncMember) => syncMember?.Name ?? string.Empty;

        private IWorldElement? _parent;
    }

    private abstract class SpecialWorldElement : FakeWorldElement
    {
        protected SpecialWorldElement(string name) : base(name)
        {
        }
    }

    private abstract class FakeSyncMemberBase : ISyncMember
    {
        protected FakeSyncMemberBase(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
        public bool IsDrivable => false;
        public RefID ReferenceID { get; }
        public World World => throw new NotSupportedException();
        public IWorldElement Parent { get => _parent ?? this; set => _parent = value; }
        public bool IsLocalElement => true;
        public bool IsPersistent { get; set; }
        public bool IsRemoved { get; set; }
        public bool IsLinked => false;
        public bool IsDriven => false;
        public bool IsHooked => false;
        public ILinkRef ActiveLink => throw new NotSupportedException();
        public ILinkRef DirectLink => throw new NotSupportedException();
        public ILinkRef InheritedLink => throw new NotSupportedException();
        public IEnumerable<ILinkable> LinkableChildren => Array.Empty<ILinkable>();
        public bool IsInInitPhase => false;

        public event Action<IChangeable>? Changed
        {
            add { }
            remove { }
        }

        public void Initialize(World world, IWorldElement element)
        {
            _ = world;
            _ = element;
        }

        public void Dispose() { }

        public void CopyValues(ISyncMember other)
        {
            _ = other;
        }

        public void CopyValues(ISyncMember other, Action<ISyncMember, ISyncMember> copier)
        {
            _ = other;
            _ = copier;
        }

        public void EndInitPhase() { }

        public void Link(ILinkRef link)
        {
            _ = link;
        }

        public void InheritLink(ILinkRef link)
        {
            _ = link;
        }

        public void ReleaseLink(ILinkRef link)
        {
            _ = link;
        }

        public void ReleaseInheritedLink(ILinkRef link)
        {
            _ = link;
        }

        public void ChildChanged(IWorldElement child)
        {
            _ = child;
        }
        public DataTreeNode Save(SaveControl control) => throw new NotSupportedException();
        public void Load(DataTreeNode data, LoadControl control) => throw new NotSupportedException();
        public string GetSyncMemberName(ISyncMember syncMember) => syncMember?.Name ?? string.Empty;

        private IWorldElement? _parent;
    }

    private sealed class FakeSyncRef : FakeSyncMemberBase, ISyncRef
    {
        private IWorldElement? _target;

        public FakeSyncRef(string name, Type targetType, IWorldElement initialTarget) : base(name)
        {
            TargetType = targetType;
            Target = initialTarget;
        }

        public IWorldElement Target
        {
            get => _target!;
            set => _target = value;
        }

        public IWorldElement RawTarget => Target;
        public RefID Value { get; set; }
        public Type TargetType { get; }
        public ReferenceState State => ReferenceState.Available;

        public void Clear() => _target = null;

        public bool TrySet(IWorldElement target)
        {
            if (!TargetType.IsInstanceOfType(target))
            {
                return false;
            }

            Target = target;
            return true;
        }
    }

    private sealed class FakeSyncEnumerable : FakeSyncMemberBase, IEnumerable
    {
        private readonly object?[] _items;

        public FakeSyncEnumerable(string name, params object?[] items) : base(name)
        {
            _items = items ?? Array.Empty<object?>();
        }

        public IEnumerator GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    }

    private sealed class FakeSyncList : FakeSyncMemberBase, ISyncList
    {
        private readonly List<ISyncMember> _elements;

        public FakeSyncList(string name, params ISyncMember[] elements) : base(name)
        {
            _elements = elements?.ToList() ?? new List<ISyncMember>();
        }

        public int Count => _elements.Count;
        public IEnumerable Elements => _elements;

        public ISyncMember GetElement(int index) => _elements[index];
        public int IndexOfElement(ISyncMember element) => _elements.IndexOf(element);
        public ISyncMember AddElement() => throw new NotSupportedException();
        public void RemoveElement(int index) => throw new NotSupportedException();
        public ISyncMember MoveElementToIndex(int oldIndex, int newIndex) => throw new NotSupportedException();

        public event SyncListElementsEvent? ElementsAdded
        {
            add { }
            remove { }
        }

        public event SyncListElementsEvent? ElementsRemoved
        {
            add { }
            remove { }
        }

        public event SyncListElementsEvent? ElementsRemoving
        {
            add { }
            remove { }
        }

        public event SyncListEvent? ListCleared
        {
            add { }
            remove { }
        }
    }

    private sealed class FakeSyncDictionary : FakeSyncMemberBase, ISyncDictionary
    {
        private readonly List<KeyValuePair<object, ISyncMember>> _entries;
        private readonly Dictionary<object, ISyncMember> _lookup;

        public FakeSyncDictionary(string name, params KeyValuePair<object, ISyncMember>[] entries) : base(name)
        {
            _entries = entries?.ToList() ?? new List<KeyValuePair<object, ISyncMember>>();
            _lookup = _entries.ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        public IEnumerable<KeyValuePair<object, ISyncMember>> BoxedEntries => _entries;
        public IEnumerable<ISyncMember> Values => _entries.Select(entry => entry.Value);

        public ISyncMember TryGetMember(object key)
        {
            if (_lookup.TryGetValue(key, out ISyncMember? value) && value != null)
            {
                return value;
            }

            throw new KeyNotFoundException();
        }

        public event SyncDictionaryElementEvent? ElementAdded
        {
            add { }
            remove { }
        }

        public event SyncDictionaryElementEvent? ElementRemoved
        {
            add { }
            remove { }
        }
    }

    private sealed class FakeSyncArray : FakeSyncMemberBase, ISyncArray
    {
        private readonly object?[] _items;

        public FakeSyncArray(string name, params object?[] items) : base(name)
        {
            _items = items ?? Array.Empty<object?>();
        }

        public int Count => _items.Length;

        public object GetElement(int index) => _items[index]!;
    }
}