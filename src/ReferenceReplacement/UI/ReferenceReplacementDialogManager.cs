using System;
using System.Collections.Generic;
using Elements.Core;
using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementDialogManager
{
    public static void Show(Slot? dialogSlot, Slot? suggestedRoot)
    {
        User? localUser = dialogSlot?.World?.LocalUser ?? suggestedRoot?.World?.LocalUser;
        if (localUser == null)
        {
            dialogSlot?.Destroy();
            return;
        }

        dialogSlot?.Destroy();

        ReferenceReplacementDialog.Create(localUser, null, suggestedRoot);
    }

    public static void Unregister(ReferenceReplacementDialog dialog)
    {
    }

}
