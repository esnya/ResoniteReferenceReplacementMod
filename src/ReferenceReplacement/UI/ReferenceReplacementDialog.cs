using System;
using System.Collections.Generic;
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
        _rootSlot.LocalScale = new float3(0.0005f, 0.0005f, 0.0005f);
        _rootSlot.PersistentSelf = false;
        _rootSlot.AttachComponent<ObjectRoot>();
        _rootSlot.Tag = "Developer";
    }

    private void BuildUI()
    {
        LocaleString title = (LocaleString)"Reference Replacement";
        UIBuilder frameBuilder = RadiantUI_Panel.SetupPanel(_rootSlot, title, new float2(1200f, 900f));
        RadiantUI_Constants.SetupEditorStyle(frameBuilder, extraPadding: true);
        frameBuilder.Style.MinHeight = 32f;
        frameBuilder.Style.MinWidth = 160f;

        Canvas? canvas = _rootSlot.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.MarkDeveloper();
            canvas.AcceptPhysicalTouch.Value = false;
        }

        List<RectTransform> columns = frameBuilder.SplitHorizontally(0.42f, 0.58f, 0.02f);
        BuildInputColumn(columns[0]);
        BuildAnalysisColumn(columns[1]);
    }

    private void BuildInputColumn(RectTransform column)
    {
        UIBuilder columnBuilder = CreateSection(column, RadiantUI_Constants.Neutrals.MID.SetA(0.9f));
        LocaleString heading = (LocaleString)"Inputs";
        columnBuilder.Text(in heading, size: 32, bestFit: false, alignment: Alignment.MiddleLeft);

        LocaleString hint = (LocaleString)"Pick the tree to scan, the SyncRef source, and the replacement target.";
        columnBuilder.Text(in hint, size: 24, bestFit: false, alignment: Alignment.TopLeft);
        columnBuilder.Spacer(6f);

        columnBuilder.ScrollArea();
        columnBuilder.VerticalLayout(6f, 12f);
        columnBuilder.FitContent(SizeFit.Disabled, SizeFit.MinSize);

        BuildReferenceEditors(columnBuilder);
        columnBuilder.NestOut();
    }

    private void BuildAnalysisColumn(RectTransform column)
    {
        UIBuilder columnBuilder = CreateSection(column, RadiantUI_Constants.Neutrals.MID.SetA(0.9f));
        columnBuilder.HorizontalHeader(56f, out RectTransform header, out RectTransform content);

        var headerBuilder = new UIBuilder(header);
        RadiantUI_Constants.SetupEditorStyle(headerBuilder, extraPadding: true);
        LocaleString heading = (LocaleString)"Analysis";
        headerBuilder.Text(in heading, size: 32, bestFit: false, alignment: Alignment.MiddleLeft);
        LocaleString detail = (LocaleString)"Review status and run replacements.";
        headerBuilder.Text(in detail, size: 24, bestFit: false, alignment: Alignment.MiddleLeft);

        var bodyBuilder = new UIBuilder(content);
        RadiantUI_Constants.SetupEditorStyle(bodyBuilder, extraPadding: true);
        bodyBuilder.HorizontalFooter(80f, out RectTransform footer, out RectTransform bodyContent);

        var statusBuilder = new UIBuilder(bodyContent);
        RadiantUI_Constants.SetupEditorStyle(statusBuilder, extraPadding: true);
        statusBuilder.ScrollArea();
        statusBuilder.VerticalLayout(6f, 12f);
        statusBuilder.FitContent(SizeFit.Disabled, SizeFit.MinSize);
        BuildStatusSection(statusBuilder);

        var footerBuilder = new UIBuilder(footer);
        RadiantUI_Constants.SetupEditorStyle(footerBuilder, extraPadding: true);
        BuildActionButtons(footerBuilder);

        columnBuilder.NestOut();
    }

    private static UIBuilder CreateSection(RectTransform target, colorX tint)
    {
        var builder = new UIBuilder(target.Slot);
        RadiantUI_Constants.SetupEditorStyle(builder, extraPadding: true);
        Image panel = builder.Panel(tint);
        panel.Sprite.Target = builder.Style.ButtonSprite;
        panel.NineSliceSizing.Value = builder.Style.NineSliceSizing;
        builder.NestInto(panel.RectTransform);
        builder.VerticalLayout(8f, 16f, Alignment.TopLeft);
        return builder;
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
        ui.Style.MinHeight = 48f;
        ui.Style.FlexibleWidth = 1f;

        LocaleString analyzeLabel = (LocaleString)"Analyze";
        ui.Button(in analyzeLabel).LocalPressed += (_, __) => Analyze(applyChanges: false);

        LocaleString replaceLabel = (LocaleString)"Replace";
        ui.Button(in replaceLabel).LocalPressed += (_, __) => Analyze(applyChanges: true);

        LocaleString closeLabel = (LocaleString)"Close";
        ui.Button(in closeLabel).LocalPressed += (_, __) => Close();
        ui.NestOut();
    }

    private void BuildStatusSection(UIBuilder ui)
    {
        LocaleString statusHeading = (LocaleString)"Status";
        ui.Text(in statusHeading, size: 28, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false);

        LocaleString statusContent = (LocaleString)"Waiting for analysis.";
        _statusText = ui.Text(in statusContent, size: 24, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false);

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
