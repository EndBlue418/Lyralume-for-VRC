using UdonSharp;
using UnityEngine;
using TMPro;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeDebugMonitor : UdonSharpBehaviour
{
    [Header("只監看此 Lyralume 實例")]
    public LyralumeController target;
    public TMP_Text display;
    [Header("遊戲開始時的 Debug 顯示")]
    public bool defaultOpen;
    [HideInInspector] public GameObject panelRoot;
    [HideInInspector] public LyralumeOverview overview;
    [HideInInspector] public bool isOpen;
    public bool logToConsole;
    [Min(0.2f)] public float refreshSeconds = 1f;
    [HideInInspector] public string lastStatus = "";
    private float nextRefresh;
    private string loggedStatus = "";
    private void Start() { ApplyDefault(); }
    public void ApplyDefault()
    {
        // Previously installed monitors have no visibility group; retain their display behavior.
        if (panelRoot == null) { isOpen = true; return; }
        SetOpen(defaultOpen);
    }
    public void ToggleDebug() { SetOpen(!isOpen); }
    public void SetOpen(bool value)
    {
        isOpen = value;
        if (panelRoot != null) panelRoot.SetActive(value);
        if (overview != null) { if (value) overview.OpenOverview(); else overview.CloseOverview(); }
        if (value) { nextRefresh = 0; Refresh(); }
    }
    private void Update()
    {
        if (!isOpen) return;
        if (Time.realtimeSinceStartup < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartup + Mathf.Max(0.2f, refreshSeconds);
        Refresh();
    }
    public void Refresh()
    {
        if (target == null) lastStatus = "Lyralume Debug：尚未指定監看目標";
        else
        {
            LyralumePlaybackState p = target.playback;
            LyralumeNetEaseProvider source = target.automaticLyrics;
            string audio = p != null && p.HasPrivateAudioAnalysis ? (p.PrivateAudioAnalysisRunning ? "獨立分析此播放器" : "等待此播放器的音訊") : (p == null || p.AudioLink == null ? "等待目前播放器的音訊" : "已取得目前播放器訊號");
            lastStatus = "Lyralume Debug · " + target.gameObject.name + "\n" +
                "系統：" + (target.systemEnabled ? "啟用" : "停用") + " | 歌曲：" + target.songName + "\n" +
                "播放器：" + (p == null || p.adapter == null ? "外部狀態" : p.adapter.gameObject.name) +
                " | 時間：" + (p == null ? "0" : p.Time.ToString("F2")) + "s\n" +
                "歌詞：" + target.lineTimes.Length + " 行 / " + (target.isYrc ? "YRC" : "LRC") +
                " | 來源：" + target.lyricSource + "\n" +
                "狀態：" + target.lyricStatus + "\n" +
                "AudioLink：" + audio + "\n" +
                "API：" + (source == null ? "未安裝來源" : source.DebugStatus());
        }
        if (display != null) { display.richText = false; display.text = lastStatus; }
        if (logToConsole && lastStatus != loggedStatus) { Debug.Log(lastStatus); loggedStatus = lastStatus; }
    }
}
