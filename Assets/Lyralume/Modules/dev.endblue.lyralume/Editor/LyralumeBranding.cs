using System.Linq;
using UnityEngine;
using UnityEditor;

public class LyralumeBranding : EditorWindow
{
    static Texture2D banner;
    public static void DrawBanner()
    {
        if (banner == null)
        {
            var path = AssetDatabase.FindAssets("Lyralume-Banner t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.Contains("/Editor/Branding/"));
            if (path != null) banner = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        if (banner != null) { var rect = GUILayoutUtility.GetAspectRect(1120f / 360f); EditorGUI.DrawRect(rect, new Color(.045f, .035f, .065f)); GUI.DrawTexture(rect, banner, ScaleMode.ScaleToFit); }
    }
    [MenuItem("Tools/Lyralume/關於 Lyralume", priority = 90)]
    public static void Open() { GetWindow<LyralumeBranding>("About Lyralume"); }
    void OnGUI()
    {
        DrawBanner();
        EditorGUILayout.LabelField("Lyralume 1.0.0", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Created by EndBlue · Assisted by Codex");
        EditorGUILayout.HelpBox("VizVid 專用歌詞系統。請自行設定相容 API。授權、來源與第三方聲明隨套件附上。", MessageType.Info);
    }
}
