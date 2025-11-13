using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using ReferenceReplacement.Logic;

namespace ReferenceReplacement.UI;

public sealed class ReferenceReplacementDialog
{

    private readonly User _owner;
    private readonly Slot _rootSlot;
    private readonly ISyncRef _processRootRef;
    private readonly ISyncRef _sourceRef;
    private readonly ISyncRef _targetRef;

    private Text? _statusText;
    private Text? _detailText;
    private bool _disposed;

    private ReferenceReplacementDialog(User owner, Slot? suggestedRoot)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Slot? userSpace = owner.LocalUserSpace ?? throw new InvalidOperationException("User space is unavailable.");

        _rootSlot = userSpace.AddSlot("Reference Replacement Dialog");
        _rootSlot.Destroyed += OnSlotDestroyed;

        (_processRootRef, _sourceRef, _targetRef) = CreateReferenceFields();

        ConfigureRootSlot();
        BuildUI();
        InitializeInputs(suggestedRoot);
        UpdateStatus("Select inputs to begin.");
        Focus();
    }

    public static ReferenceReplacementDialog Create(User owner, Slot? suggestedRoot)
    {
        return new ReferenceReplacementDialog(owner, suggestedRoot);
    }

    public bool HasProcessRoot => GetProcessRootSlot() != null;

    public bool IsAlive => !_disposed && !_rootSlot.IsDestroyed && !_rootSlot.IsRemoved;

    public void Focus()
    {
        if (IsAlive)
        {
            _rootSlot.OrderOffset = DateTime.UtcNow.Ticks;
        }
    }

    public void TrySetProcessRoot(Slot? slot)
    {
        if (slot == null || _processRootRef.Target != null)
        {
            return;
        }

        _processRootRef.Target = slot;
    }

    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _rootSlot.Destroyed -= OnSlotDestroyed;

        if (!_rootSlot.IsDestroyed && !_rootSlot.IsRemoved)
        {
            _rootSlot.Destroy();
        }

        ReferenceReplacementDialogManager.Unregister(this);
    }

    private void InitializeInputs(Slot? suggestedRoot)
    {
        if (suggestedRoot != null)
        {
            _processRootRef.Target = suggestedRoot;
        }
        else if (_owner.LocalUserSpace != null)
        {
            _processRootRef.Target ??= _owner.LocalUserSpace;
        }
    }

    private (ISyncRef processRoot, ISyncRef source, ISyncRef target) CreateReferenceFields()
    {
        return (CreateReferenceProxy(), CreateReferenceProxy(), CreateReferenceProxy());
    }

    private ISyncRef CreateReferenceProxy()
    {
        var proxy = _rootSlot.AttachComponent<ReferenceProxy>();
        proxy.Persistent = false;
        return ExtractSyncRef(proxy);
    }

    private static ISyncRef ExtractSyncRef(ReferenceProxy proxy)
    {
        foreach (ISyncMember member in proxy.SyncMembers)
        {
            if (member is ISyncRef syncRef)
            {
                return syncRef;
            }
        }

        throw new MissingMemberException(typeof(ReferenceProxy).FullName, "Reference");
    }

    private void OnSlotDestroyed(IDestroyable _)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReferenceReplacementDialogManager.Unregister(this);
    }

    private void ConfigureRootSlot()
    {
        _rootSlot.OrderOffset = DateTime.UtcNow.Ticks;
        _rootSlot.LocalScale = new float3(1f, 1f, 1f);
        _rootSlot.PersistentSelf = false;

        _rootSlot.AttachComponent<Canvas>();

        var rectTransform = _rootSlot.AttachComponent<RectTransform>();
        rectTransform.AnchorMin.Value = new float2(0.5f, 0.5f);
        rectTransform.AnchorMax.Value = new float2(0.5f, 0.5f);
        rectTransform.OffsetMin.Value = new float2(-450f, -260f);
        rectTransform.OffsetMax.Value = new float2(450f, 260f);
    }

    private void BuildUI()
    {
        var ui = new UIBuilder(_rootSlot);
        ui.Style.MinHeight = 28f;
        ui.Style.MinWidth = 120f;

        BuildPanel(ui);
    }

    private void BuildPanel(UIBuilder ui)
    {
        colorX panelTint = new(0.08f, 0.08f, 0.1f, 0.92f);
        var panel = ui.Panel(in panelTint, zwrite: false);
        ui.NestInto(panel.RectTransform);
        ui.VerticalLayout(10f, 20f, Alignment.TopLeft);

        BuildHeader(ui);
        BuildReferenceEditors(ui);
        BuildActionButtons(ui);
        BuildStatusSection(ui);

        ui.NestOut();
    }

    private static void BuildHeader(UIBuilder ui)
    {
        LocaleString title = (LocaleString)"Reference Replacement";
        ui.Text(in title, size: 36, bestFit: false, alignment: Alignment.MiddleLeft, parseRTF: false);

        LocaleString subtitle = (LocaleString)"Scan a slot tree and replace every SyncRef that points to your source.";
        ui.Text(in subtitle, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);
        ui.Spacer(6f);
    }

    private void BuildReferenceEditors(UIBuilder ui)
    {
        BuildReferenceEditor(ui, "Process root (Slot)", _processRootRef);
        BuildReferenceEditor(ui, "Source reference", _sourceRef);
        BuildReferenceEditor(ui, "Replacement reference", _targetRef);
        ui.Spacer(8f);
    }

    private static void BuildReferenceEditor(UIBuilder ui, string label, ISyncRef referenceField)
    {
        LocaleString labelString = (LocaleString)label;
        ui.Text(in labelString, bestFit: false, alignment: Alignment.MiddleLeft, parseRTF: false, nullContent: string.Empty);

        ui.PushStyle();
        ui.Style.MinHeight = 32f;
        ui.RefMemberEditor(referenceField);
        ui.PopStyle();
    }

    private void BuildActionButtons(UIBuilder ui)
    {
        ui.HorizontalLayout(8f);

        LocaleString analyzeLabel = (LocaleString)"Analyze";
        ui.Button(in analyzeLabel).LocalPressed += (_, __) => Analyze(applyChanges: false);

        LocaleString replaceLabel = (LocaleString)"Replace";
        ui.Button(in replaceLabel).LocalPressed += (_, __) => Analyze(applyChanges: true);

        LocaleString closeLabel = (LocaleString)"Close";
        ui.Button(in closeLabel).LocalPressed += (_, __) => Close();

        ui.NestOut();
        ui.Spacer(6f);
    }

    private void BuildStatusSection(UIBuilder ui)
    {
        LocaleString statusHeading = (LocaleString)"Status";
        ui.Text(in statusHeading, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);

        LocaleString statusContent = (LocaleString)"Waiting for analysis.";
        _statusText = ui.Text(in statusContent, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);

        LocaleString detail = (LocaleString)string.Empty;
        _detailText = ui.Text(in detail, size: 24, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false);
    }

    private void Analyze(bool applyChanges)
    {
        if (!TryResolveInputs(out var root, out var source, out var target, out var errorMessage))
        {
            UpdateStatus(errorMessage);
            return;
        }

        ReferenceScanResult scanResult = ReferenceScanner.Scan(root, source, target);
        if (scanResult.Matches.Count == 0)
        {
            UpdateStatus("No references found in the selected root.");
            return;
        }

        if (!applyChanges)
        {
            UpdateStatus($"Found {scanResult.Matches.Count} references (skipped {scanResult.IncompatibleCount}).", scanResult);
            return;
        }

        ApplyReplacement(scanResult, root, target);
    }

    private bool TryResolveInputs(out Slot root, out IWorldElement source, out IWorldElement target, out string message)
    {
        root = null!;
        source = null!;
        target = null!;

        Slot? rootCandidate = GetProcessRootSlot();
        IWorldElement? sourceCandidate = _sourceRef.Target;
        IWorldElement? targetCandidate = _targetRef.Target;

        if (rootCandidate == null)
        {
            message = "Process root is required.";
            return false;
        }

        if (sourceCandidate == null)
        {
            message = "Source reference is required.";
            return false;
        }

        if (targetCandidate == null)
        {
            message = "Replacement reference is required.";
            return false;
        }

        root = rootCandidate;
        source = sourceCandidate;
        target = targetCandidate;

        if (ReferenceEquals(source, target) || source.ReferenceID == target.ReferenceID)
        {
            message = "Source and replacement are identical.";
            return false;
        }

        if (root.World == null || source.World == null || target.World == null)
        {
            message = "All references must exist in-world.";
            return false;
        }

        if (root.World != source.World || root.World != target.World)
        {
            message = "Root, source, and replacement must belong to the same world.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private Slot? GetProcessRootSlot()
    {
        return _processRootRef.Target as Slot;
    }

    private void ApplyReplacement(ReferenceScanResult scanResult, Slot root, IWorldElement target)
    {
        World? world = root.World;
        if (world == null)
        {
            UpdateStatus("Root world is unavailable.");
            return;
        }

        LocaleString description = (LocaleString)$"Reference Replacement ({scanResult.Matches.Count})";
        UndoManagerExtensions.BeginUndoBatch(world, description);
        try
        {
            foreach (SyncReferenceMatch match in scanResult.Matches)
            {
                match.SyncRef.Target = target;
            }
        }
        finally
        {
            UndoManagerExtensions.EndUndoBatch(world);
        }

        UpdateStatus($"Replaced {scanResult.Matches.Count} references. Skipped {scanResult.IncompatibleCount} incompatible entries.", scanResult);
    }

    private void UpdateStatus(string message, ReferenceScanResult? scanResult = null)
    {
        LocaleString status = (LocaleString)message;
        if (_statusText != null)
        {
            _statusText.LocaleContent = status;
        }

        if (_detailText != null)
        {
            string details = scanResult == null
                ? string.Empty
                : $"Visited {scanResult.VisitedMembers} sync members. Last path: {scanResult.LastHitPath ?? "n/a"}";
            LocaleString detailString = (LocaleString)details;
            _detailText.LocaleContent = detailString;
        }
    }
}
