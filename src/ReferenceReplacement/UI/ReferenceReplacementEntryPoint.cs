using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementEntryPoint
{
    public static void OpenFromSlot(Slot? creationSlot)
    {
        if (creationSlot == null)
        {
            return;
        }

        ReferenceReplacementDialogManager.Show(creationSlot);
    }

}