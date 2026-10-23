using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using TMPro;
using JLChnToZ.VRC.VVMW;

public class LyralumeQuickSetup : EditorWindow
{
    public class Screen
    {
        public Renderer renderer;
        public int slot;
        public bool selected = true;
        public string Label { get { return renderer.name + (renderer.sharedMaterials.Length > 1 ? " · 畫面 " + (slot + 1) : ""); } }
    }
    Core player;
    LyralumeInstance instance;
    List<Screen> screens = new List<Screen>();
    Vector2 scroll;
    string message;
    [MenuItem("Tools/Lyralume/安裝與設定", priority = 0)]
    public static void Open() { GetWindow<LyralumeQuickSetup>("Lyralume"); }
    public static void Edit(LyralumeInstance target)
    {
        var window = GetWindow<LyralumeQuickSetup>("Lyralume");
        window.instance = target; window.player = target.vizVid as Core;
        if (window.player == null && target.playback != null && target.playback.adapter != null)
        {
            var so = new SerializedObject(target.playback.adapter);
            var field = so.FindProperty("core"); if (field != null) window.player = field.objectReferenceValue as Core;
        }
        window.Rescan();
    }
    void OnEnable()
    {
        if (Selection.activeGameObject != null)
        {
            instance = Selection.activeGameObject.GetComponentInParent<LyralumeInstance>();
            if (instance != null) player = instance.vizVid as Core;
            if (player == null) player = Selection.activeGameObject.GetComponentInParent<Core>();
        }
        Rescan();
    }
    void Rescan()
    {
        screens = player == null ? new List<Screen>() : DiscoverScreens(player);
        if (instance == null && player != null) instance = FindInstance(player);
        if (instance != null && instance.installationComplete && instance.screenOutput != null)
            foreach (var s in screens) s.selected = Enumerable.Range(0, instance.screenOutput.screens.Length)
                .Any(n => instance.screenOutput.screens[n] == s.renderer && instance.screenOutput.materialSlots[n] == s.slot);
    }
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        LyralumeBranding.DrawBanner();
        GUILayout.Label("Lyralume", EditorStyles.largeLabel);
        EditorGUILayout.LabelField("1  選擇 VizVid", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        var chosen = (GameObject)EditorGUILayout.ObjectField("VizVid 播放器", player == null ? null : player.gameObject, typeof(GameObject), true);
        if (EditorGUI.EndChangeCheck())
        {
            player = chosen == null ? null : chosen.GetComponent<Core>() ?? chosen.GetComponentInChildren<Core>(true) ?? chosen.GetComponentInParent<Core>();
            if (instance == null || instance.installationComplete) instance = null; Rescan();
            message = chosen != null && player == null ? "這個物件內找不到 VizVid，請拖入播放器根物件。" : null;
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2  勾選螢幕", EditorStyles.boldLabel);
        if (player != null && GUILayout.Button("重新讀取 VizVid 螢幕")) Rescan();
        if (screens.Count == 0) EditorGUILayout.HelpBox("選擇播放器後，這裡會列出已向 VizVid 登記的螢幕。新增螢幕請先使用 VizVid 的 Screen Configurator 指定同一播放器。", MessageType.Info);
        foreach (var screen in screens)
        {
            EditorGUILayout.BeginHorizontal(); screen.selected = EditorGUILayout.ToggleLeft(screen.Label, screen.selected);
            if (GUILayout.Button("定位", GUILayout.Width(48))) EditorGUIUtility.PingObject(screen.renderer);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        if (player != null)
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("AudioLink（跟隨 VizVid）", ConfiguredAudioLink(player), typeof(AudioLink.AudioLink), true);
        EditorGUILayout.HelpBox("自動配置播放器接點、字型、材質與渲染資源。API 在工具列統一管理；各播放器保留自己的獨立輸出。", MessageType.None);
        using (new EditorGUI.DisabledScope(player == null || !screens.Any(s => s.selected) || EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button(instance == null ? "3  完成安裝" : "3  更新設定", GUILayout.Height(36)))
            {
                try {
                    instance = Install(player, screens.Where(s => s.selected).ToArray(), instance);
                    message = "設定完成。可在 Lyralume 物件調整功能，螢幕會自動切換與還原。";
                    Selection.activeGameObject = instance.gameObject;
                } catch (Exception e) { message = e.Message; Debug.LogException(e); }
            }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        if (GUILayout.Button("API 網址與優先順序")) LyralumeApiWindow.Open();
        EditorGUILayout.EndScrollView();
    }
    public static LyralumeInstance FindInstance(Core core)
    {
        return FindObjectsOfType<LyralumeInstance>(true).FirstOrDefault(i => i.gameObject.scene.IsValid() &&
            (i.vizVid == core || (i.playback != null && i.playback.adapter != null &&
             new SerializedObject(i.playback.adapter).FindProperty("core")?.objectReferenceValue == core)));
    }
    public static List<Screen> DiscoverScreens(Core core)
    {
        var result = new List<Screen>();
        Action<Renderer, int, string> add = (renderer, slot, property) => {
            if (renderer == null || renderer.gameObject.scene != core.gameObject.scene) return;
            var materials = renderer.sharedMaterials;
            for (int n = 0; n < materials.Length; n++)
            {
                if (slot >= 0 && n != slot) continue;
                if (materials[n] == null || (!string.IsNullOrEmpty(property) && !materials[n].HasProperty(property))) continue;
                if (!result.Any(s => s.renderer == renderer && s.slot == n)) result.Add(new Screen { renderer = renderer, slot = n });
            }
        };
        // ScreenConfigurator is editor-only and can contribute bindings not baked into Core yet.
        foreach (var c in FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null && c.GetType().FullName == "JLChnToZ.VRC.VVMW.Designer.ScreenConfigurator"))
        {
            var so = new SerializedObject(c);
            if (so.FindProperty("core")?.objectReferenceValue != core) continue;
            var renderer = so.FindProperty("screenRenderer")?.objectReferenceValue as Renderer;
            if (renderer == null) renderer = c.GetComponentInChildren<Renderer>(true);
            add(renderer, so.FindProperty("targetIndex").intValue, so.FindProperty("targetPropertyName").stringValue);
        }
        var data = new SerializedObject(core);
        var targets = data.FindProperty("screenTargets"); var indices = data.FindProperty("screenTargetIndeces");
        var properties = data.FindProperty("screenTargetPropertyNames");
        for (int n = 0; targets != null && n < targets.arraySize; n++)
        {
            var target = targets.GetArrayElementAtIndex(n).objectReferenceValue;
            string property = properties != null && n < properties.arraySize ? properties.GetArrayElementAtIndex(n).stringValue : "";
            var renderer = target as Renderer;
            if (renderer != null) add(renderer, indices != null && n < indices.arraySize ? indices.GetArrayElementAtIndex(n).intValue : -1, property);
            var material = target as Material;
            if (material != null)
                foreach (var r in FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene == core.gameObject.scene))
                    for (int slot = 0; slot < r.sharedMaterials.Length; slot++) if (r.sharedMaterials[slot] == material) add(r, slot, property);
        }
        return result.OrderBy(s => s.renderer.name).ThenBy(s => s.slot).ToList();
    }
    public static LyralumeInstance Install(Core core, Screen[] chosen, LyralumeInstance target = null)
    {
        LyralumeAudioAnalysisSetup.EnsureProgram();
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("請先停止播放模式。");
        if (core == null || !core.gameObject.scene.IsValid() || EditorUtility.IsPersistent(core)) throw new InvalidOperationException("請選擇目前場景中的 VizVid。");
        if (target == null) target = FindInstance(core);
        if (target != null && target.vizVid != null && target.vizVid != core) throw new InvalidOperationException("每份 Lyralume 只對應一個播放器，請另建一份。");
        if (target != null && target.gameObject.scene != core.gameObject.scene) throw new InvalidOperationException("播放器與 Lyralume 必須在同一場景。");
        if (chosen == null || chosen.Length == 0) throw new InvalidOperationException("請勾選至少一個 VizVid 螢幕。");
        var available = DiscoverScreens(core);
        foreach (var s in chosen)
        {
            if (!available.Any(a => a.renderer == s.renderer && a.slot == s.slot)) throw new InvalidOperationException("選取的螢幕已不在此 VizVid 的配置內，請重新讀取。");
            foreach (var other in FindObjectsOfType<LyralumeScreenOutput>(true))
                if (target == null || other != target.screenOutput)
                    for (int n = 0; n < other.screens.Length && n < other.materialSlots.Length; n++)
                        if (other.screens[n] == s.renderer && other.materialSlots[n] == s.slot) throw new InvalidOperationException("螢幕已由另一套 Lyralume 控制。");
        }
        if (chosen.Select(s => s.renderer.GetInstanceID() + ":" + s.slot).Distinct().Count() != chosen.Length) throw new InvalidOperationException("螢幕重複。");
        var configuredLtcgi = FindLtcgi(core);
        LyralumeApiWindow.GetSettings().ValidatedEntries();
        var font = AssetDatabase.FindAssets("LyralumeReferenceFont t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f != null);
        if (font == null) throw new InvalidOperationException("缺少 Lyralume 內附字型，請重新匯入完整套件。");
        bool created = target == null;
        if (created)
        {
            var path = AssetDatabase.FindAssets("Lyralume t:Prefab").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => System.IO.Path.GetFileName(p) == "Lyralume.prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("缺少 Lyralume Prefab，請重新匯入完整套件。");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, core.gameObject.scene); Undo.RegisterCreatedObjectUndo(root, "Install Lyralume");
            target = root.GetComponent<LyralumeInstance>(); target.name = "Lyralume · " + core.name;
            foreach (var component in target.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyUdonToProxy(component);
        }
        target.lyrics.playback = target.playback; target.lyrics.automaticLyrics = target.provider;
        target.provider.controller = target.lyrics; target.screenOutput.lyrics = target.lyrics;
        var output = target.screenOutput;
        var view = target.lyrics.visualDisplay as LyralumeFumeDisplay;
        if (view != null) view.SetActive(false);
        var oldScreens = output.screens; var oldSlots = output.materialSlots;
        var oldOriginals = output.originalMaterials; var oldMaterials = output.displayMaterials;
        output.screens = chosen.Select(s => s.renderer).ToArray(); output.materialSlots = chosen.Select(s => s.slot).ToArray();
        var frontend = FindObjectsOfType<FrontendHandler>(true).FirstOrDefault(f => f.core == core);
        LyralumeSetupWindow.ConfigurePlayer(target, 2, null, core, frontend);
        target.vizVid = core;
        if (view == null) view = LyralumeDisplaySetup.Install(target, font, DisplayLayer());
        else
        {
            var originals = new Material[chosen.Length]; var displays = new Material[chosen.Length];
            string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(view.output)).Replace('\\', '/');
            for (int n = 0; n < chosen.Length; n++)
            {
                int prior = Array.FindIndex(oldScreens, r => r == chosen[n].renderer);
                if (prior >= 0 && prior < oldSlots.Length && oldSlots[prior] == chosen[n].slot && prior < oldMaterials.Length && prior < oldOriginals.Length)
                { originals[n] = oldOriginals[prior]; displays[n] = oldMaterials[prior]; }
                else
                {
                    originals[n] = chosen[n].renderer.sharedMaterials[chosen[n].slot];
                    displays[n] = new Material(Shader.Find("Lyralume/Fume Screen Display")); displays[n].SetTexture("_FoliaTex", view.output);
                    AssetDatabase.CreateAsset(displays[n], AssetDatabase.GenerateUniqueAssetPath(dir + "/Screen.mat"));
                }
            }
            view.screens = output.screens; view.originalMaterials = output.originalMaterials = originals;
            view.screenMaterials = output.displayMaterials = displays;
        }
        if (created || target.profile == null)
        {
            var profile = CreateInstance<LyralumeProfile>(); LyralumeApiWindow.CopyEndpoints(profile);
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath("Assets/LyralumeGenerated/PlayerSettings.asset")); target.profile = profile;
        }
        target.useProjectEndpoints = true;
        LyralumeApiWindow.ApplyProjectEndpoints(target);
        view.backgroundAudioLink = ConfiguredAudioLink(core);
        LyralumeAudioAnalysisSetup.Ensure(view, target.playback);
        AutoLighting(core, view, configuredLtcgi);
        target.installationComplete = true;
        foreach (var behaviour in target.GetComponentsInChildren<UdonSharpBehaviour>(true)) UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
        LyralumeSetupWindow.RecordInstance(target); EditorUtility.SetDirty(target.profile);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene); AssetDatabase.SaveAssets();
        return target;
    }
    // Core.AudioLink intentionally returns null while idle or owned by another source.
    // Installation needs the serialized configuration, not that runtime ownership gate.
    public static AudioLink.AudioLink ConfiguredAudioLink(Core core)
    {
        return core == null ? null : new SerializedObject(core).FindProperty("audioLink")?.objectReferenceValue as AudioLink.AudioLink;
    }
    public static int DisplayLayer() { return LyralumeRenderIsolation.AcquireLayer(); }
    static HashSet<CustomRenderTexture> FindLtcgi(Core core)
    {
        var data = new SerializedObject(core); var targets = data.FindProperty("screenTargets");
        var crts = new HashSet<CustomRenderTexture>();
        for (int n = 0; targets != null && n < targets.arraySize; n++)
        {
            var crt = targets.GetArrayElementAtIndex(n).objectReferenceValue as CustomRenderTexture;
            if (crt != null && crt.material != null && crt.material.shader != null && crt.material.shader.name.IndexOf("LTCGI", StringComparison.OrdinalIgnoreCase) >= 0) crts.Add(crt);
        }
        foreach (var config in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(c => c != null && c.gameObject.scene == core.gameObject.scene && c.GetType().FullName == "JLChnToZ.VRC.VVMW.Designer.LTCGIConfigurator"))
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            if (config.GetType().GetField("core", flags)?.GetValue(config) as Core != core) continue;
            var controller = config.GetType().GetField("controller", flags)?.GetValue(config) as Component;
            if (controller == null) continue;
            var crt = controller.GetType().GetField("VideoTexture", flags)?.GetValue(controller) as CustomRenderTexture;
            if (crt != null && crt.material != null) crts.Add(crt);
        }
        // VizVid preprocess replaces its configurator with the CRT's material binding.
        foreach (var controller in FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null && c.GetType().FullName == "pi.LTCGI.LTCGI_Controller"))
        {
            var crt = new SerializedObject(controller).FindProperty("VideoTexture")?.objectReferenceValue as CustomRenderTexture;
            if (crt == null || crt.material == null) continue;
            for (int n = 0; targets != null && n < targets.arraySize; n++)
                if (targets.GetArrayElementAtIndex(n).objectReferenceValue == crt.material) crts.Add(crt);
        }
        return crts;
    }
    static void AutoLighting(Core core, LyralumeFumeDisplay view, HashSet<CustomRenderTexture> crts)
    {
        // Capture before asset generation: VizVid may consume its editor-only configurator.
        crts.UnionWith(FindLtcgi(core));
        // Multiple CRTs need an explicit choice; never guess another player's lighting target.
        if (crts.Count == 1 && view.GetComponentsInChildren<UdonSharpBehaviour>(true).All(c => c.GetType().Name != "LyralumeLtcgiOutput"))
            LyralumeLightingSetup.InstallLtcgi(view, crts.First());
        var adapters = FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null && c.GetType().FullName == "JLChnToZ.VRC.VVMW.LightVolumeAdaptor" &&
            new SerializedObject(c).FindProperty("core")?.objectReferenceValue == core).ToArray();
        if (adapters.Length == 1 && view.GetComponentsInChildren<UdonSharpBehaviour>(true).All(c => c.GetType().Name != "LyralumeLightVolumeOutput"))
        {
            var so = new SerializedObject(adapters[0]);
            var lights = new List<Component>();
            foreach (string field in new[] { "pointLightVolumes", "cookiePointLightVolumes" })
            {
                var property = so.FindProperty(field);
                for (int n = 0; property != null && n < property.arraySize; n++) { var light = property.GetArrayElementAtIndex(n).objectReferenceValue as Component; if (light != null && !lights.Contains(light)) lights.Add(light); }
            }
            // VizVid 3 stores screen-cookie sources on the editor configurator;
            // the adaptor's runtime cookie list may still be empty before build.
            foreach (var configurator in FindObjectsOfType<MonoBehaviour>(true).Where(c => c != null && c.GetType().FullName == "JLChnToZ.VRC.VVMW.Designer.ScreenConfigurator"))
            {
                var data = new SerializedObject(configurator);
                if (data.FindProperty("core")?.objectReferenceValue != core) continue;
                var renderer = configurator.GetType().GetProperty("Renderer")?.GetValue(configurator) as Renderer;
                if (view.screens == null || !view.screens.Contains(renderer)) continue;
                var light = data.FindProperty("pointLightVolume")?.objectReferenceValue as Component;
                if (light != null && !lights.Contains(light)) lights.Add(light);
            }
            if (lights.Count > 0) LyralumeLightingSetup.InstallVrclv(view, UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)adapters[0]), lights.ToArray());
        }
    }
}

