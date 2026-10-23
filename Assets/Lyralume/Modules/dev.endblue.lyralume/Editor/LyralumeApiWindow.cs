using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Endpoint configuration is project-wide; visual/feature settings remain per player.
public class LyralumeApiWindow : EditorWindow
{
    public const string SettingsPath = "Assets/LyralumeGenerated/ProjectEndpoints.asset";
    Editor inspector;
    Vector2 scroll;
    [MenuItem("Tools/Lyralume/API 網址與優先順序", priority = 20)]
    public static void Open() { GetWindow<LyralumeApiWindow>("Lyralume API"); }
    public static LyralumeProfile GetSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LyralumeProfile>(SettingsPath);
        if (settings != null) return settings;
        if (!AssetDatabase.IsValidFolder("Assets/LyralumeGenerated")) AssetDatabase.CreateFolder("Assets", "LyralumeGenerated");
        settings = CreateInstance<LyralumeProfile>();
        settings.endpoints = new LyralumeEndpoint[0];
        AssetDatabase.CreateAsset(settings, SettingsPath);
        return settings;
    }
    public static void CopyEndpoints(LyralumeProfile destination)
    {
        var source = GetSettings(); source.ValidatedEntries();
        destination.endpoints = source.endpoints.Select(e => e == null ? null : new LyralumeEndpoint {
            enabled = e.enabled, name = e.name, baseUrl = e.baseUrl, proxyPath = e.proxyPath, udonPath = e.udonPath
        }).ToArray();
        destination.failuresPerEndpoint = source.failuresPerEndpoint;
        destination.maxPendingResponses = source.maxPendingResponses;
    }
    public static void ApplyProjectEndpoints(LyralumeInstance instance)
    {
        if (instance == null || !instance.useProjectEndpoints) return;
        var profile = instance.profile as LyralumeProfile;
        if (profile == null) throw new InvalidOperationException("Lyralume 缺少安裝設定，請重新完成設定。");
        CopyEndpoints(profile);
        LyralumeSetupWindow.Apply(instance, profile);
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("請填入自己的相容 API，預設不提供伺服器網址。未設定時不發送自動請求。由上而下嘗試 API。可停用、增加或調整順序。設定只影響此專案中使用共用 API 的 Lyralume；建置時自動套用。", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var so = new SerializedObject(GetSettings());
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("endpoints"), new GUIContent("API（上方優先）"), true);
        EditorGUILayout.PropertyField(so.FindProperty("failuresPerEndpoint"), new GUIContent("每站重試次數"));
        EditorGUILayout.PropertyField(so.FindProperty("maxPendingResponses"), new GUIContent("等待回應上限"));
        so.ApplyModifiedProperties();
        if (GUILayout.Button("套用到目前開啟場景的 Lyralume"))
        {
            try {
                GetSettings().ValidatedEntries();
                foreach (var i in FindObjectsOfType<LyralumeInstance>(true).Where(i => i.gameObject.scene.IsValid())) ApplyProjectEndpoints(i);
                AssetDatabase.SaveAssets(); ShowNotification(new GUIContent("API 設定已套用"));
            } catch (Exception e) { Debug.LogException(e); ShowNotification(new GUIContent(e.Message)); }
        }
        EditorGUILayout.EndScrollView();
    }
}
