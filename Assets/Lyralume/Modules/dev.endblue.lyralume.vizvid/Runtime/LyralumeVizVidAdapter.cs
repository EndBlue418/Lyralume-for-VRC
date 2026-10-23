using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.VVMW;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeVizVidAdapter : UdonSharpBehaviour
{
    public LyralumePlaybackState playback;
    public Core core;
    public FrontendHandler playlist;
    private void Start()
    {
        if (core == null) return;
        core._AddListener(this, "_OnVideoBeginLoad");
        core._AddListener(this, "_onVideoEnd");
        core._AddListener(this, "_OnVideoError");
        Publish();
    }
    private void Update() { Publish(); }
    public void Publish()
    {
        if (core == null || playback == null || playback.adapter != this) return;
        playback.Url = core.Url; playback.Time = core.Time; playback.Duration = core.Duration;
        playback.IsLoading = core.IsLoading; playback.IsReady = core.IsReady;
        playback.IsPlaying = core.IsPlaying; playback.IsPaused = core.IsPaused;
        playback.Loop = core.Loop; playback.IsRangeLooping = core.IsRangeLooping;
        playback.VideoTexture = core.VideoTexture; playback.authorityObject = core.gameObject;
        playback.AudioLink = core.AudioLink;
        // These remain this Core's sources even when another Core takes the world's AudioLink.
        playback.AudioSourceLeft = (AudioSource)core.GetProgramVariable("assignedAudioSource");
        playback.AudioSourceRight = (AudioSource)core.GetProgramVariable("assignedAudioSourceR");
        playback.NextUrl = NextUrl(); playback.SupportsNext = playlist != null && playlist.core == core;
    }
    private string NextUrl()
    {
        if (playlist == null || playlist.core != core || playlist.RepeatOne) return "";
        int index = (int)playlist.GetProgramVariable("localPlayListIndex");
        if (index > 0)
        {
            ushort[] order = (ushort[])playlist.GetProgramVariable("localPlayListOrder");
            VRCUrl[] urls = playlist.PlayListUrls;
            return order != null && order.Length > 0 && urls != null && order[0] < urls.Length && urls[order[0]] != null ? urls[order[0]].Get() : "";
        }
        VRCUrl[] queue = playlist.QueueUrls;
        return queue != null && queue.Length > 0 && queue[0] != null ? queue[0].Get() : "";
    }
    public void _OnVideoBeginLoad() { Publish(); if (playback != null && playback.adapter == this) playback.NotifyBeginLoad(); }
    public void _onVideoEnd() { Publish(); if (playback != null && playback.adapter == this) playback.NotifyEnd(); }
    public void _OnVideoError() { Publish(); if (playback != null && playback.adapter == this) playback.NotifyError(); }
}
