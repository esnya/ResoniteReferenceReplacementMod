using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementEntryPoint
{
    public static void Open(User? user, Slot? dialogSlot, Slot? suggestedRoot)
    {
        if (user == null)
        {
            return;
        }

        ReferenceReplacementDialogManager.Show(user, dialogSlot, suggestedRoot);
    }

    public static void OpenFromSlot(Slot? slot)
    {
        if (slot == null)
        {
            return;
        }

        Open(slot.World?.LocalUser, slot, slot);
    }
}
