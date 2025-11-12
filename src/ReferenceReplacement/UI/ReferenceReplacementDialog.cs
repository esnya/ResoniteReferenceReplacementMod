using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using FrooxEngine.Undo;
using ReferenceReplacement.Logic;

namespace ReferenceReplacement.UI;

public sealed class ReferenceReplacementDialog : Component
{
    public readonly SyncRef<Slot> ProcessRoot = new();
    public readonly SyncRef<IWorldElement> SourceElement = new();
    public readonly SyncRef<IWorldElement> TargetElement = new();

    private readonly SyncRef<Text> _statusText = new();
    private readonly SyncRef<Text> _detailText = new();

    public void Initialize(User owner, Slot? suggestedRoot)
    {
        BuildUI();
        if (suggestedRoot != null)
        {
            ProcessRoot.Target = suggestedRoot;
        }
        else if (owner?.LocalUserSpace != null)
        {
            ProcessRoot.Target ??= owner.LocalUserSpace;
        }
        UpdateStatus("Select inputs to begin.");
        Focus();
    }

    public bool HasProcessRoot => ProcessRoot.Target != null;

    public void TrySetProcessRoot(Slot slot)
    {
        if (slot == null)
        {
            return;
        }

        if (ProcessRoot.Target == null)
        {
            ProcessRoot.Target = slot;
        }
    }

    public void Focus()
    {
        if (Slot != null)
        {
            Slot.OrderOffset = DateTime.UtcNow.Ticks;
        }
    }

    public void Close()
    {
        Slot?.Destroy();
    }

    protected override void OnDestroy()
    {
        ReferenceReplacementDialogManager.Unregister(this);
        base.OnDestroy();
    }

    private void BuildUI()
    {
        if (Slot == null)
        {
            return;
        }

        Slot.OrderOffset = DateTime.UtcNow.Ticks;
        Slot.LocalScale = new float3(1f, 1f, 1f);

        Slot.AttachComponent<Canvas>();

        var rectTransform = Slot.AttachComponent<RectTransform>();
        rectTransform.AnchorMin.Value = new float2(0.5f, 0.5f);
        rectTransform.AnchorMax.Value = new float2(0.5f, 0.5f);
        rectTransform.OffsetMin.Value = new float2(-450f, -260f);
        rectTransform.OffsetMax.Value = new float2(450f, 260f);

        var ui = new UIBuilder(Slot);
        ui.Style.MinHeight = 28f;
        ui.Style.MinWidth = 120f;

        colorX panelTint = new(0.08f, 0.08f, 0.1f, 0.92f);
        var panel = ui.Panel(in panelTint, zwrite: false);
        ui.NestInto(panel.RectTransform);
        ui.VerticalLayout(10f, 20f, Alignment.TopLeft);

        LocaleString title = (LocaleString)"Reference Replacement";
        ui.Text(in title, size: 36, bestFit: false, alignment: Alignment.MiddleLeft, parseRTF: false);

        LocaleString subtitle = (LocaleString)"Scan a slot tree and replace every SyncRef that points to your source.";
        ui.Text(in subtitle, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);

        AddReferenceEditor(ui, "Process root (Slot)", ProcessRoot);
        AddReferenceEditor(ui, "Source reference", SourceElement);
        AddReferenceEditor(ui, "Replacement reference", TargetElement);

        ui.Spacer(8f);

        ui.HorizontalLayout(8f);
        LocaleString analyzeLabel = (LocaleString)"Analyze";
        var analyzeButton = ui.Button(in analyzeLabel);
        analyzeButton.LocalPressed += (_, __) => Analyze(applyChanges: false);

        LocaleString replaceLabel = (LocaleString)"Replace";
        var replaceButton = ui.Button(in replaceLabel);
        replaceButton.LocalPressed += (_, __) => Analyze(applyChanges: true);

        LocaleString closeLabel = (LocaleString)"Close";
        var closeButton = ui.Button(in closeLabel);
        closeButton.LocalPressed += (_, __) => Close();
        ui.NestOut();

        ui.Spacer(4f);
        LocaleString statusHeading = (LocaleString)"Status";
        ui.Text(in statusHeading, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);
        LocaleString statusContent = (LocaleString)"Waiting for analysis.";
        var statusText = ui.Text(in statusContent, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false, nullContent: string.Empty);
        _statusText.Target = statusText;

        LocaleString detail = (LocaleString)string.Empty;
        var detailText = ui.Text(in detail, size: 24, bestFit: false, alignment: Alignment.TopLeft, parseRTF: false);
        _detailText.Target = detailText;

        ui.NestOut();
    }

    private void AddReferenceEditor(UIBuilder ui, string label, ISyncRef referenceField)
    {
        LocaleString labelString = (LocaleString)label;
        ui.Text(in labelString, bestFit: false, alignment: Alignment.MiddleLeft, parseRTF: false, nullContent: string.Empty);
        ui.PushStyle();
        ui.Style.MinHeight = 32f;
        ui.RefMemberEditor(referenceField);
        ui.PopStyle();
    }

    private void Analyze(bool applyChanges)
    {
        if (!TryResolveInputs(out var root, out var source, out var target, out var errorMessage))
        {
            UpdateStatus(errorMessage);
            return;
        }

        ReferenceScanResult scanResult = ReferenceScanner.Scan(root, source, target);
        if (scanResult.MatchingRefs.Count == 0)
        {
            UpdateStatus("No references found in the selected root.");
            return;
        }

        if (!applyChanges)
        {
            UpdateStatus($"Found {scanResult.MatchingRefs.Count} references (skipped {scanResult.IncompatibleCount}).", scanResult);
            return;
        }

        ApplyReplacement(scanResult, root, target);
    }

    private bool TryResolveInputs(out Slot root, out IWorldElement source, out IWorldElement target, out string message)
    {
        root = ProcessRoot.Target;
        source = SourceElement.Target;
        target = TargetElement.Target;

        if (root == null)
        {
            message = "Process root is required.";
            return false;
        }

        if (source == null)
        {
            message = "Source reference is required.";
            return false;
        }

        if (target == null)
        {
            message = "Replacement reference is required.";
            return false;
        }

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

    private void ApplyReplacement(ReferenceScanResult scanResult, Slot root, IWorldElement target)
    {
        World? world = root.World;
        if (world == null)
        {
            UpdateStatus("Root world is unavailable.");
            return;
        }

        LocaleString description = (LocaleString)$"Reference Replacement ({scanResult.MatchingRefs.Count})";
        UndoManagerExtensions.BeginUndoBatch(world, description);
        try
        {
            foreach (ReferenceHit hit in scanResult.MatchingRefs)
            {
                hit.SyncRef.Target = target;
            }
        }
        finally
        {
            UndoManagerExtensions.EndUndoBatch(world);
        }

        UpdateStatus($"Replaced {scanResult.MatchingRefs.Count} references. Skipped {scanResult.IncompatibleCount} incompatible entries.", scanResult);
    }

    private void UpdateStatus(string message, ReferenceScanResult? scanResult = null)
    {
        LocaleString status = (LocaleString)message;
        if (_statusText.Target != null)
        {
            _statusText.Target.LocaleContent = status;
        }

        if (_detailText.Target != null)
        {
            string details = scanResult == null
                ? string.Empty
                : $"Visited {scanResult.VisitedMembers} sync members. Last path: {scanResult.LastHitPath ?? "n/a"}";
            LocaleString detailString = (LocaleString)details;
            _detailText.Target.LocaleContent = detailString;
        }
    }
}
