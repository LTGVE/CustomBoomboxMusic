using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;
#if SPECTATE_ENEMIES
using BepInEx.Bootstrap;
using SpectateEnemy;
#endif

namespace CustomBoomboxMusic
{

    public class ModNetworkBehaviour : NetworkBehaviour
    {
        public static ModNetworkBehaviour? Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            CustomBoomboxMusic.Logger.LogDebug(
                $">> OnNetworkSpawn() Instance:{Instance?.ToString() ?? "null"}"
            );
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                Instance?.gameObject.GetComponent<NetworkObject>()?.Despawn();
            Instance = this;

            base.OnNetworkSpawn();
        }

        public static bool IsInBoomboxRange(BoomboxItem? boombox)
        {
            var localPlayer = StartOfRound.Instance?.localPlayerController;
#if SPECTATE_ENEMIES
        var spectatedEnemy = Chainloader.PluginInfos.ContainsKey(
            CustomBoomboxMusic.SPECTATE_ENEMIES
        )
            ? TryGetSpectatedEnemy()
            : null;
#endif
            CustomBoomboxMusic.Logger.LogDebug(
                $">> IsInBoomboxRange(boombox: {boombox}) localPlayer:{localPlayer} isPLayerDead:{localPlayer?.isPlayerDead} spectatedPlayerScript:{localPlayer?.spectatedPlayerScript}"
#if SPECTATE_ENEMIES
                + $" spectatedEnemy:{spectatedEnemy}"
#endif
            );

            return boombox is { boomboxAudio: not null }
                && (
                    localPlayer is { isPlayerDead: false } or { spectatedPlayerScript: not null }
#if SPECTATE_ENEMIES
                || spectatedEnemy != null
#endif
                )
                && (
                    Vector3.Distance(
                        boombox.boomboxAudio.transform.position,
#if SPECTATE_ENEMIES
                        spectatedEnemy != null ? spectatedEnemy.transform.position
                        :
#endif
                            localPlayer!.isPlayerDead
                                ? localPlayer.spectatedPlayerScript!.transform.position
                            : localPlayer.transform.position
                    ) <= boombox.boomboxAudio.maxDistance
                    || boombox.IsOwner
                    || boombox.OwnerClientId == localPlayer!.spectatedPlayerScript!.actualClientId
                );
            /*
            GameObject? TryGetSpectatedEnemy() =>
                SpectateEnemiesAPI.IsLoaded && SpectateEnemiesAPI.IsSpectatingEnemies
                    ? SpectateEnemiesAPI.CurrentEnemySpectating()
                    : null;*/
        }

        public override void OnNetworkDespawn()
        {
            CustomBoomboxMusic.Logger.LogDebug(
                $">> OnNetworkDespawn() Instance:{Instance?.ToString() ?? "null"} ==this:{Instance == this}"
            );
            if (Instance == this)
                Instance = null;
            base.OnNetworkDespawn();
        }


        private static void Play(BoomboxItem boombox, AudioFile clip)
        {
            CustomBoomboxMusic.Logger.LogDebug($">> Play({boombox}, {clip}) IsOwner:{boombox.IsOwner}");
            boombox.boomboxAudio.clip = clip.AudioClip;
            boombox.boomboxAudio.pitch = 1f;
            boombox.boomboxAudio.Play();
            boombox.isBeingUsed = boombox.isPlayingMusic = true;

            if (
                GameNetworkManager.Instance?.localPlayerController == null
                || (
                    GameNetworkManager.Instance.localPlayerController.isPlayerDead
                    && !GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                )
            )
                return;
            if (IsInBoomboxRange(boombox))
                CustomBoomboxMusic.AnnouncePlaying(clip);
        }

