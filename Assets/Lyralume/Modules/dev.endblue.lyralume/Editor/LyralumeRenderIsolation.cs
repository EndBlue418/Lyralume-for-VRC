using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class LyralumeRenderIsolation
{
    public static int AcquireLayer()
    {
        var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene.IsValid() && r.gameObject.scene.isLoaded).ToArray();
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        // Reserve against inactive renderers too: a closed Debug window can reopen later.
        for (int n = 22; n <= 30; n++)
        {
            string name = layers.GetArrayElementAtIndex(n).stringValue;
            if ((!string.IsNullOrEmpty(name) && !name.StartsWith("Lyralume", StringComparison.Ordinal)) || renderers.Any(r => r.gameObject.layer == n)) continue;
            if (string.IsNullOrEmpty(name)) { layers.GetArrayElementAtIndex(n).stringValue = "Lyralume Render " + n; tags.ApplyModifiedProperties(); }
            return n;
        }
        throw new InvalidOperationException("沒有可用的獨立渲染圖層（22–30）。每套 Lyralume 主顯示、每個 Debug 各需要一層，請保留可用圖層後再安裝。");
    }
    public static Vector3 AcquirePosition(float firstY)
    {
        var occupied = UnityEngine.Object.FindObjectsOfType<Camera>(true)
            .Where(c => c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded && c.transform.parent != null && LayerMask.LayerToName(c.gameObject.layer).StartsWith("Lyralume", StringComparison.Ordinal))
            .Select(c => c.transform.parent.position).ToArray();
        for (int slot = 0; slot < 1000; slot++)
        {
            var candidate = new Vector3(0, firstY - slot * 1000f, 0);
            if (occupied.All(p => Vector3.Distance(candidate, p) >= 500f)) return candidate;
        }
        throw new InvalidOperationException("找不到未占用的隱藏舞台位置。");
    }
}
