using System;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ReferenceReplacement.UI;

namespace ReferenceReplacement.Patching;

[HarmonyPatch(typeof(DevTool), nameof(DevTool.GenerateMenuItems))]
internal static class DevToolMenuPatch
{
    private const string ButtonLabel = "Reference Replacement";

    private static void Postfix(DevTool __instance, InteractionHandler tool, ContextMenu menu)
    {
        if (__instance?.LocalUser == null || menu == null)
        {
            return;
        }

        LocaleString label = (LocaleString)ButtonLabel;
        colorX? accent = new colorX(0.18f, 0.65f, 0.85f, 1f);
        Uri icon = OfficialAssets.Graphics.Icons.General.Chainlink;
        ContextMenuItem item = menu.AddItem(in label, icon, in accent);

        if (item.Button == null)
        {
            return;
        }

        item.Button.LocalPressed += (_, __) =>
        {
            Slot? suggestedRoot = tool?.Grabber?.HolderSlot ?? tool?.LocalUserSpace;
            ReferenceReplacementDialogManager.Show(__instance.LocalUser, suggestedRoot);
        };
    }
}
