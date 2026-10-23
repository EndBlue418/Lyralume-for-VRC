using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

// Captured from the author's saved arrangement. References to LuraSwitch materials
// resolve from the user's installed copy; no third-party assets are distributed.
public static class LyralumeDebugLayout
{
    [Serializable] public class Preset { public Item[] items; }
    [Serializable] public class Item
    {
        public string key, name, caption; public int visualMode, orientation, useText;
        public Vector3 position, scale; public Quaternion rotation; public Node[] nodes;
    }
    [Serializable] public class Node
    {
        public string path, materialGuid, text; public long materialFileId;
        public Color color; public float fontSize; public int alignment, fontStyle;
        public Vector3 position, scale; public Quaternion rotation; public bool active, rect;
        public Vector2 anchorMin, anchorMax, pivot, size, offsetMin, offsetMax;
    }
    static Preset preset;
    public static Item Get(string key)
    {
        if (preset == null)
        {
            var path = AssetDatabase.FindAssets("DefaultDebugLayout t:TextAsset").Select(AssetDatabase.GUIDToAssetPath).First(p => p.EndsWith("/DefaultDebugLayout.json"));
            preset = JsonUtility.FromJson<Preset>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
        }
        return preset.items.First(i => i.key == key);
    }
    static Material Material(Node node)
    {
        if (string.IsNullOrEmpty(node.materialGuid) || node.materialGuid == "00000000000000000000000000000000") return null;
        string path = AssetDatabase.GUIDToAssetPath(node.materialGuid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().FirstOrDefault(m => {
            string guid; long id; AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out guid, out id); return id == node.materialFileId;
        });
    }
    public static void Apply(string key, Transform root)
    {
        var item = Get(key);
        foreach (var n in item.nodes)
        {
            var t = string.IsNullOrEmpty(n.path) ? root : root.Find(n.path);
            if (t == null) continue;
            var rect = t as RectTransform;
            if (n.rect && rect != null) { rect.anchorMin = n.anchorMin; rect.anchorMax = n.anchorMax; rect.pivot = n.pivot; rect.sizeDelta = n.size; }
            t.localPosition = n.position; t.localRotation = n.rotation; t.localScale = n.scale;
            t.gameObject.SetActive(n.active);
            var g = t.GetComponent<Graphic>(); var mat = Material(n);
            // The preview always needs this instance's RT material, rather than a saved scene asset.
            if (g != null && !(g is RawImage)) { if (mat != null) g.material = mat; g.color = n.color; }
            var renderer = t.GetComponent<Renderer>(); if (renderer != null && mat != null) renderer.sharedMaterial = mat;
            var text = t.GetComponent<TMP_Text>();
            if (text != null)
            {
                if (n.text != null) text.text = n.text;
                if (n.fontSize > 0) text.fontSize = n.fontSize;
                text.fontStyle = (FontStyles)n.fontStyle; text.alignment = (TextAlignmentOptions)n.alignment;
            }
        }
        root.name = item.name; root.localPosition = item.position; root.localRotation = item.rotation; root.localScale = item.scale;
    }
}
