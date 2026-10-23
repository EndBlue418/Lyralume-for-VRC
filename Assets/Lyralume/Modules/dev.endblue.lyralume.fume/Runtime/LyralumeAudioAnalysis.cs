using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;

// Private AudioLink-compatible analysis. Never publishes _AudioTexture or routes world audio.
// Audio sample packing follows AudioLink (MIT); see Documentation/ThirdParty/AudioLink-MIT.txt.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeAudioAnalysis : UdonSharpBehaviour
{
    public LyralumePlaybackState playback;
    public LyralumeFumeDisplay view;
    public CustomRenderTexture output;
    public Material analysisMaterial;
    public bool analysisEnabled = true;
    [HideInInspector] public bool isRunning, hasData;
    [HideInInspector] public Vector3 bands;
    private float[] left = new float[4096], right = new float[4096], chunk = new float[1023];
    private Color[] pixels = new Color[8192];
    private AudioSource lastSource, pendingSource;
    private bool pending;
    private float nextUpdate;
    private int rightCheck;
    private bool mono;
    private void Update() { Tick(); }
    private void OnDisable() { ResetAnalysis(); }
    public void ResetAnalysis()
    {
        isRunning = false; hasData = false; bands = Vector3.zero;
        if (playback != null) playback.PrivateAudioAnalysisRunning = false;
        if (output != null && output.IsCreated()) output.Initialize();
        lastSource = null;
    }
    public void Tick()
    {
        if (playback != null) playback.HasPrivateAudioAnalysis = true;
        AudioSource source = playback == null ? null : playback.AudioSourceLeft;
        if (!analysisEnabled || !isActiveAndEnabled || (view != null && (!view.audioEffects || !view.isActive)) || playback == null || !playback.IsPlaying || playback.IsPaused || source == null || !source.isActiveAndEnabled || output == null || analysisMaterial == null)
        { if (isRunning || hasData) ResetAnalysis(); return; }
        if (lastSource != source)
        {
            ResetAnalysis(); lastSource = source; rightCheck = 0; mono = false;
            output.Create(); output.Initialize(); nextUpdate = 0;
        }
        isRunning = true;
        playback.PrivateAudioAnalysisRunning = true;
        if (Time.realtimeSinceStartup < nextUpdate) return;
        nextUpdate = Time.realtimeSinceStartup + 1f / 30f;
        source.GetOutputData(left, 0);
        if (playback.AudioSourceRight != null) playback.AudioSourceRight.GetOutputData(right, 0);
        else if (mono && rightCheck > 0) System.Array.Copy(left, right, 4096);
        else { source.GetOutputData(right, 1); if (rightCheck <= 0) { mono = right[0] == 0; rightCheck = 60; if (mono) System.Array.Copy(left, right, 4096); } }
        rightCheck--;
        float volume = source.volume;
        var blend = source.GetCustomCurve(AudioSourceCurveType.SpatialBlend);
        var falloff = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
        var player = VRC.SDKBase.Networking.LocalPlayer;
        if (player != null && blend != null && falloff != null)
        {
            float distance = Vector3.Distance(player.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.Head).position, source.transform.position) / Mathf.Max(.001f, source.maxDistance);
            volume = Mathf.Lerp(volume, volume * falloff.Evaluate(distance), blend.Evaluate(distance));
        }
        analysisMaterial.SetFloat("_SourceVolume", volume);
        analysisMaterial.SetVector("_AdvancedTimeProps0", new Vector4(Time.timeSinceLevelLoad, 0, Time.timeSinceLevelLoad, 0));
        analysisMaterial.SetVector("_VersionNumberAndFPSProperty", new Vector4(3.02f, 3, 30, 1.02f));
        UploadSamples(); output.Update();
        if (!pending)
        {
            pending = true; pendingSource = source;
            VRCAsyncGPUReadback.Request(output, 0, TextureFormat.RGBAFloat, (IUdonEventReceiver)this);
        }
    }
    public void UploadSamples()
    {
        // AudioLink's FFT expects four 1023-sample segments per channel.
        System.Array.Copy(left, 0, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples0L", chunk);
        System.Array.Copy(left, 1023, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples1L", chunk);
        System.Array.Copy(left, 2046, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples2L", chunk);
        System.Array.Copy(left, 3069, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples3L", chunk);
        System.Array.Copy(right, 0, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples0R", chunk);
        System.Array.Copy(right, 1023, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples1R", chunk);
        System.Array.Copy(right, 2046, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples2R", chunk);
        System.Array.Copy(right, 3069, chunk, 0, 1023); analysisMaterial.SetFloatArray("_Samples3R", chunk);
    }
    public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request)
    {
        pending = false;
        if (!isRunning || !analysisEnabled || !isActiveAndEnabled || playback == null || !playback.IsPlaying || playback.IsPaused || pendingSource != playback.AudioSourceLeft || request.hasError || !request.done) return;
        if (!request.TryGetData(pixels)) return;
        // Same raw AudioLink band/history pixels consumed by the original display.
        bands = new Vector3(pixels[0].r*.65f + pixels[28*128+3].r*.35f,
            pixels[2*128].r*.60f + pixels[30*128+3].r*.40f,
            pixels[3*128].r*.80f + pixels[31*128+3].r*.20f);
        hasData = true;
    }
}
