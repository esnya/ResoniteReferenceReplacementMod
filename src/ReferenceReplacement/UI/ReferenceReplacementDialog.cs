using System;
using System.Collections.Generic;
using System.Linq;

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

    private ReferenceReplacementDialog(User owner, Slot dialogSlot)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _rootSlot = dialogSlot ?? throw new ArgumentNullException(nameof(dialogSlot));
        _rootSlot.Destroyed += OnSlotDestroyed;
        ClearSlot(_rootSlot);

        (_processRootRef, _sourceRef, _targetRef) = CreateReferenceFields();
        ClearInputs();

        ConfigureRootSlot();
        BuildUI();
        UpdateStatus("Select inputs to begin.");
        Focus();
        RepositionFor(owner);
    }

    public static ReferenceReplacementDialog Create(User owner, Slot dialogSlot)
    {
        return new ReferenceReplacementDialog(owner, dialogSlot);
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

    public void RepositionFor(User? user)
    {
        if (user == null || !IsAlive)
        {
            return;
        }

        PrepareRootSlot(user);
        _rootSlot.PositionInFrontOfUser(float3.Backward);
        if (user.LocalUserRoot != null)
        {
            _rootSlot.GlobalPosition += _rootSlot.Right * 0.5f * user.LocalUserRoot.GlobalScale;
        }
        _rootSlot.PointAtUserHead(float3.Backward, verticalAxisOnly: true);
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
        _rootSlot.PersistentSelf = false;
        if (_rootSlot.GetComponent<ObjectRoot>() == null)
        {
            _rootSlot.AttachComponent<ObjectRoot>();
        }
        _rootSlot.Tag = "Developer";
    }

    private void BuildUI()
    {
        LocaleString title = (LocaleString)"Reference Replacement";
        UIBuilder frameBuilder = RadiantUI_Panel.SetupPanel(_rootSlot, title, new float2(1200f, 900f));
        _rootSlot.LocalScale *= 0.0005f;
        RadiantUI_Constants.SetupEditorStyle(frameBuilder, extraPadding: true);
        frameBuilder.Style.MinHeight = 32f;
        frameBuilder.Style.MinWidth = 160f;

        Canvas? canvas = _rootSlot.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.MarkDeveloper();
            canvas.AcceptPhysicalTouch.Value = false;
        }

        BuildPanel(frameBuilder);
    }

    private void BuildPanel(UIBuilder ui)
    {
        ui.VerticalLayout(10f, 20f, Alignment.TopLeft);

        BuildReferenceEditors(ui);
        BuildActionButtons(ui);
        BuildStatusSection(ui);
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
        ui.Text(in labelString);
        ui.RefMemberEditor(referenceField);
    }

    private void BuildActionButtons(UIBuilder ui)
    {
        ui.HorizontalLayout(8f);

        LocaleString analyzeLabel = (LocaleString)"Analyze";
        ui.Button(in analyzeLabel).LocalPressed += (_, __) => Analyze(applyChanges: false);

        LocaleString replaceLabel = (LocaleString)"Replace";
        ui.Button(in replaceLabel).LocalPressed += (_, __) => Analyze(applyChanges: true);

        ui.NestOut();
    }

    private void BuildStatusSection(UIBuilder ui)
    {
        LocaleString statusHeading = (LocaleString)"Status";
        ui.Text(in statusHeading);

        LocaleString statusContent = (LocaleString)"Waiting for analysis.";
        _statusText = ui.Text(in statusContent);

        LocaleString detail = (LocaleString)string.Empty;
        _detailText = ui.Text(in detail);
    }

    private void Analyze(bool applyChanges)
    {
        if (!TryResolveInputs(out Slot root, out IWorldElement source, out IWorldElement target, out string errorMessage))
        {
            UpdateStatus(errorMessage);
            return;
        }

        if (!applyChanges)
        {
            ReferenceScanResult scanResult = ReferenceScanner.Scan(root, source, target);
            if (scanResult.Matches.Count == 0)
            {
                UpdateStatus("No references found in the selected root.");
                return;
            }

            UpdateStatus($"Found {scanResult.Matches.Count} references (skipped {scanResult.IncompatibleCount}).", scanResult);
            return;
        }

        ApplyReplacement(root, source, target);
    }

    private bool TryResolveInputs(out Slot root, out IWorldElement source, out IWorldElement target, out string message)
    {
        root = null!;
        source = null!;
        target = null!;

        Slot? rootCandidate = GetProcessRootSlot();
        IWorldElement? sourceCandidate = _sourceRef.Target as IWorldElement;
        IWorldElement? targetCandidate = _targetRef.Target as IWorldElement;

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

    private void ApplyReplacement(Slot root, IWorldElement source, IWorldElement target)
    {
        ReferenceScanResult scanResult = ReferenceScanner.Scan(root, source, target);
        if (scanResult.Matches.Count == 0)
        {
            UpdateStatus("No references found in the selected root.");
            return;
        }

        World? world = root.World;
        if (world == null)
        {
            UpdateStatus("Root world is unavailable.");
            return;
        }

        LocaleString description = (LocaleString)$"Reference Replacement ({scanResult.Matches.Count})";
        world.BeginUndoBatch(description);
        try
        {
            foreach (SyncReferenceMatch match in scanResult.Matches)
            {
                ISyncRef syncRef = match.SyncRef;
                IWorldElement? previous = syncRef.Target;
                if (previous != null && previous.ReferenceID == target.ReferenceID)
                {
                    continue;
                }

                syncRef.CreateUndoPoint(forceNew: true);
                syncRef.Target = target;
            }
        }
        finally
        {
            world.EndUndoBatch();
        }

        UpdateStatus($"Replaced {scanResult.Matches.Count} references (skipped {scanResult.IncompatibleCount}).", scanResult);
    }

    private void UpdateStatus(string message, ReferenceScanResult? detail = null)
    {
        if (_statusText != null)
        {
            _statusText.Content.Value = message;
        }

        if (_detailText != null)
        {
            string text = detail == null
                ? string.Empty
                : $"Last hit: {detail.LastHitPath ?? "(n/a)"}\nVisited members: {detail.VisitedMembers}\nIncompatible refs: {detail.IncompatibleCount}";
            _detailText.Content.Value = text;
        }
    }

    private static void ClearSlot(Slot slot)
    {
        foreach (Slot child in slot.Children.ToArray())
        {
            child.Destroy();
        }

        foreach (Component component in slot.Components.ToArray())
        {
            component.Destroy();
        }
    }

    private void ClearInputs()
    {
        _processRootRef.Target = null!;
        _sourceRef.Target = null!;
        _targetRef.Target = null!;
    }

    private void PrepareRootSlot(User owner)
    {
        Slot? parent = owner.LocalUserSpace;
        if (parent != null && _rootSlot.Parent != parent)
        {
            _rootSlot.SetParent(parent, false);
        }

        _rootSlot.LocalPosition = float3.Zero;
        _rootSlot.LocalRotation = floatQ.Identity;
    }
}