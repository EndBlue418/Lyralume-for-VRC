using UnityEngine;

// Editor configuration is saved with the scene; Udon runtime receives only baked component fields.
public class LyralumeInstance : MonoBehaviour
{
    [HideInInspector] public Component vizVid;
    [HideInInspector] public bool installationComplete;
    [HideInInspector] public bool useProjectEndpoints;
    public UnityEngine.Object profile;
    public LyralumeController lyrics;
    public LyralumePlaybackState playback;
    public LyralumeNetEaseProvider provider;
    public LyralumeScreenOutput screenOutput;
}

