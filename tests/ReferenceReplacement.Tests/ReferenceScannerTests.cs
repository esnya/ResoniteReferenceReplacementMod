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
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var syncRef = new FakeSyncRef("SlotRef", typeof(FakeWorldElement), source);
        var blueprint = HierarchyBlueprint.Create("Root", new[] { syncRef });

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        var match = Assert.Single(result.Matches);
        Assert.Equal(syncRef, match.SyncRef);
        Assert.Equal("Root.SlotRef", match.Path);
        Assert.Equal(1, result.VisitedMembers);
        Assert.Equal(0, result.IncompatibleCount);
    }

    [Fact]
    public void ScanCountsIncompatibleTargets()
    {
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var syncRef = new FakeSyncRef("SlotRef", typeof(SpecialWorldElement), source);
        var blueprint = HierarchyBlueprint.Create("Root", new[] { syncRef });

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        Assert.Empty(result.Matches);
        Assert.Equal(1, result.IncompatibleCount);
        Assert.Equal(1, result.VisitedMembers);
    }

    [Fact]
    public void ScanTraversesNestedEnumerables()
    {
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var nestedRef = new FakeSyncRef("Nested", typeof(FakeWorldElement), source);
        var enumerableMember = new FakeSyncEnumerable("Collection", nestedRef);
        var blueprint = HierarchyBlueprint.Create(
            "Root",
            new ISyncMember[] { enumerableMember },
            Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        var match = Assert.Single(result.Matches);
        Assert.Equal("Root.Collection[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncListElements()
    {
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var nestedRef = new FakeSyncRef("Nested", typeof(FakeWorldElement), source);
        var syncList = new FakeSyncList("List", nestedRef);
        var blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { syncList }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        var match = Assert.Single(result.Matches);
        Assert.Equal("Root.List.Elements[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncDictionaryValues()
    {
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var nestedRef = new FakeSyncRef("Nested", typeof(FakeWorldElement), source);
        var dictionary = new FakeSyncDictionary("Dict", new KeyValuePair<object, ISyncMember>("Key", nestedRef));
        var blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { dictionary }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        var match = Assert.Single(result.Matches);
        Assert.Equal("Root.Dict.BoxedEntries[0]", match.Path);
    }

    [Fact]
    public void ScanTraversesSyncArrayItems()
    {
        var source = new FakeWorldElement("Source");
        var replacement = new FakeWorldElement("Replacement");
        var nestedRef = new FakeSyncRef("Nested", typeof(FakeWorldElement), source);
        var arrayMember = new FakeSyncArray("Array", nestedRef);
        var blueprint = HierarchyBlueprint.Create("Root", new ISyncMember[] { arrayMember }, Array.Empty<HierarchyBlueprint>());

        ReferenceScanResult result = ReferenceScanner.Scan(blueprint, source, replacement);

        var match = Assert.Single(result.Matches);
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

        public void ChildChanged(IWorldElement child) { }
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

        public void Initialize(World world, IWorldElement element) { }
        public void Dispose() { }
        public void CopyValues(ISyncMember other) { }
        public void CopyValues(ISyncMember other, Action<ISyncMember, ISyncMember> copier) { }
        public void EndInitPhase() { }
        public void Link(ILinkRef link) { }
        public void InheritLink(ILinkRef link) { }
        public void ReleaseLink(ILinkRef link) { }
        public void ReleaseInheritedLink(ILinkRef link) { }
        public void ChildChanged(IWorldElement child) { }
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
            return _lookup.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
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
