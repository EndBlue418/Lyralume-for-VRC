using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeNetEaseProvider : UdonSharpBehaviour
{
    public LyralumeController controller;
    [Header("伺服器部署並設定 Redis 後再啟用")]
    public bool autoDownloadEnabled;
    [Header("只預讀實際下一首，不提前顯示")]
    public bool prefetchNextSong = true;
    public bool coverRecoveryEnabled = true;
    [Header("API 站點：Tools / Lyralume / 設定精靈")]
    [Min(1)] public int failuresPerEndpoint = 2;
    [Min(1)] public int maxPendingResponses = 6;
    [HideInInspector] public string endpointConfiguration = "";
    [HideInInspector] public string[] endpointNames = new string[0];
    [HideInInspector] public VRCUrl[] catalogueUrls = new VRCUrl[0];
    // UdonSharp serializes jagged arrays through Odin as one payload, avoiding
    // hundreds of thousands of Unity prefab property overrides per instance.
    [HideInInspector] public VRCUrl[][] endpointBeginUrls;
    [HideInInspector] public VRCUrl[][] endpointPartUrls;
    [HideInInspector] public VRCUrl catalogueUrl;
    [HideInInspector] public VRCUrl[] beginUrls = new VRCUrl[0];
    [HideInInspector] public VRCUrl[] partUrls = new VRCUrl[0];

    private string songId = "";
    private int[] parts = new int[4];
    private int stage;
    private int slot;
    private int part;
    private int generation;
    private int requestGeneration;
    private string expectedUrl = "";
    private bool awaiting;
    private float nextRequest;
    private int failures;
    private int pendingResponses;
    private int endpointIndex;
    private int displayedEndpointIndex;
    private int prefetchedEndpointIndex;
    private bool prefetching;
    private string currentSongId = "";
    private string attemptedNextId = "";
    private string prefetchedSongId = "";
    private DataDictionary prefetchedData;
    private float nextPrefetchCheck;
    private bool coverPending;
    private int coverAttempts;
    private float nextCoverCheck;
    private bool wasEnabled;

    public string DebugStatus()
    {
        string endpoint = endpointIndex >= 0 && endpointIndex < endpointNames.Length ? endpointNames[endpointIndex] : "尚未設定";
        return endpoint + " | 階段=" + stage + " | 重試=" + failures + " | 等待=" + pendingResponses +
            " | 請求=" + (awaiting ? "進行中" : "閒置") + " | 自動=" + autoDownloadEnabled;
    }

    private void Start()
    {
        if (autoDownloadEnabled && controller != null && controller.playback != null && controller.playback.Url != null)
            RequestForSong(controller.playback.Url.Get());
    }

    public void CancelRequest()
    {
        generation++;
        songId = "";
        stage = 0;
        failures = 0; pendingResponses = 0; endpointIndex = 0;
        prefetching = false;
        currentSongId = "";
        coverPending = false; coverAttempts = 0;
        // 保留 awaiting，等舊下載回呼到達後排空，避免與下一次請求混淆。
    }

    public void RequestForSong(string playerUrl)
    {
        coverPending = false; coverAttempts = 0;
        generation++;
        songId = "";
        stage = 0;
        failures = 0; pendingResponses = 0; endpointIndex = 0;
        prefetching = false;
        currentSongId = "";
        if (!autoDownloadEnabled || controller == null || !controller.systemEnabled)
        {
            if (wasEnabled) CancelRequest();
            wasEnabled = false;
            return;
        }
        wasEnabled = true;
        controller.ClearAndShowStatus("");
        songId = ExtractSongId(playerUrl);
        currentSongId = songId;
        if (songId == "") return;
        if (songId == prefetchedSongId && prefetchedData != null)
        {
            displayedEndpointIndex = prefetchedEndpointIndex;
            controller.ReceiveLyricData(prefetchedData);
            ScheduleCoverRecovery(prefetchedData);
            return;
        }
        if (!HasEndpointTables())
        { controller.ClearAndShowStatus("自動歌詞網址表尚未建立"); return; }
        EncodeSongParts();
        stage = 1;
        controller.ClearAndShowStatus("歌詞自動載入中…");
    }

    private string ExtractSongId(string playerUrl)
    {
        if (string.IsNullOrEmpty(playerUrl)) return "";
        string decoded = playerUrl.Replace("%3D", "=").Replace("%3d", "=")
            .Replace("%3F", "?").Replace("%3f", "?").Replace("%26", "&").Replace("%23", "#");
        if (!decoded.Contains("music.163.com")) return "";
        int at = decoded.IndexOf("?id=");
        if (at < 0) at = decoded.IndexOf("&id=");
        if (at < 0) return "";
        int start = at + 4;
        int end = start;
        while (end < decoded.Length && decoded[end] >= '0' && decoded[end] <= '9') end++;
        string id = decoded.Substring(start, end - start);
        if (id.Length == 0 || id.Length > 12 || id[0] == '0') return "";
        return id;
    }

    // 獨立入口供編譯後的 Udon VM 回歸測試；RequestForSong 先驗證歌曲 ID。
    public void EncodeSongParts()
    {
        string padded = songId;
        while (padded.Length < 12) padded = "0" + padded;
        for (int i = 0; i < 4; i++)
        {
            // UdonSharp 不會正確寫回以陣列元素作為 out 的參數；先用本地變數接收。
            int parsedPart;
            if (!int.TryParse(padded.Substring(i * 3, 3), out parsedPart))
            { songId = ""; controller.ClearAndShowStatus("歌曲 ID 解析失敗"); return; }
            parts[i] = parsedPart;
        }
        Debug.Log("[Lyralume NetEase] ID " + songId + ": " + parts[0] + "," + parts[1] + "," + parts[2] + "," + parts[3]);
    }

    private void Update()
    {
        if (!autoDownloadEnabled || controller == null || !controller.systemEnabled) return;
        if (UnityEngine.Time.realtimeSinceStartup >= nextPrefetchCheck)
        {
            nextPrefetchCheck = UnityEngine.Time.realtimeSinceStartup + 2f;
            CheckNextSong();
        }
        if (stage == 0 && !awaiting && coverPending && !prefetching && UnityEngine.Time.realtimeSinceStartup >= nextCoverCheck && coverAttempts < 8)
        { stage = 4; coverAttempts++; nextCoverCheck = UnityEngine.Time.realtimeSinceStartup + 60f; }
        if (stage == 0 || awaiting ||
            UnityEngine.Time.realtimeSinceStartup < nextRequest) return;
        VRCUrl url = null;
        if (stage == 1 || stage == 4) url = GetCatalogueUrl(stage == 4 ? displayedEndpointIndex : endpointIndex);
        else if (stage == 2)
        {
            if (!IsPublisher()) { stage = 1; return; }
            url = endpointBeginUrls != null && endpointBeginUrls.Length > 0 ?
                endpointBeginUrls[endpointIndex][parts[0]] : beginUrls[endpointIndex * 1000 + parts[0]];
        }
        else if (stage == 3)
        {
            if (!IsPublisher()) { stage = 1; return; }
            url = endpointPartUrls != null && endpointPartUrls.Length > 0 ?
                endpointPartUrls[endpointIndex][((part - 1) * 16 + slot) * 1000 + parts[part]] :
                partUrls[endpointIndex * 48000 + ((part - 1) * 16 + slot) * 1000 + parts[part]];
        }
        if (url == null) return;
        expectedUrl = url.Get();
        requestGeneration = generation;
        awaiting = true;
        nextRequest = UnityEngine.Time.realtimeSinceStartup + 5.2f;
        VRCStringDownloader.LoadUrl(url, this);
    }

    private bool IsPublisher()
    {
        return controller.playback != null && controller.playback.authorityObject != null ? Networking.IsOwner(controller.playback.authorityObject) : Networking.IsMaster;
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (!autoDownloadEnabled || controller == null || !controller.systemEnabled || result.Url.Get() != expectedUrl) return;
        awaiting = false;
        if (requestGeneration != generation || !autoDownloadEnabled) return;
        DataToken root;
        if (!VRCJson.TryDeserializeFromJson(result.Result, out root) || root.TokenType != TokenType.DataDictionary)
        { if (stage == 4) stage = 0; else Fail("自動歌詞回傳格式錯誤"); return; }
        DataDictionary data = root.DataDictionary;
        if (stage == 4)
        {
            stage = 0; DataToken coverSongs;
            if (data.TryGetValue("songs", out coverSongs) && coverSongs.TokenType == TokenType.DataList)
                for (int i = 0; i < coverSongs.DataList.Count; i++) if (coverSongs.DataList[i].TokenType == TokenType.DataDictionary) ApplyCoverRefresh(coverSongs.DataList[i].DataDictionary);
            return;
        }
        string status = ReadString(data, "status");
        if (status == "ready")
        {
            if (ReadString(data, "songId") != songId) { Fail("歌詞歌曲 ID 不一致，重新載入"); return; }
            FinishRequest(data);
            return;
        }
        if (status == "catalogue")
        {
            DataToken songs;
            if (data.TryGetValue("songs", out songs) && songs.TokenType == TokenType.DataList)
            {
                for (int i = 0; i < songs.DataList.Count; i++)
                {
                    DataToken candidate = songs.DataList[i];
                    if (candidate.TokenType != TokenType.DataDictionary || ReadString(candidate.DataDictionary, "songId") != songId) continue;
                    FinishRequest(candidate.DataDictionary); return;
                }
            }
            if (!IsPublisher() && ++pendingResponses >= Mathf.Max(1, maxPendingResponses))
            { Fail("此站點尚無歌曲快取"); return; }
            stage = IsPublisher() ? 2 : 1;
            return;
        }
        if (status == "building")
        {
            DataToken slotToken, nextToken;
            if (!data.TryGetValue("slot", out slotToken) || !data.TryGetValue("nextPart", out nextToken) ||
                slotToken.TokenType != TokenType.Double || nextToken.TokenType != TokenType.Double)
            { Fail("自動歌詞通路回傳格式錯誤"); return; }
            slot = (int)slotToken.Double;
            part = (int)nextToken.Double;
            if (slot < 0 || slot >= 16 || part < 1 || part >= 4)
            { Fail("自動歌詞通路資料超出範圍"); return; }
            pendingResponses = 0;
            stage = 3;
            return;
        }
        if (status == "busy" || status == "restart")
        {
            pendingResponses++;
            if (pendingResponses >= Mathf.Max(1, maxPendingResponses)) { Fail("歌詞伺服器持續忙碌"); return; }
            stage = 1; nextRequest = UnityEngine.Time.realtimeSinceStartup + 10f; return;
        }
        Fail("自動歌詞伺服器尚未就緒");
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (!autoDownloadEnabled || controller == null || !controller.systemEnabled || result.Url.Get() != expectedUrl) return;
        awaiting = false;
        if (requestGeneration != generation || !autoDownloadEnabled) return;
        if (stage == 4) { stage = 0; return; }
        Debug.LogError("[Lyralume NetEase] " + result.ErrorCode + ": " + result.Error);
        Fail(result.ErrorCode == 503 ? "歌詞伺服器尚未設定共享儲存" : "自動歌詞下載失敗");
    }

    private void Fail(string message)
    {
        failures++;
        // A slot is local to one server. Always restart at catalogue after failover.
        slot = 0; part = 0; pendingResponses = 0;
        if (failures >= Mathf.Max(1, failuresPerEndpoint))
        {
            if (endpointIndex + 1 >= EndpointCount())
            {
                stage = 0; prefetching = false;
                if (songId == currentSongId) controller.ClearAndShowStatus(message + "（所有 API 站點均失敗）");
                return;
            }
            endpointIndex++; failures = 0;
            Debug.LogWarning("[Lyralume NetEase] Switching API to " + endpointNames[endpointIndex]);
        }
        if (!prefetching) controller.ClearAndShowStatus(message + "，嘗試 API " + (endpointIndex + 1));
        stage = 1;
        nextRequest = UnityEngine.Time.realtimeSinceStartup + 5.2f;
    }

    // 讀取 VizVid 已排好的下一個項目，亦支援隨機播放與循環清單。
    public string NextSongId()
    {
        if (controller == null || controller.playback == null || !controller.playback.SupportsNext) return "";
        return ExtractSongId(controller.playback.NextUrl);
    }

    private void CheckNextSong()
    {
        if (!prefetchNextSong || controller.playback == null ||
            !IsPublisher() || (!controller.playback.IsPlaying && !controller.playback.IsPaused)) return;
        if (stage != 0 && !prefetching) return; // 目前歌曲永遠優先。
        string nextId = NextSongId();
        if (nextId == currentSongId) nextId = "";
        if (prefetching && stage != 0 && songId != nextId)
        {
            generation++;
            stage = 0;
            prefetching = false;
        }
        if (nextId == "") { attemptedNextId = ""; return; }
        if (nextId == prefetchedSongId || nextId == attemptedNextId || stage != 0) return;
        if (!HasEndpointTables()) return;
        attemptedNextId = nextId;
        generation++;
        songId = nextId;
        prefetching = true;
        failures = 0; pendingResponses = 0; endpointIndex = 0;
        EncodeSongParts();
        stage = 1;
    }

    private void FinishRequest(DataDictionary data)
    {
        stage = 0;
        if (prefetching)
        {
            prefetchedEndpointIndex = endpointIndex;
            prefetchedSongId = songId;
            prefetchedData = data;
            Debug.Log("[Lyralume NetEase] Prefetched next song " + songId);
        }
        else { displayedEndpointIndex = endpointIndex; controller.ReceiveLyricData(data); ScheduleCoverRecovery(data); }
        prefetching = false;
    }

    private bool HasCoverThumbnail(DataDictionary data)
    {
        DataToken value;
        if (data == null || !data.TryGetValue("coverThumbnail", out value) || value.TokenType != TokenType.DataDictionary) return false;
        DataDictionary thumbnail = value.DataDictionary;
        DataToken version, width, height;
        if (!thumbnail.TryGetValue("version", out version) || version.TokenType != TokenType.Double || version.Double != 1 ||
            !thumbnail.TryGetValue("width", out width) || width.TokenType != TokenType.Double || width.Double != 64 ||
            !thumbnail.TryGetValue("height", out height) || height.TokenType != TokenType.Double || height.Double != 64) return false;
        return ReadString(thumbnail, "songId") == ReadString(data, "songId") && ReadString(thumbnail, "encoding") == "rgb24-hex-top-down" && ReadString(thumbnail, "pixels").Length == 24576;
    }
    private void ScheduleCoverRecovery(DataDictionary data)
    {
        coverPending = controller != null && controller.visualDisplay != null && controller.coverEnabled && coverRecoveryEnabled && ReadString(data, "songId") == currentSongId && !HasCoverThumbnail(data);
        coverAttempts = 0; nextCoverCheck = UnityEngine.Time.realtimeSinceStartup + 20f;
    }
    public void ApplyCoverRefresh(DataDictionary data)
    {
        if (!coverPending || controller == null || controller.visualDisplay == null || !controller.coverEnabled || !coverRecoveryEnabled || ReadString(data, "songId") != currentSongId || !HasCoverThumbnail(data)) return;
        controller.ReceiveCoverData(data);
        coverPending = false;
    }

    public int EndpointCount()
    {
        if (catalogueUrls != null && catalogueUrls.Length > 0) return catalogueUrls.Length;
        return catalogueUrl != null && !string.IsNullOrEmpty(catalogueUrl.Get()) ? 1 : 0;
    }

    public bool HasEndpointTables()
    {
        int count = EndpointCount();
        if (count <= 0) return false;
        if (endpointBeginUrls != null && endpointBeginUrls.Length > 0)
        {
            if (endpointBeginUrls.Length != count || endpointPartUrls == null || endpointPartUrls.Length != count ||
                catalogueUrls == null || catalogueUrls.Length != count || endpointNames == null || endpointNames.Length != count) return false;
            for (int i = 0; i < count; i++)
                if (endpointBeginUrls[i] == null || endpointBeginUrls[i].Length != 1000 ||
                    endpointPartUrls[i] == null || endpointPartUrls[i].Length != 48000) return false;
            return true;
        }
        return beginUrls != null && partUrls != null && beginUrls.Length == count * 1000 &&
            partUrls.Length == count * 48000 && (catalogueUrls != null && catalogueUrls.Length > 0 ?
            endpointNames != null && endpointNames.Length == count : catalogueUrl != null);
    }

    private VRCUrl GetCatalogueUrl(int index)
    {
        return catalogueUrls == null || catalogueUrls.Length == 0 ? catalogueUrl : catalogueUrls[index];
    }

    private string ReadString(DataDictionary data, string key)
    {
        DataToken value;
        return data.TryGetValue(key, out value) && value.TokenType == TokenType.String ? value.String : "";
    }
}
