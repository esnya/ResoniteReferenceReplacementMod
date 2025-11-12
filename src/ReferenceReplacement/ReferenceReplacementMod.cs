using FrooxEngine;
using HarmonyLib;
using ReferenceReplacement.UI;
using ResoniteModLoader;

namespace ReferenceReplacement;

public class ReferenceReplacementMod : ResoniteMod
{
    public const string VersionTag = "0.1.0";
    private static readonly Harmony HarmonyInstance = new("com.c0dex.reference-replacement");
    private static bool _creationEntryRegistered;

    public override string Name => "ReferenceReplacement";
    public override string Author => "codex";
    public override string Version => VersionTag;
    public override string Link => "https://example.com/reference-replacement";

    public override void OnEngineInit()
    {
        HarmonyInstance.PatchAll();
        RegisterCreationEntry();
    }

    private static void RegisterCreationEntry()
    {
        if (_creationEntryRegistered)
        {
            return;
        }

        DevCreateNewForm.AddAction("Editor", "Reference Replacement (Mod)", slot =>
        {
            if (slot == null)
            {
                return;
            }

            User? user = slot.World?.LocalUser;
            if (user == null)
            {
                return;
            }

            ReferenceReplacementDialogManager.Show(user, slot);
        });

        _creationEntryRegistered = true;
    }
}