[CustomEditor(typeof(LyralumeInstance))]
public class LyralumeInstanceInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var i = (LyralumeInstance)target;
        EditorGUILayout.LabelField("Lyralume", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("播放器", i.vizVid == null ? "尚未選擇" : i.vizVid.name);
        EditorGUILayout.LabelField("螢幕", i.screenOutput == null ? "0" : i.screenOutput.screens.Length.ToString());
        if (GUILayout.Button(i.installationComplete ? "變更播放器／螢幕設定" : "完成安裝")) LyralumeQuickSetup.Edit(i);
        var p = i.profile as LyralumeProfile;
        if (i.provider != null && i.provider.EndpointCount() == 0)
            EditorGUILayout.HelpBox("尚未設定 API，自動歌詞目前不會發送請求。請使用「API 網址與優先順序」填入自己的相容服務。", MessageType.Info);
        if (p != null)
        {
            var so = new SerializedObject(p); so.Update();
            EditorGUI.BeginChangeCheck();
            Draw(so, "automaticDownload", "自動歌詞"); Draw(so, "prefetchNextSong", "預讀下一首");
            Draw(so, "wordHighlighting", "逐字高亮"); Draw(so, "translation", "翻譯"); Draw(so, "romanization", "羅馬音"); Draw(so, "cover", "封面");
            if (EditorGUI.EndChangeCheck()) { so.ApplyModifiedProperties(); LyralumeSetupWindow.Apply(i, p); }
        }
        if (GUILayout.Button("API 網址與優先順序")) LyralumeApiWindow.Open();
        if (GUILayout.Button("安裝獨立 Debug")) EditorApplication.ExecuteMenuItem("Tools/Lyralume/安裝 Debug 監看器");
    }
    static void Draw(SerializedObject so, string field, string label) { EditorGUILayout.PropertyField(so.FindProperty(field), new GUIContent(label)); }
}
