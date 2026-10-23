using UnityEditor;
using UnityEngine;
using UdonSharpEditor;

// Do not traverse baked URL tables on Inspector repaint.
public abstract class LyralumeCompactInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        var c = (Component)target;
        EditorGUILayout.LabelField("Lyralume · 內部模組", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("此模組由安裝工具配置。網址表與渲染資料不在 Inspector 展開。", MessageType.None);
        var i = c.GetComponentInParent<LyralumeInstance>();
        if (i != null && GUILayout.Button("選取 Lyralume 設定")) Selection.activeGameObject = i.gameObject;
    }
}
[CustomEditor(typeof(LyralumeNetEaseProvider))] public class LyralumeProviderInspector : LyralumeCompactInspector { }
[CustomEditor(typeof(LyralumeController))] public class LyralumeControllerInspector : LyralumeCompactInspector { }
[CustomEditor(typeof(LyralumePlaybackState))] public class LyralumePlaybackInspector : LyralumeCompactInspector { }
[CustomEditor(typeof(LyralumeScreenOutput))] public class LyralumeOutputInspector : LyralumeCompactInspector { }
