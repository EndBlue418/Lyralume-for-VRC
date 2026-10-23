using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UdonSharpEditor;

public class LyralumeDebugInstaller : EditorWindow
{
    LyralumeInstance target;
    bool split = true;
    bool defaultOpen;
    [MenuItem("Tools/Lyralume/安裝 Debug 監看器", priority = 30)]
    public static void Open() { GetWindow<LyralumeDebugInstaller>("Lyralume Debug"); }
    void OnEnable() { if (Selection.activeGameObject != null) target = Selection.activeGameObject.GetComponentInParent<LyralumeInstance>(); }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("預設左側預覽、右側 Runtime，控制項整合在 Runtime 面板。標準版使用 LuraSwitch 2；簡易版使用 Unity UI。", MessageType.Info);
        var root = (GameObject)EditorGUILayout.ObjectField("Lyralume", target == null ? null : target.gameObject, typeof(GameObject), true);
        target = root == null ? null : root.GetComponent<LyralumeInstance>();
        split = EditorGUILayout.Popup("視窗版本", split ? 1 : 0, new[] { "簡易版 · Unity UI", "標準雙面板 · LuraSwitch 2" }) == 1;
        defaultOpen = EditorGUILayout.Toggle("遊戲開始時顯示 Debug", defaultOpen);
        EditorGUILayout.HelpBox("預設關閉；外側的 Debug 開關會保留。編輯模式仍顯示面板，方便擺放。", MessageType.None);
        if (split && !LyralumeDebugUI.HasLuraSwitch) EditorGUILayout.HelpBox("請先匯入 LuraSwitch 2，再安裝分體版。套件不內含 LuraSwitch 本體。", MessageType.Warning);
        using (new EditorGUI.DisabledScope(target == null || EditorApplication.isPlayingOrWillChangePlaymode || (split && !LyralumeDebugUI.HasLuraSwitch)))
            if (GUILayout.Button("安裝 Debug"))
                try { Selection.activeGameObject = Install(target, split, defaultOpen).gameObject; }
                catch (Exception e) { Debug.LogException(e); ShowNotification(new GUIContent(e.Message)); }
    }
    public static LyralumeDebugMonitor Install(LyralumeInstance target) { return Install(target, true, false); }
    public static LyralumeDebugMonitor Install(LyralumeInstance target, bool split)
    { return Install(target, split, false); }
    public static LyralumeDebugMonitor Install(LyralumeInstance target, bool split, bool defaultOpen)
    {
        if (target == null || target.lyrics == null || !(target.lyrics.visualDisplay is LyralumeFumeDisplay)) throw new InvalidOperationException("請指定完成配置的 Lyralume。");
        if (split) LyralumeDebugUI.ValidateLuraSwitch();
        var existing = FindObjectsOfType<LyralumeDebugMonitor>(true).FirstOrDefault(m => m.target == target.lyrics && m.gameObject.scene == target.gameObject.scene && (m.transform.Find("Split Panels") != null) == split);
        if (existing != null)
        {
            if (split) { LyralumeDebugUI.UpgradeToggle(existing); EditorSceneManager.MarkSceneDirty(existing.gameObject.scene); }
            return existing;
        }
        var path = AssetDatabase.FindAssets("LyralumeDebug t:Prefab").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => System.IO.Path.GetFileName(p) == "LyralumeDebug.prefab");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException("缺少 LyralumeDebug Prefab。");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, target.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(root, "Install Lyralume Debug");
        root.name = (split ? "Lyralume Debug Split · " : "Lyralume Debug · ") + target.name;
        root.transform.SetPositionAndRotation(target.transform.position + new Vector3(0, 1.6f, 1f), target.transform.rotation);
        try
        {
            var monitor = root.GetComponent<LyralumeDebugMonitor>(); monitor.target = target.lyrics; monitor.logToConsole = false; monitor.defaultOpen = defaultOpen;
            var overview = LyralumeOverviewSetup.Install(monitor, (LyralumeFumeDisplay)target.lyrics.visualDisplay);
            overview.openOnEnable = defaultOpen;
            LyralumeDebugUI.Build(monitor, overview, split);
            UdonSharpEditorUtility.CopyProxyToUdon(monitor); PrefabUtility.RecordPrefabInstancePropertyModifications(monitor);
            EditorSceneManager.MarkSceneDirty(root.scene); return monitor;
        }
        catch { Undo.DestroyObjectImmediate(root); throw; }
    }
}
