using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeController : UdonSharpBehaviour
{
    [Header("代理 API（執行時請提供完整的 VRCUrl）")]
    [HideInInspector] public string proxyApiUrl = "";
    [HideInInspector] public string[] proxyApiUrls = new string[0];
    [HideInInspector] public VRCUrl[] endpointSongLyricUrls = new VRCUrl[0];
    private int configuredSongIndex = -1;
    private int configuredEndpointIndex;
    public VRCUrlInputField urlInputField;
    public VRCUrl lyricRequestUrl;
    [Header("同步時間：優先使用 VizVid")]
    public LyralumePlaybackState playback;
    public LyralumeNetEaseProvider automaticLyrics;
    public AudioSource audioSource;
    public float lyricOffsetSeconds;
    [Header("可選：浮名畫面（不取代原有歌詞下載與 TMP）")]
    public UdonSharpBehaviour visualDisplay;
    [HideInInspector] public DataDictionary coverData;
    public bool systemEnabled = true;
    public bool wordHighlighting = true;
    public bool translationEnabled = true;
    public bool romanizationEnabled = true;
    public bool coverEnabled = true;
    [HideInInspector] public string songName = "", artistName = "", displayText = "";
    [HideInInspector] public int displayRevision;
    [Header("逐字顏色（RRGGBB）")]
    public string sungColor = "FFD166";
    public string pendingColor = "FFFFFF";
    [Header("預設歌曲 ID：網址由 API 伺服器清單自動產生")]
    public string[] songIds = new string[0];
    [HideInInspector] public VRCUrl[] songLyricUrls = new VRCUrl[0];

    [HideInInspector] public float[] lineTimes = new float[0];
    [HideInInspector] public float[] lineEnds = new float[0];
    [HideInInspector] public string[] lineTexts = new string[0];
    [HideInInspector] public int[] lineWordStarts = new int[0];
    [HideInInspector] public int[] lineWordCounts = new int[0];
    [HideInInspector] public float[] wordTimes = new float[0];
    [HideInInspector] public float[] wordEnds = new float[0];
    [HideInInspector] public string[] wordTexts = new string[0];
    [HideInInspector] public bool isYrc;
    [HideInInspector] public int timelineRevision;
    [HideInInspector] public string lyricSource = "netease";
    [HideInInspector] public string lyricStatus = "正在載入歌詞";
    [HideInInspector] public float[] translationTimes = new float[0], translationEnds = new float[0];
    [HideInInspector] public string[] translationTexts = new string[0];
    [HideInInspector] public float[] romanizationTimes = new float[0], romanizationEnds = new float[0];
    [HideInInspector] public string[] romanizationTexts = new string[0];
    [HideInInspector] public string translationSource = "", romanizationSource = "";
    private float[] secondaryStarts, secondaryEnds;
    private string[] secondaryTexts;
    private int displayedLine = -2;
    private int displayedWord = -2;
    private bool previousHighlighting = true;
    private string previousSungColor = "", previousPendingColor = "";
    private string observedPlayerUrl = "";
    private string activeRequest = "";
    private string requestPlayerUrl = "";
    private float nextCheck;
    private float nextRequestTime;
    private VRCUrl pendingRequestUrl;
    private bool waitingForPlayback;
    private bool wasSystemEnabled = true;

    private void Start()
    {
        observedPlayerUrl = CurrentPlayerUrl();
        if (playback != null)
        {
            playback._AddListener(this, "_onVideoEnd");
            playback._AddListener(this, "_OnVideoBeginLoad");
        }

        if (lyricRequestUrl != null && !string.IsNullOrEmpty(lyricRequestUrl.Get()))
            LoadLyricsUrl(lyricRequestUrl);
    }

    public void OnUrlSubmitted()
    {
        if (urlInputField != null) OnUrlSubmitted(urlInputField.GetUrl());
    }

    public void OnUrlSubmitted(VRCUrl inputUrl)
    {
        if (inputUrl == null || string.IsNullOrEmpty(inputUrl.Get())) return;
        string url = inputUrl.Get();
        if (IsProxyUrl(url))
            LoadLyricsUrl(inputUrl);
        else
            LoadForSong(url);
    }

    private bool IsProxyUrl(string url)
    {
        if (url.StartsWith(proxyApiUrl + "?") || url.StartsWith(proxyApiUrl + "/")) return true;
        for (int i = 0; i < proxyApiUrls.Length; i++)
            if (url.StartsWith(proxyApiUrls[i] + "?") || url.StartsWith(proxyApiUrls[i] + "/")) return true;
        return false;
    }

    private VRCUrl ConfiguredSongUrl(int index)
    {
        return endpointSongLyricUrls.Length >= songIds.Length && proxyApiUrls.Length > 0 ?
            endpointSongLyricUrls[index] : songLyricUrls[index];
    }

    private void IdentifyConfiguredRequest(string url)
    {
        configuredSongIndex = -1;
        if (songIds.Length == 0) return;
        for (int i = 0; i < endpointSongLyricUrls.Length; i++)
            if (endpointSongLyricUrls[i] != null && endpointSongLyricUrls[i].Get() == url)
            { configuredSongIndex = i % songIds.Length; configuredEndpointIndex = i / songIds.Length; return; }
    }

    public void LoadConfiguredLyrics()
    {
        LoadLyricsUrl(lyricRequestUrl);
    }

    public void LoadLyricsUrl(VRCUrl requestUrl)
    {
        if (requestUrl == null || string.IsNullOrEmpty(requestUrl.Get())) return;
        if (UnityEngine.Time.realtimeSinceStartup < nextRequestTime)
        {
            pendingRequestUrl = requestUrl;
            ClearLyrics();
            ShowStatus("歌詞等待載入…");
            return;
        }
        pendingRequestUrl = null;
        IdentifyConfiguredRequest(requestUrl.Get());
        ClearLyrics();
        activeRequest = requestUrl.Get();
        requestPlayerUrl = CurrentPlayerUrl();
        nextRequestTime = UnityEngine.Time.realtimeSinceStartup + 5f;
        ShowStatus("歌詞載入中…");
        VRCStringDownloader.LoadUrl(requestUrl, this);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        if (result.Url.Get() != activeRequest || requestPlayerUrl != CurrentPlayerUrl()) return;
        ReceiveLyricJson(result.Result);
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        if (result.Url.Get() != activeRequest || requestPlayerUrl != CurrentPlayerUrl()) return;
        if (configuredSongIndex >= 0 && configuredEndpointIndex + 1 < proxyApiUrls.Length)
        {
            int next = (configuredEndpointIndex + 1) * songIds.Length + configuredSongIndex;
            if (next < endpointSongLyricUrls.Length)
            {
                Debug.LogWarning("[NetEase] Switching configured song to API " + (configuredEndpointIndex + 2));
                LoadLyricsUrl(endpointSongLyricUrls[next]);
                return;
            }
        }
        ShowStatus("歌詞載入失敗");
        Debug.LogError("[NetEase] " + result.Error);
    }

    // 可由現有下載器直接把代理 JSON 傳入。支援 yrc/lrc 字串及 { lyric: ... }。
    public void ReceiveLyricJson(string json)
    {
        if (!systemEnabled || waitingForPlayback) return;
        DataToken root;
        if (!VRCJson.TryDeserializeFromJson(json, out root) || root.TokenType != TokenType.DataDictionary)
        {
            ClearLyrics();
            ShowStatus("歌詞資料格式錯誤");
            return;
        }
        ReceiveLyricData(root.DataDictionary);
    }

    // Catalogue and prefetch already contain parsed data. Never round-trip it through VRCJson's string writer.
    public void ReceiveLyricData(DataDictionary data)
    {
        if (!systemEnabled || waitingForPlayback) return;
        ClearLyrics();
        if (data == null) { ShowStatus("歌詞資料格式錯誤"); return; }
        ReceiveCoverData(data);
        string title = ReadString(data, "name");
        string artist = ReadString(data, "artist");
        songName = string.IsNullOrEmpty(title) ? "網易雲音樂" : title;
        artistName = artist;
        string yrc = ReadLyric(data, "yrc");
        if (!string.IsNullOrEmpty(yrc) && ParseYrc(yrc)) { ReadSecondaryLyrics(data); return; }
        if (ParseStructuredLyrics(data)) { ReadSecondaryLyrics(data); return; }
        string lrc = ReadLyric(data, "lrc");
        if (!string.IsNullOrEmpty(lrc) && ParseLrc(lrc)) { ReadSecondaryLyrics(data); return; }
        ShowStatus("無歌詞");
    }

    private void ReadSecondaryLyrics(DataDictionary root)
    {
        string id = ReadString(root, "songId");
        if (id.Length == 0) return;
        string current = CurrentPlayerUrl().ToLower().Replace("%3d", "=").Replace("%26", "&").Replace("%3f", "?");
        if (current.Length > 0)
        {
            if (!current.Contains("music.163.com")) return;
            int at = current.IndexOf("?id="); if (at < 0) at = current.IndexOf("&id=");
            if (at < 0) return;
            int stop = at + 4; while (stop < current.Length && current[stop] >= '0' && current[stop] <= '9') stop++;
            if (current.Substring(at + 4, stop - at - 4) != id) return;
        }
        bool translated = false, romanized = false;
        DataToken value;
        if (lyricSource == "amll" && root.TryGetValue("secondaryLyrics", out value) && value.TokenType == TokenType.DataDictionary)
        {
            DataDictionary sidecar = value.DataDictionary;
            if (ReadMilliseconds(sidecar, "version") == 1 && ReadString(sidecar, "songId") == id && ReadString(sidecar, "source") == "amll" &&
                sidecar.TryGetValue("tracks", out value) && value.TokenType == TokenType.DataList && value.DataList.Count <= 16)
            {
                DataList tracks = value.DataList;
                for (int i = 0; i < tracks.Count; i++)
                {
                    if (tracks[i].TokenType != TokenType.DataDictionary) continue;
                    DataDictionary track = tracks[i].DataDictionary;
                    string kind = ReadString(track, "kind"), language = ReadString(track, "language").ToLower();
                    bool isTranslation = kind == "translation";
                    if ((!isTranslation && kind != "romanization") || (isTranslation ? translated : romanized)) continue;
                    bool declaredChinese = language == "zh" || language.StartsWith("zh-") || language == "cmn" || language.StartsWith("cmn-");
                    if (isTranslation && language != "" && !declaredChinese) continue;
                    if (!ReadSecondaryTrack(track, isTranslation, declaredChinese)) continue;
                    StoreSecondary(isTranslation, "amll");
                    if (isTranslation) translated = true; else romanized = true;
                }
            }
        }
        if (!translated)
        {
            bool parsed = isYrc && ReadSecondaryLrc(ReadLyric(root, "ytlrc"), true);
            if (!parsed) parsed = ReadSecondaryLrc(ReadLyric(root, "tlyric"), true);
            if (parsed) StoreSecondary(true, "netease");
        }
        if (!romanized)
        {
            bool parsed = isYrc && ReadSecondaryLrc(ReadLyric(root, "yromalrc"), false);
            if (!parsed) parsed = ReadSecondaryLrc(ReadLyric(root, "romalrc"), false);
            if (parsed) StoreSecondary(false, "netease");
        }
        PublishTimeline();
    }

    private void StoreSecondary(bool translation, string source)
    {
        if (translation) { translationTimes = secondaryStarts; translationEnds = secondaryEnds; translationTexts = secondaryTexts; translationSource = source; }
        else { romanizationTimes = secondaryStarts; romanizationEnds = secondaryEnds; romanizationTexts = secondaryTexts; romanizationSource = source; }
    }

    private bool IsSecondaryLanguage(string[] values, bool chinese)
    {
        int han = 0, latin = 0, kana = 0, hangul = 0;
        for (int i = 0; i < values.Length; i++) for (int j = 0; j < values[i].Length; j++)
        {
            int c = values[i][j];
            if ((c >= 0x3400 && c <= 0x9fff) || (c >= 0xf900 && c <= 0xfaff)) han++;
            else if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= 0x00c0 && c <= 0x024f)) latin++;
            else if ((c >= 0x3040 && c <= 0x30ff) || (c >= 0xff66 && c <= 0xff9f)) kana++;
            else if (c >= 0xac00 && c <= 0xd7af) hangul++;
        }
        return chinese ? han > 0 && han * 4 >= han + latin && kana == 0 && hangul == 0 : latin > 0 && kana == 0 && hangul == 0;
    }

    private bool ReadSecondaryTrack(DataDictionary track, bool chinese, bool declaredChinese)
    {
        DataToken value;
        if (ReadString(track, "timing") != "line" || !track.TryGetValue("lines", out value) || value.TokenType != TokenType.DataList) return false;
        DataList lines = value.DataList;
        if (lines.Count == 0 || lines.Count > 2000) return false;
        float[] times = new float[lines.Count], ends = new float[lines.Count]; string[] text = new string[lines.Count];
        int previous = -1, characters = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TokenType != TokenType.DataDictionary) return false;
            DataDictionary line = lines[i].DataDictionary;
            int start = ReadMilliseconds(line, "startMs"), end = ReadMilliseconds(line, "endMs");
            string content = ReadString(line, "text"); characters += content.Length;
            if (start < 0 || end <= start || start < previous || content.Length > 2000 || characters > 120000) return false;
            previous = start; times[i] = start / 1000f; ends[i] = end / 1000f; text[i] = content;
        }
        if (!(chinese && declaredChinese) && !IsSecondaryLanguage(text, chinese)) return false;
        secondaryStarts = times; secondaryEnds = ends; secondaryTexts = text; return true;
    }

    private bool ReadSecondaryLrc(string raw, bool chinese)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 120000) return false;
        string[] rows = raw.Replace("\r", "").Split('\n');
        if (rows.Length > 4000) return false;
        float[] times = new float[2000]; string[] texts = new string[2000]; int count = 0, offset = 0;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].StartsWith("[offset:") && rows[i].IndexOf(']') > 8) int.TryParse(rows[i].Substring(8, rows[i].IndexOf(']') - 8), out offset);
        if (offset < -600000 || offset > 600000) return false;
        for (int i = 0; i < rows.Length && count < 2000; i++)
        {
            string row = rows[i].Trim(); int cursor = 0, textStart = 0;
            while (textStart < row.Length && row[textStart] == '[') { int close = row.IndexOf(']', textStart + 1); if (close < 0) break; textStart = close + 1; }
            string text = row.Substring(textStart).Trim(); if (text.Length > 2000) continue;
            while (cursor < row.Length && row[cursor] == '[' && count < 2000)
            {
                int close = row.IndexOf(']', cursor + 1); if (close < 0) break;
                string stamp = row.Substring(cursor + 1, close - cursor - 1); int colon = stamp.IndexOf(':');
                int minutes; float seconds;
                if (colon > 0 && int.TryParse(stamp.Substring(0, colon), out minutes) && minutes >= 0 && minutes < 1440 && ParseSeconds(stamp.Substring(colon + 1), out seconds) && seconds >= 0 && seconds < 60)
                {
                    float time = minutes * 60f + seconds + offset / 1000f;
                    if (time >= 0 && time <= 86400) { int j = count; while (j > 0 && times[j - 1] > time) { times[j] = times[j - 1]; texts[j] = texts[j - 1]; j--; } times[j] = time; texts[j] = text; count++; }
                }
                cursor = close + 1;
            }
        }
        if (count == 0) return false;
        secondaryStarts = new float[count]; secondaryEnds = new float[count]; secondaryTexts = new string[count];
        for (int i = 0; i < count; i++)
        {
            secondaryStarts[i] = times[i]; secondaryTexts[i] = texts[i];
            int next = i + 1; while (next < count && times[next] == times[i]) next++;
            secondaryEnds[i] = Mathf.Min(times[i] + 12f, next < count ? times[next] : times[i] + 12f);
        }
        return IsSecondaryLanguage(secondaryTexts, chinese);
    }

    public string TranslationAtTime(float time) { return !translationEnabled ? "" : SecondaryAtTime(translationTimes, translationEnds, translationTexts, time); }
    public string RomanizationAtTime(float time) { return !romanizationEnabled ? "" : SecondaryAtTime(romanizationTimes, romanizationEnds, romanizationTexts, time); }
    private string SecondaryAtTime(float[] times, float[] ends, string[] text, float time)
    {
        int low = 0, high = times.Length - 1, found = -1;
        while (low <= high) { int middle = (low + high) / 2; if (times[middle] <= time) { found = middle; low = middle + 1; } else high = middle - 1; }
        if (found < 0 || time >= ends[found]) return "";
        string result = text[found];
        for (int i = found - 1; i >= 0 && times[i] == times[found] && found - i < 3; i--) if (time < ends[i] && text[i] != result) result = text[i] + "\n" + result;
        return result;
    }

    private string ReadString(DataDictionary data, string key)
    {
        DataToken value;
        return data.TryGetValue(key, out value) && value.TokenType == TokenType.String ? value.String : "";
    }

    private string ReadLyric(DataDictionary data, string key)
    {
        DataToken value;
        if (!data.TryGetValue(key, out value)) return "";
        if (value.TokenType == TokenType.String) return value.String;
        if (value.TokenType == TokenType.DataDictionary) return ReadString(value.DataDictionary, "lyric");
        return "";
    }

    private int ReadMilliseconds(DataDictionary data, string key)
    {
        DataToken value;
        if (!data.TryGetValue(key, out value) || value.TokenType != TokenType.Double) return -1;
        double number = value.Double;
        if (double.IsNaN(number) || double.IsInfinity(number) || number < 0 || number > 86400000 || number != (int)number) return -1;
        return (int)number;
    }

    private bool ParseStructuredLyrics(DataDictionary root)
    {
        DataToken value;
        if (!root.TryGetValue("lyricTimeline", out value) || value.TokenType != TokenType.DataDictionary) return false;
        DataDictionary data = value.DataDictionary;
        if (ReadMilliseconds(data, "version") != 1 || ReadString(data, "timing") != "word") return false;
        string id = ReadString(data, "songId");
        if (id == "" || id != ReadString(root, "songId")) return false;
        string source = ReadString(data, "source");
        if (source != "amll" && source != "qq" && source != "kugou") return false;
        if (!data.TryGetValue("lines", out value) || value.TokenType != TokenType.DataList) return false;
        DataList rows = value.DataList;
        if (rows.Count == 0 || rows.Count > 2000) return false;
        int totalWords = 0, totalCharacters = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].TokenType != TokenType.DataDictionary) return false;
            DataDictionary row = rows[i].DataDictionary;
            if (!row.TryGetValue("words", out value) || value.TokenType != TokenType.DataList || value.DataList.Count == 0) return false;
            totalWords += value.DataList.Count; totalCharacters += ReadString(row, "text").Length;
            if (totalWords > 20000 || totalCharacters > 100000) return false;
        }
        // Keep all data temporary until the entire provider timeline has passed validation.
        float[] newStarts = new float[rows.Count], newEnds = new float[rows.Count];
        string[] newTexts = new string[rows.Count];
        int[] newWordStarts = new int[rows.Count], newWordCounts = new int[rows.Count];
        float[] newWordTimes = new float[totalWords], newWordEnds = new float[totalWords];
        string[] newWords = new string[totalWords];
        int cursor = 0, previousStart = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            DataDictionary row = rows[i].DataDictionary;
            int start = ReadMilliseconds(row, "startMs"), end = ReadMilliseconds(row, "endMs");
            if (start < 0 || end < start || start < previousStart) return false;
            previousStart = start;
            row.TryGetValue("words", out value); DataList tokens = value.DataList;
            string text = ""; int previousWordStart = -1;
            newWordStarts[i] = cursor; newWordCounts[i] = tokens.Count;
            for (int w = 0; w < tokens.Count; w++)
            {
                if (tokens[w].TokenType != TokenType.DataDictionary) return false;
                DataDictionary word = tokens[w].DataDictionary;
                int wordStart = ReadMilliseconds(word, "startMs"), wordEnd = ReadMilliseconds(word, "endMs");
                if (wordStart < 0 || wordEnd < wordStart || wordStart < previousWordStart || wordStart < start - 2 || wordEnd > end + 2) return false;
                if (!word.TryGetValue("text", out value) || value.TokenType != TokenType.String) return false;
                previousWordStart = wordStart;
                newWords[cursor] = value.String; newWordTimes[cursor] = wordStart / 1000f; newWordEnds[cursor] = wordEnd / 1000f;
                text += value.String; cursor++;
            }
            if (text != ReadString(row, "text")) return false;
            newStarts[i] = start / 1000f; newEnds[i] = end / 1000f; newTexts[i] = text;
        }
        lineTimes = newStarts; lineEnds = newEnds; lineTexts = newTexts;
        lineWordStarts = newWordStarts; lineWordCounts = newWordCounts;
        wordTimes = newWordTimes; wordEnds = newWordEnds; wordTexts = newWords;
        isYrc = true; lyricSource = source; displayedLine = -2; displayedWord = -2;
        PublishTimeline(); RenderLyricsAtTime(PlaybackTime());
        return true;
    }

    private bool ParseYrc(string lyrics)
    {
        string[] rows = lyrics.Replace("\r", "").Split('\n');
        int capacity = 0;
        for (int i = 0; i < rows.Length; i++)
            for (int j = 0; j < rows[i].Length; j++) if (rows[i][j] == '(') capacity++;
        lineTimes = new float[rows.Length];
        lineEnds = new float[rows.Length];
        lineTexts = new string[rows.Length];
        lineWordStarts = new int[rows.Length];
        lineWordCounts = new int[rows.Length];
        wordTimes = new float[capacity];
        wordEnds = new float[capacity];
        wordTexts = new string[capacity];
        int count = 0;
        int words = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            int close = row.IndexOf(']');
            if (!row.StartsWith("[") || close < 2) continue;
            string[] timing = row.Substring(1, close - 1).Split(',');
            int start, duration;
            if (timing.Length != 2 || !int.TryParse(timing[0], out start) || !int.TryParse(timing[1], out duration) || start < 0 || duration < 0) continue;
            int firstWord = words;
            int cursor = close + 1;
            string text = "";
            while (cursor < row.Length)
            {
                int open = row.IndexOf('(', cursor);
                if (open < 0) break;
                int end = row.IndexOf(')', open + 1);
                if (end < 0) break;
                string[] token = row.Substring(open + 1, end - open - 1).Split(',');
                int wordStart, wordDuration;
                if (token.Length != 3 || !int.TryParse(token[0], out wordStart) || !int.TryParse(token[1], out wordDuration) || wordStart < 0 || wordDuration < 0) break;
                int next = row.IndexOf('(', end + 1);
                if (next < 0) next = row.Length;
                string word = row.Substring(end + 1, next - end - 1);
                wordTimes[words] = wordStart / 1000f;
                wordEnds[words] = (wordStart + wordDuration) / 1000f;
                wordTexts[words] = word;
                words++;
                text += word;
                cursor = next;
            }
            if (words == firstWord) continue;
            lineTimes[count] = start / 1000f;
            lineEnds[count] = (start / 1000f) + (duration / 1000f);
            lineTexts[count] = text;
            lineWordStarts[count] = firstWord;
            lineWordCounts[count] = words - firstWord;
            count++;
        }
        if (count == 0) { ClearLyrics(); return false; }
        TrimLines(count);
        isYrc = true;
        PublishTimeline();
        RenderLyricsAtTime(PlaybackTime());
        return true;
    }

    private bool ParseLrc(string lyrics)
    {
        string[] rows = lyrics.Replace("\r", "").Split('\n');
        int capacity = 0;
        for (int i = 0; i < rows.Length; i++)
            for (int j = 0; j < rows[i].Length; j++) if (rows[i][j] == '[') capacity++;
        lineTimes = new float[capacity];
        lineTexts = new string[capacity];
        lineEnds = new float[capacity];
        lineWordStarts = new int[capacity];
        lineWordCounts = new int[capacity];
        int count = 0;
        int offsetMs = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            string row = rows[i].Trim();
            if (row.StartsWith("[offset:") && row.IndexOf(']') > 8)
                int.TryParse(row.Substring(8, row.IndexOf(']') - 8), out offsetMs);
            int cursor = 0;
            while (cursor < row.Length && row[cursor] == '[')
            {
                int close = row.IndexOf(']', cursor + 1);
                if (close < 0) break;
                string stamp = row.Substring(cursor + 1, close - cursor - 1);
                int colon = stamp.IndexOf(':');
                int minutes;
                float seconds;
                if (colon > 0 && int.TryParse(stamp.Substring(0, colon), out minutes) && ParseSeconds(stamp.Substring(colon + 1), out seconds) && minutes >= 0 && seconds >= 0 && seconds < 60)
                {
                    int textStart = close + 1;
                    while (textStart < row.Length && row[textStart] == '[')
                    {
                        int nextClose = row.IndexOf(']', textStart + 1);
                        if (nextClose < 0) break;
                        textStart = nextClose + 1;
                    }
                    lineTimes[count] = minutes * 60f + seconds;
                    lineTexts[count] = row.Substring(textStart).Trim();
                    count++;
                }
                cursor = close + 1;
            }
        }
        if (count == 0) { ClearLyrics(); return false; }
        for (int i = 0; i < count; i++) lineTimes[i] += offsetMs / 1000f;
        TrimLines(count);
        isYrc = false;
        PublishTimeline();
        RenderLyricsAtTime(PlaybackTime());
        return true;
    }

    // LRC 使用固定的小數點格式，避免電腦語系影響時間解析。
    private bool ParseSeconds(string stamp, out float seconds)
    {
        seconds = 0;
        string[] parts = stamp.Split('.');
        int whole;
        if (parts.Length > 2 || !int.TryParse(parts[0], out whole)) return false;
        seconds = whole;
        if (parts.Length == 2)
        {
            if (parts[1].Length == 0) return false;
            float scale = 1;
            int fraction = 0;
            for (int i = 0; i < parts[1].Length; i++)
            {
                int digit = parts[1][i] - '0';
                if (digit < 0 || digit > 9 || i >= 6) return false;
                fraction = fraction * 10 + digit;
                scale *= 10;
            }
            seconds += fraction / scale;
        }
        return true;
    }

    private void TrimLines(int count)
    {
        float[] times = new float[count];
        float[] ends = new float[count];
        string[] texts = new string[count];
        int[] starts = new int[count];
        int[] counts = new int[count];
        // 插入排序也處理 LRC 多時間標籤及原始資料未依時間排序的情況。
        for (int i = 0; i < count; i++)
        {
            int j = i;
            while (j > 0 && times[j - 1] > lineTimes[i])
            {
                times[j] = times[j - 1]; ends[j] = ends[j - 1]; texts[j] = texts[j - 1];
                starts[j] = starts[j - 1]; counts[j] = counts[j - 1]; j--;
            }
            times[j] = lineTimes[i]; ends[j] = lineEnds[i]; texts[j] = lineTexts[i];
            starts[j] = lineWordStarts[i]; counts[j] = lineWordCounts[i];
        }
        lineTimes = times; lineEnds = ends; lineTexts = texts;
        lineWordStarts = starts; lineWordCounts = counts;
        displayedLine = -2; displayedWord = -2;
    }

    public void RenderLyricsAtTime(float seconds)
    {
        if (!systemEnabled || lineTimes.Length == 0) return;
        seconds += lyricOffsetSeconds;
        int line = -1;
        for (int i = 0; i < lineTimes.Length; i++)
        {
            if (lineTimes[i] > seconds) break;
            line = i;
        }
        // YRC 有行結束時間：間奏不持續掛著上一句。
        if (line >= 0 && isYrc && seconds >= lineEnds[line]) line = -1;
        int sung = 0;
        if (line >= 0 && isYrc)
        {
            int first = lineWordStarts[line];
            while (sung < lineWordCounts[line] && seconds >= wordTimes[first + sung]) sung++;
        }
        if (line == displayedLine && sung == displayedWord && previousHighlighting == wordHighlighting && previousSungColor == sungColor && previousPendingColor == pendingColor) return;
        previousHighlighting = wordHighlighting; previousSungColor = sungColor; previousPendingColor = pendingColor;
        displayedLine = line; displayedWord = sung;
        if (line < 0) { ShowStatus(""); return; }
        if (!isYrc || !wordHighlighting) { ShowStatus(LiteralText(lineTexts[line])); return; }
        string before = "";
        string after = "";
        for (int i = 0; i < lineWordCounts[line]; i++)
        {
            string word = LiteralText(wordTexts[lineWordStarts[line] + i]);
            if (i < sung) before += word; else after += word;
        }
        ShowStatus("<color=#" + sungColor + ">" + before + "</color><color=#" + pendingColor + ">" + after + "</color>");
    }

    private string LiteralText(string text)
    {
        // 避免歌詞內容的尖括號被當作 TMP 格式標籤。
        return text.Replace("<", "＜").Replace(">", "＞");
    }

    private void Update()
    {
        if (!systemEnabled)
        {
            if (wasSystemEnabled) DisableSystem();
            wasSystemEnabled = false;
            return;
        }
        if (!wasSystemEnabled) { wasSystemEnabled = true; EnableSystem(); }
        if (UnityEngine.Time.realtimeSinceStartup < nextCheck) return;
        nextCheck = UnityEngine.Time.realtimeSinceStartup + 0.05f;
        if (!systemEnabled || waitingForPlayback) return;
        if (playback != null)
        {
            string url = CurrentPlayerUrl();
            if (url != observedPlayerUrl)
            {
                observedPlayerUrl = url;
                activeRequest = "";
                pendingRequestUrl = null;
                ClearLyrics();
                ShowStatus("");
                LoadForSong(url);
            }
        }
        if (pendingRequestUrl != null && UnityEngine.Time.realtimeSinceStartup >= nextRequestTime)
            LoadLyricsUrl(pendingRequestUrl);
        RenderLyricsAtTime(PlaybackTime());
    }

    private void LoadForSong(string url)
    {
        // 直接切歌也需清空上一首資訊，不依賴停止事件。
        ClearAndShowStatus("");
        if (automaticLyrics != null && automaticLyrics.autoDownloadEnabled)
        {
            automaticLyrics.RequestForSong(url);
            return;
        }
        if (!url.Contains("music.163.com") && !url.Contains("163cn.tv")) return;
        for (int i = 0; i < songIds.Length && i < songLyricUrls.Length; i++)
        {
            string id = songIds[i];
            if (string.IsNullOrEmpty(id)) continue;
            string marker = "id=" + id;
            int at = url.IndexOf(marker);
            int end = at + marker.Length;
            if (at >= 0 && (end == url.Length || url[end] == '&' || url[end] == '#' || url[end] == '%'))
            {
                LoadLyricsUrl(ConfiguredSongUrl(i));
                return;
            }
            marker = "id%3D" + id;
            at = url.IndexOf(marker);
            end = at + marker.Length;
            if (at >= 0 && (end == url.Length || url[end] == '%' || url[end] == '&' || url[end] == '#'))
            {
                LoadLyricsUrl(ConfiguredSongUrl(i));
                return;
            }
        }
        ShowStatus("尚未設定此歌曲的歌詞 URL");
    }

    private string CurrentPlayerUrl()
    {
        return playback != null && playback.Url != null ? (playback.Url.Get() ?? "") : "";
    }

    private float PlaybackTime()
    {
        return playback != null ? playback.Time : audioSource != null ? audioSource.time : 0f;
    }

    // VizVid 的結束事件也涵蓋手動停止；暫停不會觸發此事件。
    public void _onVideoEnd()
    {
        waitingForPlayback = true;
        activeRequest = "";
        pendingRequestUrl = null;
        ClearLyrics();
        songName = "";
        artistName = "";
        ShowStatus("");
        if (automaticLyrics != null) automaticLyrics.CancelRequest();
    }

    // 新的載入事件會重新開啟顯示，包括重播相同網址。
    public void _OnVideoBeginLoad()
    {
        waitingForPlayback = false;
        observedPlayerUrl = CurrentPlayerUrl();
        activeRequest = "";
        pendingRequestUrl = null;
        LoadForSong(observedPlayerUrl);
    }

    private void ClearLyrics()
    {
        translationTimes = new float[0]; translationEnds = new float[0]; translationTexts = new string[0];
        romanizationTimes = new float[0]; romanizationEnds = new float[0]; romanizationTexts = new string[0];
        translationSource = ""; romanizationSource = "";
        lineTimes = new float[0]; lineTexts = new string[0];
        wordTimes = new float[0]; wordTexts = new string[0];
        wordEnds = new float[0];
        lineEnds = new float[0]; lineWordStarts = new int[0]; lineWordCounts = new int[0];
        isYrc = false; displayedLine = -2; displayedWord = -2;
        lyricSource = "netease";
        lyricStatus = "正在載入歌詞";
        PublishTimeline();
    }

    // Keep the parsed timing source shared while preserving the existing TMP output.
    private void PublishTimeline()
    {
        // Rendering reads this snapshot independently; it cannot halt the original TMP writer.
        timelineRevision++;
    }

    private void ShowStatus(string status)
    {
        // This channel describes the download state, not the legacy per-frame lyric writer.
        // Empty old-TMP updates must not erase loading, retry, failure or confirmed no-lyrics status.
        if (lineTimes.Length == 0 && !string.IsNullOrEmpty(status)) lyricStatus = status;
        displayText = status; displayRevision++;
    }





    public void ClearAndShowStatus(string status)
    {
        if (!systemEnabled || waitingForPlayback) return;
        activeRequest = "";
        pendingRequestUrl = null;
        ClearLyrics();
        songName = "";
        artistName = "";
        ShowStatus(status);
    }
    public void ReceiveCoverData(DataDictionary data)
    {
        if (!coverEnabled || !systemEnabled) return;
        coverData = data;
        if (visualDisplay != null)
        {
            visualDisplay.SetProgramVariable("coverData", data);
            visualDisplay.SendCustomEvent("ReceivePendingCoverData");
        }
    }
    public void DisableSystem()
    {
        if (automaticLyrics != null) automaticLyrics.CancelRequest();
        activeRequest = ""; pendingRequestUrl = null;
        ClearLyrics(); songName = ""; artistName = ""; displayText = ""; displayRevision++;
        systemEnabled = false;
    }
    public void EnableSystem()
    {
        systemEnabled = true; waitingForPlayback = false;
        observedPlayerUrl = CurrentPlayerUrl();
        LoadForSong(observedPlayerUrl);
    }
}
