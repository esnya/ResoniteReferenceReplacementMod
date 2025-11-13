using FrooxEngine;

namespace ReferenceReplacement.UI;

internal static class ReferenceReplacementDialogManager
{
    public static void Show(User? localUser)
    {
        if (localUser == null)
        {
            return;
        }

        ReferenceReplacementDialog.Create(localUser);
    }

    public static void Unregister(ReferenceReplacementDialog dialog)
    {
    }

}
