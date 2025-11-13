using System;

using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementDialogManager
{
    public static void Show(Slot creationSlot)
    {
        if (creationSlot == null)
        {
            return;
        }

        User? localUser = creationSlot.World?.LocalUser;
        if (localUser == null)
        {
            creationSlot.Destroy();
            return;
        }

        ReferenceReplacementDialog.Create(localUser, creationSlot);
    }

    public static void Unregister(ReferenceReplacementDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
    }

}