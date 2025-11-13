using System;
using FrooxEngine;
using HarmonyLib;
using ReferenceReplacement.UI;
using ResoniteModLoader;
#if USE_RESONITE_HOT_RELOAD_LIB
using ResoniteHotReloadLib;
#endif

namespace ReferenceReplacement;

public class ReferenceReplacementMod : ResoniteMod
{
    public const string VersionTag = "0.1.0";
    private const string HarmonyId = "com.nekometer.esnya.reference-replacement";
    private const string CreationMenuCategory = "Editor";
    private const string CreationMenuLabel = "Reference Replacement (Mod)";
    private static readonly Harmony HarmonyInstance = new(HarmonyId);
    private static bool _creationEntryRegistered;

    public override string Name => "ReferenceReplacement";
    public override string Author => "esnya";
    public override string Version => VersionTag;
    public override string Link => "https://github.com/esnya/ResoniteReferenceReplacementMod";

    public override void OnEngineInit()
    {
        InitializeMod(this);
    }

#if USE_RESONITE_HOT_RELOAD_LIB
    public static void BeforeHotReload()
    {
        HarmonyInstance.UnpatchAll(HarmonyId);
        HotReloader.RemoveMenuOption(CreationMenuCategory, CreationMenuLabel);
        _creationEntryRegistered = false;
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        InitializeMod(modInstance);
    }
#endif

    private static void InitializeMod(ResoniteMod modInstance)
    {
        ArgumentNullException.ThrowIfNull(modInstance);
#if USE_RESONITE_HOT_RELOAD_LIB
        HotReloader.RegisterForHotReload(modInstance);
#endif
        HarmonyInstance.PatchAll();
        RegisterCreationEntry();
    }

    private static void RegisterCreationEntry()
    {
        if (_creationEntryRegistered)
        {
            return;
        }

        DevCreateNewForm.AddAction(CreationMenuCategory, CreationMenuLabel, ReferenceReplacementEntryPoint.OpenFromSlot);

        _creationEntryRegistered = true;
    }
}
