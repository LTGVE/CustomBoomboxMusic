using HarmonyLib;

[HarmonyPatch(typeof(HUDManager), "EnableChat_performed")]
public class HUDManager_OpenChatPatch {
    [HarmonyPrefix]
    public static void Prefix(HUDManager __instance) { 
        __instance.chatTextField.characterLimit=int.MaxValue;
    }
}