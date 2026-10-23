using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharpEditor;
using VRC.SDKBase;

[Serializable]
public class LyralumeEndpoint
{
    public bool enabled = true;
    public string name = "API";
    public string baseUrl = "";
    public string proxyPath = "/api/proxy";
    public string udonPath = "/api/udon";
}

// Editor-only configuration. Udon receives complete, pre-generated VRCUrls.
[CreateAssetMenu(menuName = "Lyralume/設定檔")]
public class LyralumeProfile : ScriptableObject
{
    public LyralumeEndpoint[] endpoints = new LyralumeEndpoint[0];
    public bool automaticDownload = true;
    public bool prefetchNextSong = true;
    public bool wordHighlighting = true;
    public bool translation = true;
    public bool romanization = true;
    public bool cover = true;
    [Min(1)] public int failuresPerEndpoint = 2;
    [Min(1)] public int maxPendingResponses = 6;



    public LyralumeEndpoint[] ValidatedEntries()
    {
        var active = (endpoints ?? new LyralumeEndpoint[0]).Where(e => e != null && e.enabled).ToArray();
        if (failuresPerEndpoint < 1 || maxPendingResponses < 1) throw new InvalidOperationException("重試及等待次數必須至少為 1。");
        foreach (var e in active)
        {
            Uri uri;
            if (!Uri.TryCreate(e.baseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out uri) ||
                uri.Scheme != "https" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
                !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(e.name + "：請輸入不含參數或帳密的 HTTPS 基底網址。");
            ValidatePath(e.proxyPath); ValidatePath(e.udonPath);
        }
        if (active.Select(e => Root(e) + e.udonPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != active.Length)
            throw new InvalidOperationException("啟用的 API 站點重複，請移除或停用重複項目。");
        return active;
    }

    static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/") || path.StartsWith("//") ||
            path.Contains("?") || path.Contains("#") || path.Contains(":") || path.Contains("\\") || path.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("API 路徑必須以 / 開頭，且不能含參數、空格或網域。");
    }
    static string Root(LyralumeEndpoint entry) { return entry.baseUrl.Trim().TrimEnd('/'); }
    [Serializable] class TableSignature { public LyralumeEndpoint[] entries; public string[] songIds; }
    public string Signature(LyralumeController controller)
    {
        using (var sha = SHA256.Create())
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(
                "jagged-v3|" + JsonUtility.ToJson(new TableSignature { entries = ValidatedEntries(), songIds = controller.songIds }))));
    }

    public void Apply(LyralumeController controller, LyralumeNetEaseProvider automatic)
    {
        var settings = this;
        controller.wordHighlighting = wordHighlighting;
        controller.translationEnabled = translation; controller.romanizationEnabled = romanization; controller.coverEnabled = cover;
        automatic.autoDownloadEnabled = false; automatic.prefetchNextSong = prefetchNextSong;
        automatic.failuresPerEndpoint = failuresPerEndpoint; automatic.maxPendingResponses = maxPendingResponses;
        var entries = settings.ValidatedEntries();
        automatic.autoDownloadEnabled = automaticDownload && entries.Length > 0;
        string signature = settings.Signature(controller);
        if (entries.Length == 0)
        {
            controller.lyricRequestUrl = null;
            controller.proxyApiUrl = ""; controller.proxyApiUrls = new string[0];
            controller.endpointSongLyricUrls = new VRCUrl[0]; controller.songLyricUrls = new VRCUrl[0];
            controller.automaticLyrics = automatic; automatic.controller = controller;
            automatic.endpointNames = new string[0]; automatic.catalogueUrls = new VRCUrl[0]; automatic.catalogueUrl = null;
            automatic.endpointBeginUrls = new VRCUrl[0][]; automatic.endpointPartUrls = new VRCUrl[0][];
            automatic.beginUrls = new VRCUrl[0]; automatic.partUrls = new VRCUrl[0];
            automatic.endpointConfiguration = signature;
            UdonSharpEditorUtility.CopyProxyToUdon(automatic); UdonSharpEditorUtility.CopyProxyToUdon(controller);
            EditorUtility.SetDirty(automatic); EditorUtility.SetDirty(controller);
            return;
        }
        if (automatic.endpointConfiguration == signature && automatic.HasEndpointTables() &&
            automatic.EndpointCount() == entries.Length && controller.proxyApiUrls.Length == entries.Length &&
            controller.endpointSongLyricUrls.Length == controller.songIds.Length * entries.Length)
        {
            UdonSharpEditorUtility.CopyProxyToUdon(automatic); UdonSharpEditorUtility.CopyProxyToUdon(controller);
            EditorUtility.SetDirty(automatic); EditorUtility.SetDirty(controller);
            return;
        }
        Debug.Log("[Lyralume] Generating tables for " + controller.name);
        if (!Application.isBatchMode)
        {
            Undo.RecordObject(controller, "Configure API endpoints");
            Undo.RecordObject(automatic, "Configure API endpoints");
        }
        var catalogue = new VRCUrl[entries.Length];
        var begin = new VRCUrl[entries.Length][];
        var partUrls = new VRCUrl[entries.Length][];
        var names = new string[entries.Length];
        var proxies = new string[entries.Length];
        var songs = new VRCUrl[entries.Length * controller.songIds.Length];
        for (int endpoint = 0; endpoint < entries.Length; endpoint++)
        {
            begin[endpoint] = new VRCUrl[1000];
            partUrls[endpoint] = new VRCUrl[48000];
            var e = entries[endpoint];
            string udon = Root(e) + e.udonPath;
            proxies[endpoint] = Root(e) + e.proxyPath;
            names[endpoint] = string.IsNullOrWhiteSpace(e.name) ? Root(e) : e.name;
            catalogue[endpoint] = new VRCUrl(udon + "?op=catalogue");
            for (int value = 0; value < 1000; value++)
                begin[endpoint][value] = new VRCUrl(udon + "?op=begin&value=" + value);
            for (int part = 1; part < 4; part++) for (int slot = 0; slot < 16; slot++) for (int value = 0; value < 1000; value++)
                partUrls[endpoint][((part - 1) * 16 + slot) * 1000 + value] =
                    new VRCUrl(udon + "?op=part&slot=" + slot + "&part=" + part + "&value=" + value);
            for (int song = 0; song < controller.songIds.Length; song++)
            {
                var id = controller.songIds[song];
                if (string.IsNullOrEmpty(id) || id.Length > 12 || id.Any(c => c < '0' || c > '9') || id[0] == '0')
                    throw new InvalidOperationException("歌曲 ID 必須為 1 至 12 位數字：" + id);
                songs[endpoint * controller.songIds.Length + song] = new VRCUrl(proxies[endpoint] + "?url=" +
                    Uri.EscapeDataString("https://music.163.com/song?id=" + id));
            }
        }
        // Rebase only the known legacy proxy request. Preserve deliberately supplied custom URLs.
        string initial = controller.lyricRequestUrl == null ? "" : (controller.lyricRequestUrl.Get() ?? "");
        if (!string.IsNullOrEmpty(controller.proxyApiUrl) && initial.StartsWith(controller.proxyApiUrl + "?", StringComparison.Ordinal))
            controller.lyricRequestUrl = new VRCUrl(proxies[0] + initial.Substring(controller.proxyApiUrl.Length));
        controller.proxyApiUrls = proxies;
        controller.proxyApiUrl = proxies[0];
        controller.endpointSongLyricUrls = songs;
        controller.songLyricUrls = songs.Take(controller.songIds.Length).ToArray();
        automatic.controller = controller;
        controller.automaticLyrics = automatic;
        automatic.endpointNames = names;
        automatic.catalogueUrls = catalogue;
        automatic.catalogueUrl = catalogue[0];
        automatic.endpointBeginUrls = begin;
        automatic.endpointPartUrls = partUrls;
        automatic.beginUrls = new VRCUrl[0];
        automatic.partUrls = new VRCUrl[0];
        automatic.failuresPerEndpoint = settings.failuresPerEndpoint;
        automatic.maxPendingResponses = settings.maxPendingResponses;
        automatic.endpointConfiguration = signature;
        Debug.Log("[Lyralume] Copying URL tables to Udon " + controller.name);
        UdonSharpEditorUtility.CopyProxyToUdon(automatic);
        UdonSharpEditorUtility.CopyProxyToUdon(controller);
        Debug.Log("[Lyralume] URL tables copied " + controller.name);
        foreach (var c in new Component[] { controller, automatic })
        {
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            EditorUtility.SetDirty(c);
        }
        if (controller.gameObject.scene.IsValid() && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

}
