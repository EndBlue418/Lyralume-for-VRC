using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeAudioSourceAdapter : UdonSharpBehaviour
{
    public LyralumePlaybackState playback;
    public AudioSource source;
    public VRCUrl mediaUrl;
    private bool wasPlaying;
    private AudioClip previousClip;
    private VRCUrl previousUrl;
    private bool wasPaused;
    private void Update()
    {
        if (playback == null || source == null || playback.adapter != this) return;
        playback.Time = source.time;
        playback.Duration = source.clip == null ? 0f : source.clip.length;
        playback.IsPlaying = source.isPlaying;
        playback.IsPaused = !source.isPlaying && source.time > 0f;
        playback.IsReady = source.clip != null;
        playback.IsLoading = false; playback.Loop = source.loop;
        playback.SupportsNext = false; playback.NextUrl = "";
        playback.Url = mediaUrl;
        if (previousClip != source.clip || previousUrl != mediaUrl || (!wasPlaying && !wasPaused && source.isPlaying))
        {
            previousClip = source.clip; previousUrl = mediaUrl;
            if (source.clip != null) playback.NotifyBeginLoad();
        }
        if (wasPlaying && !source.isPlaying && source.time == 0f) playback.NotifyEnd();
        wasPlaying = source.isPlaying;
        wasPaused = playback.IsPaused;
    }
    // External controls should call these after explicit stop/replay/seek actions.
    public void NotifyStop() { if (playback != null) playback.NotifyEnd(); }
    public void NotifyReplay() { if (playback != null) playback.NotifyBeginLoad(); }
}
