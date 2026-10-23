using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using UdonSharp;
using UdonSharpEditor;

public static class LyralumeSetupWindow
{
    public static LyralumeInstance CreateInstanceRoot()
    {
        var root = new GameObject("Lyralume"); Undo.RegisterCreatedObjectUndo(root, "Create Lyralume");
        var i = root.AddComponent<LyralumeInstance>();
        var runtime = new GameObject("Runtime"); runtime.transform.SetParent(root.transform, false);
        i.playback = runtime.AddUdonSharpComponent<LyralumePlaybackState>();
        i.lyrics = runtime.AddUdonSharpComponent<LyralumeController>();
        i.provider = runtime.AddUdonSharpComponent<LyralumeNetEaseProvider>();
        i.screenOutput = runtime.AddUdonSharpComponent<LyralumeScreenOutput>();
        i.lyrics.playback = i.playback; i.lyrics.automaticLyrics = i.provider;
        i.provider.controller = i.lyrics; i.screenOutput.lyrics = i.lyrics;
        // The screen module stays off until a camera/texture and bindings have been configured.
        i.screenOutput.outputEnabled = false;
        foreach (var behaviour in i.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        return i;
    }
    public static void ConfigurePlayer(LyralumeInstance i, int selection, AudioSource audio, Component core, Component playlist)
    {
        if (i == null || i.playback == null) throw new InvalidOperationException("請指定目標 Lyralume 實例。");
        UdonSharpBehaviour adapter = null;
        if (selection == 1)
        {
            if (audio == null) throw new InvalidOperationException("請指定 AudioSource。");
            var a = i.GetComponentInChildren<LyralumeAudioSourceAdapter>(true) ?? i.playback.gameObject.AddUdonSharpComponent<LyralumeAudioSourceAdapter>();
            a.source = audio; a.playback = i.playback; adapter = a;
        }
        if (selection == 2)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("LyralumeVizVidAdapter")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("請先安裝 Lyralume VizVid 接點套件。");
            if (core == null || core.GetType().FullName != "JLChnToZ.VRC.VVMW.Core") throw new InvalidOperationException("請指定 VizVid Core 元件。");
            var a = i.GetComponentInChildren(type, true) ?? i.playback.gameObject.AddUdonSharpComponent(type);
            type.GetField("core").SetValue(a, core); type.GetField("playlist").SetValue(a, playlist);
            type.GetField("playback").SetValue(a, i.playback); adapter = (UdonSharpBehaviour)a;
        }
        foreach (var a in i.GetComponentsInChildren<UdonSharpBehaviour>(true))
            if (a is LyralumeAudioSourceAdapter || a.GetType().Name == "LyralumeVizVidAdapter") a.enabled = a == adapter;
        i.playback.adapter = adapter;
        foreach (var a in i.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(a);
        RecordInstance(i);
        EditorSceneManager.MarkSceneDirty(i.gameObject.scene);
    }
    public static void Apply(LyralumeInstance i, LyralumeProfile p)
    {
        if (i == null || p == null) throw new InvalidOperationException("請指定目標實例與設定檔。");
        if (i.lyrics == null || i.provider == null) throw new InvalidOperationException("實例缺少歌詞核心或資料來源。");
        ValidateScreenBindings(i);
        p.Apply(i.lyrics, i.provider); i.profile = p;
        foreach (var a in i.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(a);
        RecordInstance(i);
        EditorUtility.SetDirty(i); EditorSceneManager.MarkSceneDirty(i.gameObject.scene);
    }
    public static void RecordInstance(LyralumeInstance i)
    {
        foreach (var c in i.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            EditorUtility.SetDirty(c);
            if (PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        }
    }
    public static void ValidateScreenBindings(LyralumeInstance i)
    {
        var output = i.screenOutput;
        if (output == null || output.screens == null || output.screens.Length == 0) return;
        if (output.materialSlots == null || output.materialSlots.Length != output.screens.Length) throw new InvalidOperationException("每個螢幕需要對應一個 materialSlots 數值。");
        for (int n = 0; n < output.screens.Length; n++)
        {
            var screen = output.screens[n]; int slot = output.materialSlots[n];
            if (screen == null || slot < 0 || slot >= screen.sharedMaterials.Length) throw new InvalidOperationException("螢幕或材質槽無效。");
            for (int m = 0; m < n; m++) if (screen == output.screens[m] && slot == output.materialSlots[m]) throw new InvalidOperationException("螢幕材質槽重複綁定。");
            foreach (var other in UnityEngine.Object.FindObjectsOfType<LyralumeScreenOutput>(true))
            {
                if (other == output || !other.gameObject.scene.IsValid()) continue;
                for (int m = 0; m < other.screens.Length && m < other.materialSlots.Length; m++)
                    if (other.screens[m] == screen && other.materialSlots[m] == slot) throw new InvalidOperationException("另一個 Lyralume 實例已綁定此螢幕材質槽。");
            }
        }
    }
    public static void BakeScreens(LyralumeInstance i)
    {
        if (i == null || i.screenOutput == null) throw new InvalidOperationException("請指定目標實例。");
        ValidateScreenBindings(i); var o = i.screenOutput;
        if (o.sourceCamera == null) throw new InvalidOperationException("請指定此實例專用相機；相機應只拍攝歌詞層。");
        if (o.screens.Length == 0) throw new InvalidOperationException("請先加入螢幕。");
        string folder = "Assets/LyralumeGenerated";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "LyralumeGenerated");
        if (o.texture == null)
        {
            o.texture = new RenderTexture(1920, 1080, 24); o.texture.name = "Lyralume Output";
            AssetDatabase.CreateAsset(o.texture, AssetDatabase.GenerateUniqueAssetPath(folder + "/Output.renderTexture"));
        }
        var originals = new Material[o.screens.Length]; var displays = new Material[o.screens.Length];
        for (int n = 0; n < o.screens.Length; n++)
        {
            var current = o.screens[n].sharedMaterials[o.materialSlots[n]];
            originals[n] = o.displayMaterials != null && n < o.displayMaterials.Length && current == o.displayMaterials[n] && o.originalMaterials != null && n < o.originalMaterials.Length ? o.originalMaterials[n] : current;
            var material = new Material(Shader.Find("Unlit/Texture")); material.mainTexture = o.texture;
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(folder + "/Screen.mat")); displays[n] = material;
        }
        o.originalMaterials = originals; o.displayMaterials = displays; o.sourceCamera.targetTexture = o.texture; o.outputEnabled = true;
        UdonSharpEditorUtility.CopyProxyToUdon(o); EditorUtility.SetDirty(o); EditorSceneManager.MarkSceneDirty(i.gameObject.scene);
        RecordInstance(i);
    }
}

// Operates only on Lyralume instances in the scene being built; no project-wide migration.
public class LyralumeBuild : IProcessSceneWithReport
{
    public int callbackOrder { get { return -10000; } }
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        foreach (var root in scene.GetRootGameObjects()) foreach (var i in root.GetComponentsInChildren<LyralumeInstance>(true))
        {
            LyralumeApiWindow.ApplyProjectEndpoints(i);
            if (i.profile == null) throw new BuildFailedException("Lyralume 尚未指定設定檔。");
            LyralumeSetupWindow.Apply(i, i.profile as LyralumeProfile);
            UnityEngine.Object.DestroyImmediate(i);
        }
    }
}


