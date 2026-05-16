using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CustomBoomboxMusic
{

    [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
    [HarmonyWrapSafe]

    public class BoomboxCommand 
    {


        public static MethodInfo GrabbaleObject_ActivateItemRpc =typeof(GrabbableObject).GetMethod("ActivateItemRpc", BindingFlags.Instance | BindingFlags.NonPublic);
        public static string error;
        [HarmonyPrefix]
        public static bool Prefix(HUDManager __instance)
        {
            __instance.chatTextField.characterLimit = int.MaxValue;
            bool isCommandHeader = __instance.chatTextField.text.StartsWith("!")&& __instance.chatTextField.text.StartsWith("!boombox")&& __instance.chatTextField.text.Length > "!boombox ".Length;
            if (!isCommandHeader)
                return true;
            var args = __instance.chatTextField.text.RemoveStartWith("!boombox ").ToCommandLineArgs();

            error = "";

            var command = args.Length > 0 ? args[0] : null;
            GrabbableObject boomboxObject = GameNetworkManager.Instance?.localPlayerController?.currentlyHeldObjectServer;
            BoomboxItem boombox = null;
            var hasBoombox = boomboxObject!=null&& boomboxObject.gameObject.TryGetComponent(out boombox);
            


                switch (command)
            {
                case "reload" or "r":
                    HUDManager.Instance.DisplayTip("Info", "Reloading...");
                    AudioManager.Reload();
                    HUDManager.Instance.DisplayTip("Info",
                        $"Done reloading, found {a(AudioManager.AudioClips.Count)}"
                    );
                    break;
                case "version" or "v" or null:
                    HUDManager.Instance.DisplayTip("Info",
                        $"{MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION}\n{a(AudioManager.AudioClips.Count)} loaded"
                    );
                    break;
                case "list" or "l":
                    var clips = AudioManager.AudioClips.OrderBy(i => i.Name).ToList();
                    var count = clips.Count;
                    HUDManager.Instance.DisplayTip("Info",
                        count == 0
                            ? $"{a(count)} loaded"
                            : $"{a(count)} loaded:\n> {clips.Join(i => i.Name, "\n> ")}"
                    );
                    CustomBoomboxMusic.Logger.LogInfo($"Listing loaded tracks ({count}):");
                    foreach (var clip in clips)
                        CustomBoomboxMusic.Logger.LogInfo(
                            $"> {clip.Name} - {clip.FilePath} (CRC32: {clip.Crc})"
                        );
                    break;
                case "play" or "p":
                    error = "Invalid arguments";
                    if (args.Length < 2)
                    {
                        __instance.localPlayer.isTypingChat = false;
                        break;
                    }
                    error = "You need to be holding a boombox";

                    if (!hasBoombox)
                    {
                        __instance.localPlayer.isTypingChat = false;
                        break;
                    }
                    error = "Track could not be found";
                    return Play(args[1..].Join(null, " "), boombox);
                case "nlink" or "nurl":
                    var url =args.Length > 1? args[1] : null;
                    try
                    {
                        if (url == null)
                        {
                            error = "Invalid arguments";
                            break;
                        }
                        if (!hasBoombox)
                        {
                            error = "You need to be holding a boombox";
                            __instance.localPlayer.isTypingChat = false;
                            break;
                        }

                        var urlData = URLProcess.ProcessURL(url);
                        var query = urlData.queryData;
                        var id = query.FirstOrDefault(i => i.key == "id").value;
                        ModNetworkBehaviour.Instance?.DownloadSong_Rpc(id, boombox.NetworkObject);
                    }
                    catch (Exception e) { 
                        Debug.LogException(e);
                    }

                    break;
                case "dlink" or "durl" or "dl": 
                    var durl = args.Length > 1 ? args[1] : null;
                    try
                    {
                        if (durl == null)
                        {
                            error = "Invalid arguments";
                            break;
                        }
                        if (!hasBoombox)
                        {
                            error = "You need to be holding a boombox";
                            __instance.localPlayer.isTypingChat = false;
                            break;
                        }
                        __instance.AddTextToChatOnServer("Your song has been started downloading.");
                        ModNetworkBehaviour.Instance?.DownloadDirectLinkSong_Rpc(durl, boombox.NetworkObject);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }

                    break;

                case "bypassReverb" or "br":
                    if (args.Length < 2)
                    {
                        error = "Invalid arguments";
                        break;
                    }
                    if (!hasBoombox)
                    {

                        error = "You need to be holding a boombox";
                        __instance.localPlayer.isTypingChat = false;
                        break;
                    }
                    var bypass = bool.Parse(args[1]);
                    ModNetworkBehaviour.Instance?.BoomBoxBypassReverb_Rpc(bypass, boombox.NetworkObject);
                    __instance.AddTextToChatOnServer("Changed bypass reverb.");
                    break;
                default:
                    HUDManager.Instance.DisplayTip("Warn",
                        "Invalid subcommand, use /help for usage information",
                        true
                    );
                    break;
            }
            if (isCommandHeader)
            {
                __instance.chatTextField.text = "";
                return true;
            }
            return true;

            string a(int count) => count == 0 ? "No tracks" : $"{count} track{b(count)}";
            string b(int count) => count == 1 ? string.Empty : "s";
        }


        private static bool Play(string identifier, BoomboxItem boombox)
        {
            var files = AudioManager.AudioClips;
            if (CustomBoomboxMusic.Instance.IncludeVanilla || files.Count == 0)
                files = files.Concat(AudioManager.VanillaAudioClips(boombox)).ToList();
            List<AudioFile?> clips = new() ;
            if (uint.TryParse(identifier, out var crc))
                if (AudioManager.TryGetCrc(crc, out var clip))
                    clips.Add(clip);
            if (clips.Count == 0)
                clips.AddRange(
                    files.Where(i =>
                        string.Equals(i.Name, identifier, StringComparison.CurrentCultureIgnoreCase)
                    )
                );
            if (clips.Count == 0)
                clips.AddRange(
                    files.Where(i =>
                        i.Name.StartsWith(identifier, StringComparison.CurrentCultureIgnoreCase)
                    )
                );
            if (clips.Count == 0)
                return false;

            foreach (var clip in clips)
            {
                if (clip == null)
                    continue;
                if (ModNetworkBehaviour.Instance != null)
                {
                    if (clip.VanillaId != null)
                        ModNetworkBehaviour.Instance.StartPlayingVanillaMusicServerRpc(
                            boombox.NetworkObject,
                            clip.VanillaId.Value
                        );
                    else if (clip.Crc != null)
                        ModNetworkBehaviour.Instance.StartPlayingMusicServerRpc(
                            boombox.NetworkObject,
                            clip.Crc.Value,
                            clip.Name
                        );
                    else
                    {
                        CustomBoomboxMusic.Logger.LogWarning(
                            $"AudioFile doesn't have CRC nor VanillaID: {clip}"
                        );
                        continue;
                    }

                    return true;
                }

                if (!boombox.isBeingUsed)
                    GrabbaleObject_ActivateItemRpc.Invoke(boombox, new object[] { true, true });
                boombox.boomboxAudio.clip = clip.AudioClip;
                boombox.boomboxAudio.pitch = 1f;
                boombox.boomboxAudio.Play();
                boombox.isBeingUsed = boombox.isPlayingMusic = true;
                CustomBoomboxMusic.AnnouncePlaying(clip);
                return true;
            }

            return false;
        }
    }
}