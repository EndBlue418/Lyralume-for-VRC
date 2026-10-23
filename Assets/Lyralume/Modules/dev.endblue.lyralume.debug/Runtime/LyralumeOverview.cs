using UdonSharp;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeOverview : UdonSharpBehaviour
{
    public LyralumeFumeDisplay source;
    public Camera overviewCamera;
    public Transform overviewStage;
    public RenderTexture output;
    public MeshFilter[] pages;
    public Material glyphMaterial, screenMaterial;
    public TMP_Text status;
    public GameObject nativeModeState;
    [HideInInspector] public GameObject directModeState; // Legacy migration only; no runtime mode.
    [HideInInspector] public bool directMode;
    // LuraSwitch's controller protocol writes _value, then calls OnValueChanged.
    [HideInInspector] public float _value;
    public void OnValueChanged() { focusDistance = Mathf.Clamp01(_value); if (isOpen) FrameMap(); }
    public Slider focusZoomSlider;
    [Range(0, 1)] public float focusDistance; // 0 = full stage, 1 = near main camera.
    public Transform nativeStage;
    public Transform nativeBackground;
    public MeshFilter[] nativePages, nativeAuxiliary;
    public Material nativeGlyphMaterial, nativeBackgroundMaterial, nativeSecondaryMaterial;
    public int nativeLayer = 25;
    [HideInInspector] public int mapLayer = -1;
    [HideInInspector] public bool nativeMode;
    [HideInInspector] public int completedNativePages;
    [HideInInspector] public int completedPages;
    private int revision = -1, pageCount;
    private Vector3 minimum, maximum;
    private bool hasBounds;
    private float nextMarker;

    [HideInInspector] public bool isOpen;
    public bool openOnEnable = true;
    public void ToggleOverview() { if (isOpen) CloseOverview(); else OpenOverview(); }
    public void ToggleNativeMode() { SetNativeMode(!nativeMode); }
    private void OnEnable()
    {
        var monitor = GetComponent<LyralumeDebugMonitor>();
        bool shouldOpen = monitor != null && monitor.panelRoot != null ? monitor.defaultOpen : openOnEnable;
        if (shouldOpen) OpenOverview();
    }
    public void OpenOverview()
    {
        if (overviewCamera == null || source == null) return;
        isOpen = true; overviewCamera.enabled = true; nextMarker = 0;
        Tick();
    }
    private void OnDisable() { CloseOverview(); }
    public void CloseOverview()
    {
        isOpen = false; if (overviewCamera != null) overviewCamera.enabled = false;
        if (output != null) output.Release();
    }
    private void Update() { if (!nativeMode) Tick(); }
    // The main display updates its camera and attached labels in Update.
    // Copy their final pose together in LateUpdate instead of sampling mixed frames.
    private void LateUpdate() { if (nativeMode) Tick(); }
    public void Tick()
    {
        if (!isOpen || !gameObject.activeInHierarchy || source == null || overviewCamera == null) return;
        if (nativeModeState != null) SetNativeMode(nativeModeState.activeSelf);
        if (focusZoomSlider != null) focusDistance = Mathf.Clamp01(focusZoomSlider.value);
        if (revision != source.GetOverviewRevision())
        {
            revision = source.GetOverviewRevision(); completedPages = 0; hasBounds = false;
            completedNativePages = 0;
            pageCount = Mathf.Min(source.GetOverviewLineCount(), pages.Length);
            for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(false);
            if (nativePages != null) for (int i = 0; i < nativePages.Length; i++) nativePages[i].gameObject.SetActive(false);
            glyphMaterial.SetTexture("_MainTex", source.glyphMaterial.GetTexture("_MainTex"));
            FrameMap();
        }
        if (nativeMode)
        {
            if (completedNativePages < pageCount)
            {
                int count = source.GetOverviewLineCount(); var page = nativePages[completedNativePages];
                page.sharedMesh = source.BuildNativeOverviewMesh(page.sharedMesh, count * completedNativePages / pageCount, count * (completedNativePages + 1) / pageCount);
                page.gameObject.SetActive(true); completedNativePages++;
            }
        }
        if (completedPages < pageCount)
        {
            int count = source.GetOverviewLineCount();
            var page = pages[completedPages];
            page.sharedMesh = source.BuildOverviewMesh(page.sharedMesh, count * completedPages / pageCount, count * (completedPages + 1) / pageCount);
            page.gameObject.SetActive(!nativeMode);
            if (page.sharedMesh.vertexCount > 0)
            {
                Bounds bounds = page.sharedMesh.bounds;
                if (!hasBounds) { minimum = bounds.min; maximum = bounds.max; hasBounds = true; }
                else { minimum = Vector3.Min(minimum, bounds.min); maximum = Vector3.Max(maximum, bounds.max); }
            }
            completedPages++; FrameMap();
        }
        if (nativeMode) { FrameMap(); UpdateNativeFrame(); UpdateMarker(); return; }
        if (Time.realtimeSinceStartup < nextMarker) return;
        nextMarker = Time.realtimeSinceStartup + 0.1f;
        UpdateMarker();
    }
    public void FrameMap()
    {
        Vector3 center = hasBounds ? (minimum + maximum) * 0.5f : Vector3.zero;
        float width = hasBounds ? maximum.x - minimum.x : 20;
        float height = hasBounds ? maximum.y - minimum.y : 10;
        if (nativeMode && source.stageCamera != null && source.articleRoot != null)
        {
            // Camera-attached translation and romanization sit outside the lyric
            // article's mesh bounds. Full-stage framing must include the live screen.
            Vector3 live = source.articleRoot.InverseTransformPoint(source.stageCamera.transform.position);
            Vector3 scale = source.articleRoot.lossyScale;
            float h = source.stageCamera.orthographicSize / Mathf.Max(.001f, Mathf.Abs(scale.y));
            float w = source.stageCamera.orthographicSize * source.stageCamera.aspect / Mathf.Max(.001f, Mathf.Abs(scale.x));
            float left = Mathf.Min(center.x - width * .5f, live.x - w);
            float right = Mathf.Max(center.x + width * .5f, live.x + w);
            float bottom = Mathf.Min(center.y - height * .5f, live.y - h);
            float top = Mathf.Max(center.y + height * .5f, live.y + h);
            center = new Vector3((left + right) * .5f, (bottom + top) * .5f, 0);
            width = right - left; height = top - bottom;
        }
        Transform stage = nativeMode ? nativeStage : overviewStage;
        overviewCamera.transform.position = stage.TransformPoint(new Vector3(center.x, center.y, -10));
        overviewCamera.transform.rotation = stage.rotation;
        float sx = Mathf.Abs(stage.lossyScale.x), sy = Mathf.Abs(stage.lossyScale.y);
        overviewCamera.orthographicSize = Mathf.Max(3, Mathf.Max((height * 0.5f + 1) * sy, (width * 0.5f + 1) * sx / overviewCamera.aspect));
        if (nativeMode)
        {
            Vector3 fullPosition = overviewCamera.transform.position;
            float fullSize = overviewCamera.orthographicSize;
            FrameDirectStage();
            fullSize = Mathf.Max(fullSize, overviewCamera.orthographicSize);
            overviewCamera.transform.position = Vector3.Lerp(fullPosition, overviewCamera.transform.position, Mathf.Clamp01(focusDistance));
            overviewCamera.orthographicSize = Mathf.Lerp(fullSize, overviewCamera.orthographicSize, Mathf.Clamp01(focusDistance));
        }
    }
    public void UpdateMarker()
    {
        if (source == null || source.stageCamera == null || source.articleRoot == null) return;
        Vector3 at = source.articleRoot.InverseTransformPoint(source.stageCamera.transform.position);
        Vector3 sourceScale = source.articleRoot.lossyScale;
        float h = source.stageCamera.orthographicSize / Mathf.Max(.001f, Mathf.Abs(sourceScale.y));
        float w = source.stageCamera.orthographicSize * source.stageCamera.aspect / Mathf.Max(.001f, Mathf.Abs(sourceScale.x));
        glyphMaterial.SetVector("_ViewRect", new Vector4(at.x - w, at.y - h, at.x + w, at.y + h));
        Transform stage = nativeMode ? nativeStage : overviewStage;
        Vector3 center = stage.InverseTransformPoint(overviewCamera.transform.position);
        float mapH = overviewCamera.orthographicSize * 2 / Mathf.Max(.001f, Mathf.Abs(stage.lossyScale.y));
        float mapW = overviewCamera.orthographicSize * 2 * overviewCamera.aspect / Mathf.Max(.001f, Mathf.Abs(stage.lossyScale.x));
        screenMaterial.SetVector("_ViewRect", new Vector4((at.x - w - center.x) / mapW + 0.5f,
            (at.y - h - center.y) / mapH + 0.5f, (at.x + w - center.x) / mapW + 0.5f, (at.y + h - center.y) / mapH + 0.5f));
        screenMaterial.SetVector("_Cursor", new Vector4((at.x - center.x) / mapW + 0.5f, (at.y - center.y) / mapH + 0.5f, 0, 0));
        if (status != null) status.text = nativeMode ? (completedNativePages < pageCount ? "BUILDING REAL STAGE..." : "REAL STAGE | ZOOM " + Mathf.RoundToInt(focusDistance * 100) + "% | PURPLE FRAME = MAIN CAMERA") : completedPages < pageCount ? "BUILDING LYRIC MAP..." :
            source.GetOverviewLineCount() == 0 ? "NO LYRIC TIMELINE" : "FULL LAYOUT | " + source.GetOverviewLineCount() + " LINES | CAMERA " + Mathf.RoundToInt(at.x) + ", " + Mathf.RoundToInt(at.y);
    }

    public void SetNativeMode(bool value)
    {
        if (nativeStage == null) return;
        if (nativeMode == value) return;
        nativeMode = value; ApplyStageMode();
    }

    public void SetDirectMode(bool value)
    {
        // Retained for old editor tooling; the direct-stage switch has been retired.
        directMode = false;
    }

    public void ApplyStageMode()
    {
        bool animated = nativeMode;
        if (nativeStage != null) nativeStage.gameObject.SetActive(animated);
        overviewCamera.cullingMask = animated ? 1 << nativeLayer : 1 << (mapLayer >= 0 ? mapLayer : source.lineMeshes[0].gameObject.layer);
        for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(!animated && i < completedPages);
        screenMaterial.SetFloat("_NativeMode", animated ? 1 : 0);
        nextMarker = 0; FrameMap();
    }

    public void FrameDirectStage()
    {
        Vector3 at = source.articleRoot.InverseTransformPoint(source.stageCamera.transform.position);
        // Near end of the REAL STAGE zoom range, centered on the live main camera.
        // Preserve framing scale when the overview RT and main RT have different aspects.
        Transform stage = nativeStage;
        overviewCamera.transform.position = stage.TransformPoint(new Vector3(at.x, at.y, -10));
        overviewCamera.transform.rotation = stage.rotation;
        Vector3 sourceScale = source.articleRoot.lossyScale, nativeScale = stage.lossyScale;
        float halfHeight = source.stageCamera.orthographicSize / Mathf.Max(.001f, Mathf.Abs(sourceScale.y)) * Mathf.Abs(nativeScale.y);
        float halfWidth = source.stageCamera.orthographicSize * source.stageCamera.aspect / Mathf.Max(.001f, Mathf.Abs(sourceScale.x)) * Mathf.Abs(nativeScale.x);
        overviewCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / overviewCamera.aspect) * 1.35f;
    }

    public void UpdateOutputTint()
    {
        if (source.screenMaterials.Length == 0) return;
        screenMaterial.SetFloat("_ContentOpacity", source.screenMaterials[0].GetFloat("_ScreenOpacity"));
        screenMaterial.SetColor("_NativeTint", source.screenMaterials[0].GetColor("_Color"));
        screenMaterial.SetFloat("_NativeBrightness", source.screenMaterials[0].GetFloat("_EmissionIntensity"));
    }

    public void UpdateNativeFrame()
    {
        // Paper fills clip space, but Unity still culls its physical unit quad first.
        // Keep that quad in this camera's frustum at every zoom and song position.
        if (nativeBackground != null)
            nativeBackground.SetPositionAndRotation(overviewCamera.transform.position + overviewCamera.transform.forward, overviewCamera.transform.rotation);
        float time = source.glyphMaterial.GetFloat("_SongTime");
        nativeGlyphMaterial.SetFloat("_SongTime", time);
        nativeGlyphMaterial.SetFloat("_StageY", nativeStage.position.y);
        nativeGlyphMaterial.SetColor("_AccentColor", source.glyphMaterial.GetColor("_AccentColor"));
        nativeGlyphMaterial.SetFloat("_AccentBlend", source.glyphMaterial.GetFloat("_AccentBlend"));
        nativeGlyphMaterial.SetFloat("_ActiveColorStrength", source.glyphMaterial.GetFloat("_ActiveColorStrength"));
        nativeGlyphMaterial.SetFloat("_ColorReleaseSeconds", source.glyphMaterial.GetFloat("_ColorReleaseSeconds"));
        nativeGlyphMaterial.SetFloat("_KeywordColorStrength", source.glyphMaterial.GetFloat("_KeywordColorStrength"));
        nativeGlyphMaterial.SetFloat("_KeywordReleaseSeconds", source.glyphMaterial.GetFloat("_KeywordReleaseSeconds"));
        nativeGlyphMaterial.SetFloat("_RefrainColorStrength", source.glyphMaterial.GetFloat("_RefrainColorStrength"));
        nativeGlyphMaterial.SetFloat("_RefrainHalo", source.glyphMaterial.GetFloat("_RefrainHalo"));
        nativeGlyphMaterial.SetFloat("_RecentHistoryOpacity", source.glyphMaterial.GetFloat("_RecentHistoryOpacity"));
        nativeGlyphMaterial.SetFloat("_HistoryLayerStrength", source.glyphMaterial.GetFloat("_HistoryLayerStrength"));
        nativeGlyphMaterial.SetFloat("_InterludeAmount", source.glyphMaterial.GetFloat("_InterludeAmount"));
        nativeGlyphMaterial.SetFloat("_EndingProgress", source.glyphMaterial.GetFloat("_EndingProgress"));
        nativeGlyphMaterial.SetFloat("_FinalLineHold", source.glyphMaterial.GetFloat("_FinalLineHold"));
        var paper = source.backgroundMaterial;
        nativeBackgroundMaterial.SetFloat("_StageY", nativeStage.position.y);
        nativeBackgroundMaterial.SetFloat("_SongTime", time);
        nativeBackgroundMaterial.SetFloat("_EndingProgress", paper.GetFloat("_EndingProgress"));
        nativeBackgroundMaterial.SetTexture("_CoverTex", paper.GetTexture("_CoverTex"));
        nativeBackgroundMaterial.SetTexture("_BassTexture", paper.GetTexture("_BassTexture"));
        nativeBackgroundMaterial.SetFloat("_CoverBlend", paper.GetFloat("_CoverBlend"));
        nativeBackgroundMaterial.SetFloat("_CoverStrength", paper.GetFloat("_CoverStrength"));
        nativeBackgroundMaterial.SetFloat("_CoverBlur", paper.GetFloat("_CoverBlur"));
        nativeBackgroundMaterial.SetFloat("_BassEnabled", paper.GetFloat("_BassEnabled"));
        nativeBackgroundMaterial.SetFloat("_HighMidRotation", paper.GetFloat("_HighMidRotation"));
        nativeBackgroundMaterial.SetVector("_SmoothedBands", paper.GetVector("_SmoothedBands"));
        nativeBackgroundMaterial.SetFloat("_ReadingActivity", paper.GetFloat("_ReadingActivity"));
        nativeBackgroundMaterial.SetFloat("_RefrainAmount", paper.GetFloat("_RefrainAmount"));
        nativeBackgroundMaterial.SetFloat("_AccentBlend", paper.GetFloat("_AccentBlend"));
        nativeBackgroundMaterial.SetColor("_AccentColor", paper.GetColor("_AccentColor"));
        nativeBackgroundMaterial.SetFloat("_InterludeAmount", paper.GetFloat("_InterludeAmount"));
        nativeBackgroundMaterial.SetFloat("_LyricFocusActive", paper.GetFloat("_LyricFocusActive"));
        nativeBackgroundMaterial.SetVector("_LyricFocus", paper.GetVector("_LyricFocus"));
        Vector3 at = nativeStage.InverseTransformPoint(overviewCamera.transform.position);
        nativeBackgroundMaterial.SetVector("_StageView", new Vector4(at.x, at.y, overviewCamera.orthographicSize * 2 * overviewCamera.aspect, overviewCamera.orthographicSize * 2));
        UpdateOutputTint();
        if (source.secondaryMaterial != null)
        {
            nativeSecondaryMaterial.CopyPropertiesFromMaterial(source.secondaryMaterial);
            nativeSecondaryMaterial.SetFloat("_StageY", nativeStage.position.y);
        }
        for (int i = 0; i < nativeAuxiliary.Length; i++)
        {
            MeshFilter original = i == 0 ? source.statusMesh : i == 1 ? source.translationMesh : source.romanizationMesh;
            bool visible = original != null && original.gameObject.activeInHierarchy && original.sharedMesh != null;
            nativeAuxiliary[i].gameObject.SetActive(visible);
            if (!visible) continue;
            nativeAuxiliary[i].sharedMesh = original.sharedMesh;
            // The Debug prefab can be moved independently. Its stage displays the
            // same article coordinates, not the main stage's absolute world pose.
            Transform copy = nativeAuxiliary[i].transform;
            copy.localPosition = source.articleRoot.InverseTransformPoint(original.transform.position);
            copy.localRotation = Quaternion.Inverse(source.articleRoot.rotation) * original.transform.rotation;
            Vector3 originalScale = original.transform.lossyScale, articleScale = source.articleRoot.lossyScale;
            copy.localScale = new Vector3(originalScale.x / articleScale.x, originalScale.y / articleScale.y, originalScale.z / articleScale.z);
        }
    }
}
