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

        User? localUser = creationSlot.World?.LocalUser;
        if (localUser == null)
        {
            creationSlot.Destroy();
            return;
        }

        try
        {
            ReferenceReplacementDialogManager.Show(localUser);
        }
        finally
        {
            creationSlot.Destroy();
        }
    }

}