        private static void PlayFallback(BoomboxItem boombox, string missingName)
        {
            CustomBoomboxMusic.Logger.LogDebug(
                $">> PlayFallback({boombox}, {missingName}) IsOwner:{boombox.IsOwner}"
            );

            boombox.boomboxAudio.clip = boombox.musicAudios[
                boombox.musicRandomizer.Next(boombox.musicAudios.Length)
            ];
            boombox.boomboxAudio.pitch = 1f;
            boombox.boomboxAudio.Play();
            boombox.isBeingUsed = boombox.isPlayingMusic = true;

            if (
                GameNetworkManager.Instance?.localPlayerController == null
                || (
                    GameNetworkManager.Instance.localPlayerController.isPlayerDead
                    && !GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                )
            )
                return;
            if (IsInBoomboxRange(boombox))
                CustomBoomboxMusic.AnnounceMissing(missingName);
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartPlayingMusicServerRpc(
            NetworkObjectReference boomboxObjectReference,
            uint crc,
            string? clipName = null
        )
        {

            CustomBoomboxMusic.Logger.LogDebug(
                $">> StartPlayingMusicServerRpc({boomboxObjectReference}, {crc})"
            );
            if (boomboxObjectReference.TryGet(out _))
                StartPlayingMusicClientRpc(boomboxObjectReference, crc, clipName);
            else
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingMusicServerRpc] Boombox object could not be found, dropping request"
                );
        }


        [ClientRpc]
        public void StartPlayingMusicClientRpc(
            NetworkObjectReference boomboxObjectReference,
            uint crc,
            string? clipName = null
        )
        {


            CustomBoomboxMusic.Logger.LogDebug(
                $">> StartPlayingMusicClientRpc({boomboxObjectReference}, {crc}, {clipName})"
            );
            if (!boomboxObjectReference.TryGet(out var boomboxObject))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingMusicClientRpc] Boombox object could not be found, dropping request"
                );
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {boomboxObject}");

            if (!boomboxObject.TryGetComponent<BoomboxItem>(out var boombox))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingMusicClientRpc] Boombox component could not be found, dropping request"
                );
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {boombox}");

            if (!AudioManager.TryGetCrc(crc, out var clip))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    $"Couldn't find AudioClip with crc32 {crc}, playing fallback"
                );
                PlayFallback(boombox, $"{clipName} (CRC32: {crc})");
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {clip}");

            Play(boombox, clip);
        }


        [ServerRpc(RequireOwnership = false)]
        public void StartPlayingVanillaMusicServerRpc(
            NetworkObjectReference boomboxObjectReference,
            int vanillaId
        )
        {


            CustomBoomboxMusic.Logger.LogDebug(
                $">> StartPlayingVanillaMusicServerRpc({boomboxObjectReference}, {vanillaId})"
            );
            if (boomboxObjectReference.TryGet(out _))
                StartPlayingVanillaMusicClientRpc(boomboxObjectReference, vanillaId);
            else
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingVanillaMusicServerRpc] Boombox object could not be found, dropping request"
                );
        }


        [ClientRpc]
        public void StartPlayingVanillaMusicClientRpc(
            NetworkObjectReference boomboxObjectReference,
            int vanillaId
        )
        {

            CustomBoomboxMusic.Logger.LogDebug(
                $">> StartPlayingVanillaMusicClientRpc({boomboxObjectReference}, {vanillaId})"
            );
            if (!boomboxObjectReference.TryGet(out var boomboxObject))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingVanillaMusicClientRpc] Boombox object could not be found, dropping request"
                );
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {boomboxObject}");

            if (!boomboxObject.TryGetComponent<BoomboxItem>(out var boombox))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    "[StartPlayingMusicClientRpc] Boombox component could not be found, dropping request"
                );
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {boombox}");

            if (!AudioManager.TryGetVanillaId(boombox, vanillaId, out var clip))
            {
                CustomBoomboxMusic.Logger.LogWarning(
                    $"Couldn't find AudioClip with vanillaId {vanillaId}, playing fallback"
                );
                PlayFallback(
                    boombox,
                    string.Format(AudioManager.VANILLA_AUDIO_CLIP_NAME, vanillaId + 1)
                );
                return;
            }
            CustomBoomboxMusic.Logger.LogDebug($"   {clip}");

            Play(boombox, clip);
        }
        [Rpc(SendTo.Everyone)]
        internal void DownloadSong_Rpc(string songID,NetworkObjectReference boomboxObjectReference)

        {
            if (boomboxObjectReference.TryGet(out var boomboxObject))
                if (boomboxObject.TryGetComponent<BoomboxItem>(out var boomboxItem))
                    DownloadSongAsync(songID, boomboxItem).Forget();

        }
        private async UniTask DownloadSongAsync(string songID,BoomboxItem boomboxItem)
        {
            var url = ConstValues.RequestSongApiURL + songID + ".mp3";
            var infoUrl = ConstValues.SongInfoApi + $"[{songID}]";
            var infoRequest = UnityWebRequest.Get(infoUrl);
            var infoResult = await infoRequest.SendWebRequest();
            string fullName = "";
            if (infoResult != null && infoResult.result == UnityWebRequest.Result.Success)
            {
                var infoJson = infoResult.downloadHandler.text;
                Debug.Log("Get Song Info Success!");
                var info = JsonConvert.DeserializeObject<RequestSongInfoData>(infoJson);
                var songName = info.songs[0].name;
                var artists = info.songs[0].getAllArtists();
                fullName = artists + " - " + songName;
                Debug.Log($"Found Song! : {fullName}");
            }
            else
            {
                return;
            }
            var www = new UnityWebRequest(url);
            www.downloadHandler = new DownloadHandlerBuffer();
            var result = await www.SendWebRequest();
            var songFolder = Path.Combine(CustomBoomboxMusic.PluginPath, CustomBoomboxMusic.DIRECTORY_NAME);
            if (!Directory.Exists(songFolder)) { 
                Directory.CreateDirectory(songFolder);
            }
            if (result != null && result.result == UnityWebRequest.Result.Success)
            {
                var bytes = result.downloadHandler.data;
                var path = songFolder+"/"+fullName+".mp3";
                File.WriteAllBytes(path, bytes);
                FileInfo fileInfo = new FileInfo(path);
                CustomBoomboxMusic.Logger.LogDebug($"Downloaded Song! : {fullName} Size : {fileInfo.Length / 1024}KB");
                AudioManager.ProcessFile(fileInfo,out var audioFile);
                if (audioFile != null) {
                    if (NetworkManager.IsHost)
                        StartPlayingMusicClientRpc(boomboxItem.NetworkObject, (uint)audioFile.Crc);
                    else
                       StartPlayingMusicServerRpc(boomboxItem.NetworkObject, (uint)audioFile.Crc);
                
                }
                HUDManager.Instance.AddTextToChatOnServer("Song has been downloaded and added to the boombox! name : " + fullName);
            }
            else
            {
                CustomBoomboxMusic.Logger.LogError($"Failed to download song! : {fullName}");
            }

        }
        [Rpc(SendTo.Everyone)]
        internal void DownloadDirectLinkSong_Rpc(string url, NetworkObjectReference boomboxObjectReference)

        {
            if (boomboxObjectReference.TryGet(out var boomboxObject))
                if (boomboxObject.TryGetComponent<BoomboxItem>(out var boomboxItem))
                    DownloadDirectLinkSongAsync(url, boomboxItem).Forget();

        }
        private async UniTask DownloadDirectLinkSongAsync(string url, BoomboxItem boomboxItem)
        {

            var www = new UnityWebRequest(url);
            www.downloadHandler = new DownloadHandlerBuffer();
            var result = await www.SendWebRequest();
            var songFolder = Path.Combine(CustomBoomboxMusic.PluginPath, CustomBoomboxMusic.DIRECTORY_NAME);
            if (!Directory.Exists(songFolder))
            {
                Directory.CreateDirectory(songFolder);
            }
            var fullName = url.Split('/').Last();

            if (result != null && result.result == UnityWebRequest.Result.Success)
            {
                var bytes = result.downloadHandler.data;
                var path = songFolder + "/"+fullName+ (fullName.EndsWith(".mp3")||fullName.EndsWith(".wav") ? "" : ".mp3");
                File.WriteAllBytes(path, bytes);
                FileInfo fileInfo = new FileInfo(path);
                CustomBoomboxMusic.Logger.LogDebug($"Downloaded Song! : {fullName} Size : {fileInfo.Length / 1024}KB");
                AudioManager.ProcessFile(fileInfo, out var audioFile);
                if (audioFile != null)
                {
                    if (NetworkManager.IsHost)
                        StartPlayingMusicClientRpc(boomboxItem.NetworkObject, (uint)audioFile.Crc);
                    else
                        StartPlayingMusicServerRpc(boomboxItem.NetworkObject, (uint)audioFile.Crc);

                }
                HUDManager.Instance.AddTextToChatOnServer("Song has been downloaded and added to the boombox! name : "+fullName);
            }
            else
            {
                CustomBoomboxMusic.Logger.LogError($"Failed to download song! : {fullName}");
            }

        }

        [Rpc(SendTo.Everyone)]
        internal void BoomBoxBypassReverb_Rpc(bool bypass, NetworkObjectReference boomboxObjectReference)

        {
            if (boomboxObjectReference.TryGet(out var boomboxObject))
                if (boomboxObject.TryGetComponent<BoomboxItem>(out var boomboxItem))
                {
                    Debug.Log("BoomBoxBypassReverb_Rpc called! value : "+bypass);
                    boomboxItem.boomboxAudio.bypassReverbZones = bypass;
                    boomboxItem.boomboxAudio.bypassEffects = bypass;
                    boomboxItem.boomboxAudio.bypassListenerEffects = bypass;
                    boomboxObject.GetComponent<AudioLowPassFilter>().enabled = !bypass;

                }
        }
    }
}