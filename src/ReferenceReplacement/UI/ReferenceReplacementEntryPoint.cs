using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementEntryPoint
{
    public static void Open(Slot? dialogSlot, Slot? suggestedRoot)
    {
        ReferenceReplacementDialogManager.Show(dialogSlot, suggestedRoot);
    }

    public static void OpenFromSlot(Slot? slot)
    {
        if (slot == null)
        {
            return;
        }

        Open(slot, slot);
    }
}
