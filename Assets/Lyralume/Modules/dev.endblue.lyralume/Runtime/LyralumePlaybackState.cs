using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Neutral state owned by exactly one adapter. No player package is required.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumePlaybackState : UdonSharpBehaviour
{
    public VRCUrl Url;
    public float Time;
    public float Duration;
    public bool IsLoading, IsReady, IsPlaying, IsPaused, Loop, IsRangeLooping;
    public bool SupportsNext;
    public string NextUrl = "";
    public GameObject authorityObject;
    public Component AudioLink;
    public AudioSource AudioSourceLeft, AudioSourceRight;
    [HideInInspector] public bool HasPrivateAudioAnalysis, PrivateAudioAnalysisRunning;
    public Texture VideoTexture;
    public UdonSharpBehaviour adapter;
    private UdonSharpBehaviour[] receivers = new UdonSharpBehaviour[0];
    private string[] events = new string[0];

    public void _AddListener(UdonSharpBehaviour receiver, string eventName)
    {
        for (int i = 0; i < receivers.Length; i++)
            if (receivers[i] == receiver && events[i] == eventName) return;
        int length = receivers.Length;
        UdonSharpBehaviour[] next = new UdonSharpBehaviour[length + 1];
        string[] names = new string[length + 1];
        for (int i = 0; i < length; i++) { next[i] = receivers[i]; names[i] = events[i]; }
        next[length] = receiver; names[length] = eventName; receivers = next; events = names;
    }
    private void Dispatch(string eventName)
    {
        for (int i = 0; i < receivers.Length; i++)
            if (receivers[i] != null && events[i] == eventName) receivers[i].SendCustomEvent(eventName);
    }
    public void NotifyBeginLoad() { Dispatch("_OnVideoBeginLoad"); }
    public void NotifyEnd() { Dispatch("_onVideoEnd"); }
    public void NotifyError() { Dispatch("_OnVideoError"); }
}
