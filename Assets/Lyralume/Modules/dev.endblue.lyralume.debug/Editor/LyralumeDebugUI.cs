using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using TMPro;
using UdonSharp;
using UdonSharpEditor;

// Reflection is editor-only: ordinary Debug has no compile-time dependency on LuraSwitch.
public static class LyralumeDebugUI
{
    static Type FindType(string name) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null); }
    static string Prefab(string name) { return AssetDatabase.FindAssets(name + " t:Prefab").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name && p.Contains("LuraSwitch")); }
    public static bool HasLuraSwitch { get { return FindType("QuickBrown.LuraSwitch.SwitchBase") != null && FindType("QuickBrown.LuraSwitch.SliderSwitch") != null && Prefab("Switch_Base") != null && Prefab("Slider_Base") != null; } }
    public static void ValidateLuraSwitch()
    {
        if (!HasLuraSwitch) throw new InvalidOperationException("分體版需要先安裝 LuraSwitch 2（Switch_Base / Slider_Base）。");
        RequireFields(FindType("QuickBrown.LuraSwitch.SwitchBase"), "mode", "externalScript", "externalEventName", "switchText", "syncMode", "switchVisualMode");
        RequireFields(FindType("QuickBrown.LuraSwitch.SliderSwitch"), "controller", "sliderDefaultValue", "sliderStep", "sliderText", "syncMode", "sliderVisualMode", "orientationMode");
    }
    static void RequireFields(Type type, params string[] fields)
    {
        foreach (string field in fields) if (type.GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic) == null)
            throw new InvalidOperationException("LuraSwitch 版本不相容：缺少 " + type.Name + "." + field);
    }
    static void Layer(Transform root) { root.gameObject.layer = 5; foreach (Transform c in root) Layer(c); }
    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.gameObject.layer = 5; return rect;
    }
    static Canvas Panel(Transform parent, string name, Vector2 size, Vector3 position)
    {
        var rect = Rect(parent, name, Vector2.one * .5f, Vector2.one * .5f); rect.sizeDelta = size; rect.localScale = Vector3.one * .0015f; rect.localPosition = position;
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        rect.gameObject.AddComponent<CanvasScaler>(); var raycaster = rect.gameObject.AddComponent<GraphicRaycaster>(); raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        rect.gameObject.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        // Decorative backing is a UI sibling, never a coplanar mesh/collider over the controls.
        var bg = Rect(rect, "Background", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>(); bg.color = new Color(.025f, .025f, .04f, .98f); bg.raycastTarget = false;
        return canvas;
    }
    static TMP_Text Text(Transform parent, string name, string content, TMP_FontAsset font, Vector2 min, Vector2 max, float size = 22)
    {
        var rect = Rect(parent, name, min, max); rect.offsetMin = new Vector2(20, 8); rect.offsetMax = new Vector2(-20, -8);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = size; text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft; text.richText = false; text.raycastTarget = false; text.text = content; return text;
    }
    static void Map(Canvas canvas, LyralumeOverview overview, Vector2 min, Vector2 max)
    {
        var area = Rect(canvas.transform, "Preview Area", min, max); area.offsetMin = new Vector2(20, 0); area.offsetMax = new Vector2(-20, 0);
        var rect = Rect(area, "Lyric Overview", Vector2.zero, Vector2.one);
        var raw = rect.gameObject.AddComponent<RawImage>(); raw.texture = overview.output; raw.material = overview.screenMaterial; raw.raycastTarget = false;
        rect.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        rect.GetComponent<AspectRatioFitter>().aspectRatio = 2;
    }
    static void Button(Canvas canvas, string label, Vector2 min, Vector2 max, TMP_FontAsset font, LyralumeOverview overview, string method)
    {
        var rect = Rect(canvas.transform, label, min, max); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.2f, .12f, .3f, 1);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text(rect, "Label", label, font, Vector2.zero, Vector2.one, 20).alignment = TextAlignmentOptions.Center;
        UnityEventTools.AddStringPersistentListener(button.onClick, UdonSharpEditorUtility.GetBackingUdonBehaviour(overview).SendCustomEvent, method);
    }
    public static void Build(LyralumeDebugMonitor monitor, LyralumeOverview overview, bool split)
    {
        if (overview == null) throw new InvalidOperationException("此 Debug 沒有預覽來源，請重新安裝。");
        if (split) ValidateLuraSwitch();
        Undo.RegisterFullObjectHierarchyUndo(monitor.gameObject, "Repair Lyralume Debug UI");
        // Rebuild only generated UI; stage cameras, materials, RT and target stay intact.
        string containerName = split ? "Split Panels" : "Debug Panel";
        var old = monitor.transform.Find(containerName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var oldToggle = monitor.transform.Find("Debug Toggle");
        if (oldToggle != null) Undo.DestroyObjectImmediate(oldToggle.gameObject);
        TMP_FontAsset font = AssetDatabase.FindAssets("LyralumeReferenceFont t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).First(f => f != null);
        overview.focusZoomSlider = null; overview.nativeModeState = null;
        if (!split)
        {
            var panel = Panel(monitor.transform, containerName, new Vector2(960, 1060), Vector3.zero);
            monitor.display = Text(panel.transform, "Runtime", "Lyralume Runtime", font, new Vector2(0, .61f), Vector2.one);
            Map(panel, overview, new Vector2(0, .15f), new Vector2(1, .60f));
            overview.status = Text(panel.transform, "Overview Status", "LAYOUT / REAL STAGE", font, new Vector2(0, .105f), new Vector2(1, .15f), 17);
            Button(panel, "總覽開關", new Vector2(.025f, .025f), new Vector2(.25f, .09f), font, overview, "ToggleOverview");
            Button(panel, "Layout / Real", new Vector2(.27f, .025f), new Vector2(.59f, .09f), font, overview, "ToggleNativeMode");
            var rect = Rect(panel.transform, "Zoom", new Vector2(.66f, .035f), new Vector2(.96f, .08f));
            var track = rect.gameObject.AddComponent<Image>(); track.color = new Color(.25f, .2f, .35f, 1);
            var slider = rect.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1; slider.value = overview.focusDistance;
            var area = Rect(rect, "Handle Area", Vector2.zero, Vector2.one); area.offsetMin = new Vector2(10, 0); area.offsetMax = new Vector2(-10, 0);
            var handle = Rect(area, "Handle", Vector2.zero, new Vector2(0, 1)); handle.sizeDelta = new Vector2(20, 0);
            var graphic = handle.gameObject.AddComponent<Image>(); graphic.color = new Color(.75f, .5f, 1, 1); slider.handleRect = handle; slider.targetGraphic = graphic;
            overview.focusZoomSlider = slider;
        }
        else
        {
            var container = new GameObject(containerName); container.transform.SetParent(monitor.transform, false);
            var screen = Panel(container.transform, "Screen · 左側預覽", new Vector2(1100, 650), new Vector3(-.65f, .08f, 0));
            Text(screen.transform, "Title", "LYRALUME · OVERVIEW", font, new Vector2(0, .9f), Vector2.one, 25);
            Map(screen, overview, new Vector2(0, .1f), new Vector2(1, .9f));
            overview.status = Text(screen.transform, "Overview Status", "LAYOUT / REAL STAGE", font, Vector2.zero, new Vector2(1, .1f), 18);
            var runtime = Panel(container.transform, "Runtime · 右側狀態", new Vector2(680, 650), new Vector3(.73f, .08f, 0));
            Text(runtime.transform, "Title", "LYRALUME · RUNTIME", font, new Vector2(0, .9f), Vector2.one, 25);
            monitor.display = Text(runtime.transform, "Runtime", "Lyralume Runtime", font, Vector2.zero, new Vector2(1, .9f), 20);
            LyralumeDebugLayout.Apply("Screen", screen.transform);
            LyralumeDebugLayout.Apply("Runtime", runtime.transform);
            // Controls are children of Runtime, so moving Runtime moves the entire console.
            LuraControl(runtime.transform, "Switch_Base", "Layout", Vector3.zero, overview, "ToggleOverview", font);
            LuraControl(runtime.transform, "Switch_Base", "Real Stage", Vector3.zero, overview, "ToggleNativeMode", font);
            LuraControl(runtime.transform, "Slider_Base", "Zoom", Vector3.zero, overview, null, font);
            monitor.display.rectTransform.anchorMin = new Vector2(0, .56f);
            monitor.display.rectTransform.anchorMax = new Vector2(1, .9f);
            Text(runtime.transform, "Credits", "Created by EndBlue · Assisted by Codex", font, Vector2.zero, new Vector2(1, .065f), 15).alignment = TextAlignmentOptions.Center;
        }
        if (!split) Text(monitor.display.GetComponentInParent<Canvas>().transform, "Credits", "Lyralume · Created by EndBlue", font, new Vector2(0, .09f), new Vector2(1, .12f), 14).alignment = TextAlignmentOptions.Center;
        monitor.panelRoot = monitor.transform.Find(containerName).gameObject; monitor.overview = overview;
        BuildToggle(monitor, overview, split, font);
        monitor.Refresh(); overview.UpdateMarker(); UdonSharpEditorUtility.CopyProxyToUdon(monitor); UdonSharpEditorUtility.CopyProxyToUdon(overview);
        PrefabUtility.RecordPrefabInstancePropertyModifications(monitor); PrefabUtility.RecordPrefabInstancePropertyModifications(overview);
    }
    public static void UpgradeToggle(LyralumeDebugMonitor monitor)
    {
        var old = monitor.transform.Find("Debug Toggle");
        if (old == null || old.GetComponent<Canvas>() == null) return;
        ValidateLuraSwitch();
        Undo.DestroyObjectImmediate(old.gameObject);
        BuildToggle(monitor, monitor.overview, true, monitor.display.font);
        Undo.RegisterCreatedObjectUndo(monitor.transform.Find("Debug Toggle").gameObject, "Update Debug switch");
    }
    static void BuildToggle(LyralumeDebugMonitor monitor, LyralumeOverview overview, bool split, TMP_FontAsset font)
    {
        if (split)
        {
            // Outside panelRoot: closing Debug must never hide its reopening control.
            var root = LuraControl(monitor.transform, "Switch_Base", "Debug", Vector3.zero, overview, "ToggleDebug", font, monitor);
            root.name = "Debug Toggle";
            var runtime = monitor.display.GetComponentInParent<Canvas>(true).transform;
            var rect = (RectTransform)runtime;
            root.transform.localPosition = runtime.localPosition + new Vector3(rect.rect.width * runtime.localScale.x * .5f + .22f, 0, -.03f);
            root.transform.localScale = Vector3.Scale(LyralumeDebugLayout.Get("ToggleOverview").scale, runtime.localScale);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        else
        {
        var toggleCanvas = Panel(monitor.transform, "Debug Toggle", new Vector2(300, 72), new Vector3(.73f, .68f, -.03f));
        var toggleRect = Rect(toggleCanvas.transform, "Debug On Off", new Vector2(.03f,.08f), new Vector2(.97f,.92f));
        var toggleImage = toggleRect.gameObject.AddComponent<Image>(); toggleImage.color = new Color(.2f,.12f,.3f,1);
        var toggle = toggleRect.gameObject.AddComponent<Button>(); toggle.targetGraphic = toggleImage;
        Text(toggleRect, "Label", "Debug", font, Vector2.zero, Vector2.one, 24).alignment = TextAlignmentOptions.Center;
        UnityEventTools.AddStringPersistentListener(toggle.onClick, UdonSharpEditorUtility.GetBackingUdonBehaviour(monitor).SendCustomEvent, "ToggleDebug");
        }
    }
    static GameObject LuraControl(Transform parent, string prefabName, string label, Vector3 position, LyralumeOverview overview, string method, TMP_FontAsset font, UdonSharpBehaviour recipient = null)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(prefabName)), parent);
        root.name = label; root.transform.localPosition = position;
        // LuraSwitch's 2D canvases face +Z; our screens face -Z.
        root.transform.localRotation = Quaternion.Euler(0, 180, 0); root.transform.localScale = Vector3.one * 2;
        bool slider = method == null;
        string styleKey = slider ? "Zoom" : method == "ToggleDebug" ? "ToggleOverview" : method;
        var style = LyralumeDebugLayout.Get(styleKey);
        var component = (UdonSharpBehaviour)root.GetComponentInChildren(FindType("QuickBrown.LuraSwitch." + (slider ? "SliderSwitch" : "SwitchBase")), true);
        // Keep authored prefab references. Older LuraSwitch backing programs may
        // not contain these serialized fields yet; reading them would erase wiring.
        UdonSharpEditorUtility.CopyProxyToUdon(component);
        var data = new SerializedObject(component); data.FindProperty("syncMode").intValue = 0;
        data.FindProperty(slider ? "sliderVisualMode" : "switchVisualMode").intValue = style.visualMode;
        data.FindProperty(slider ? "sliderText" : "switchText").stringValue = label;
        data.FindProperty("useText").intValue = style.useText;
        if (slider)
        {
            data.FindProperty("sliderDefaultValue").floatValue = overview.focusDistance; data.FindProperty("sliderStep").floatValue = 0;
            data.FindProperty("orientationMode").intValue = style.orientation;
            data.FindProperty("controller").objectReferenceValue = overview;
        }
        else
        {
            data.FindProperty("mode").intValue = 1;
            data.FindProperty("externalScript").objectReferenceValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(recipient == null ? overview : recipient);
            data.FindProperty("externalEventName").stringValue = method;
            if (method == "ToggleDebug") data.FindProperty("switch_InteractionText").stringValue = label;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        // Serialized Udon proxies do not consistently run their source OnValidate.
        // Ask LuraSwitch to apply its own mode, orientation and labels in the editor.
        component.GetType().GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(component, null);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
        LyralumeDebugLayout.Apply(styleKey, root.transform);
        // The copied Layout style contains its caption; preserve the new Debug caption.
        if (method == "ToggleDebug") foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) if (text.text == style.caption) text.text = label;
        UdonSharpEditorUtility.CopyProxyToUdon(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        // Retain genuine LuraSwitch geometry, pickup/collider and event handlers.
        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) { Layer(canvas.transform); if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>(); }
        foreach (var c in root.GetComponentsInChildren<Component>(true)) if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
        return root;
    }
}
