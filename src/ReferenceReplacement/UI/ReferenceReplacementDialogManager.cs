using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementDialogManager
{
    private static readonly Dictionary<RefID, WeakReference<ReferenceReplacementDialog>> Dialogs = new();

    public static void Show(User user, Slot? suggestedRoot)
    {
        if (user == null)
        {
            return;
        }

        CleanupDeadEntries();

        if (Dialogs.TryGetValue(user.ReferenceID, out var weakReference))
        {
            if (weakReference.TryGetTarget(out var existing) && existing?.Slot != null && IsSlotAlive(existing.Slot))
            {
                existing.Focus();
                if (!existing.HasProcessRoot && suggestedRoot != null)
                {
                    existing.TrySetProcessRoot(suggestedRoot);
                }

                return;
            }

            Dialogs.Remove(user.ReferenceID);
        }

        Slot dialogSlot = user.LocalUserSpace.AddSlot("Reference Replacement Dialog");

        var dialog = dialogSlot.AttachComponent<ReferenceReplacementDialog>();
        dialog.Initialize(user, suggestedRoot);
        Dialogs[user.ReferenceID] = new WeakReference<ReferenceReplacementDialog>(dialog);
    }

    public static void Unregister(ReferenceReplacementDialog dialog)
    {
        RefID? keyToRemove = null;
        foreach (var (key, weak) in Dialogs)
        {
            if (!weak.TryGetTarget(out var entry) || entry == dialog)
            {
                keyToRemove = key;
                break;
            }
        }

        if (keyToRemove.HasValue)
        {
            Dialogs.Remove(keyToRemove.Value);
        }
    }

    private static void CleanupDeadEntries()
    {
        List<RefID> stale = new();
        foreach (var (key, weak) in Dialogs)
        {
            if (!weak.TryGetTarget(out var dialog) || dialog == null || !IsSlotAlive(dialog.Slot))
            {
                stale.Add(key);
            }
        }

        foreach (var key in stale)
        {
            Dialogs.Remove(key);
        }
    }

    private static bool IsSlotAlive(Slot? slot)
    {
        return slot != null && !slot.IsDestroyed && !slot.IsRemoved;
    }
}
