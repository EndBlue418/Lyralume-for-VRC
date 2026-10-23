using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UdonSharp;
using UdonSharpEditor;
using TMPro;

public static class LyralumeOverviewSetup
{
    static Material Make(string shader, string path, Material source = null)
    {
        var material = source == null ? new Material(Shader.Find(shader)) : new Material(source);
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static GameObject Child(Transform parent, string name, int layer)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = layer; return go; }
    static MeshFilter Mesh(Transform parent, string name, int layer, Material material)
    { var go = Child(parent, name, layer); var filter = go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>().sharedMaterial = material; go.SetActive(false); return filter; }
    public static LyralumeOverview Install(LyralumeDebugMonitor monitor, LyralumeFumeDisplay source)
    {
        var scriptPath = AssetDatabase.FindAssets("LyralumeOverview t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileName(p) == "LyralumeOverview.cs");
        string programPath = Path.ChangeExtension(scriptPath, "asset");
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath);
        if (program == null)
        {
            program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath); program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion; AssetDatabase.CreateAsset(program, programPath);
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions { IsEditorBuild = false });
        }
        // This newly authored source already uses current UdonSharp serialization attributes.
        program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
        string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/LyralumeGenerated/DebugOverview"); Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var overview = monitor.gameObject.AddUdonSharpComponent<LyralumeOverview>(); overview.source = source;
        int layer = LyralumeRenderIsolation.AcquireLayer(); overview.mapLayer = layer;
        var stage = Child(monitor.transform, "Overview Stage", layer); stage.transform.position = LyralumeRenderIsolation.AcquirePosition(-12000f);
        overview.overviewStage = stage.transform;
        var camera = Child(stage.transform, "Overview Camera", layer).AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << layer;
        camera.nearClipPlane = .01f; camera.farClipPlane = 50; camera.useOcclusionCulling = false; camera.allowHDR = false; camera.allowMSAA = false;
        overview.output = new RenderTexture(1024, 512, 16); AssetDatabase.CreateAsset(overview.output, folder + "/Overview.renderTexture");
        camera.targetTexture = overview.output; camera.aspect = 2; overview.overviewCamera = camera;
        overview.glyphMaterial = Make("Lyralume/Overview Glyph", folder + "/MapGlyph.mat"); overview.glyphMaterial.SetFloat("_StageY", stage.transform.position.y);
        overview.screenMaterial = Make("Lyralume/Overview Screen", folder + "/MapScreen.mat"); overview.screenMaterial.SetTexture("_MainTex", overview.output);
        overview.pages = new MeshFilter[16];
        for (int n = 0; n < 16; n++) overview.pages[n] = Mesh(stage.transform, "Map Page " + n, layer, overview.glyphMaterial);
        int nativeLayer = layer; overview.nativeLayer = nativeLayer;
        var native = Child(monitor.transform, "Real Lyric Stage", nativeLayer); native.transform.SetPositionAndRotation(source.articleRoot.position, source.articleRoot.rotation); native.transform.localScale = source.articleRoot.lossyScale;
        overview.nativeStage = native.transform;
        overview.nativeGlyphMaterial = Make(null, folder + "/RealGlyph.mat", source.glyphMaterial);
        overview.nativeBackgroundMaterial = Make(null, folder + "/RealBackground.mat", source.backgroundMaterial);
        overview.nativeSecondaryMaterial = Make(null, folder + "/RealSecondary.mat", source.secondaryMaterial);
        overview.nativePages = new MeshFilter[16];
        for (int n = 0; n < 16; n++) overview.nativePages[n] = Mesh(native.transform, "Animated Page " + n, nativeLayer, overview.nativeGlyphMaterial);
        var background = GameObject.CreatePrimitive(PrimitiveType.Quad); background.name = "Real Background"; background.transform.SetParent(native.transform, false); background.layer = nativeLayer;
        UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>()); background.GetComponent<Renderer>().sharedMaterial = overview.nativeBackgroundMaterial; overview.nativeBackground = background.transform;
        overview.nativeAuxiliary = new MeshFilter[3];
        for (int n = 0; n < 3; n++) overview.nativeAuxiliary[n] = Mesh(native.transform, n == 0 ? "Real Status" : n == 1 ? "Real Translation" : "Real Romanization", nativeLayer, n == 0 ? overview.nativeGlyphMaterial : overview.nativeSecondaryMaterial);
        native.SetActive(false);
        overview.FrameMap(); overview.CloseOverview(); UdonSharpEditorUtility.CopyProxyToUdon(overview); return overview;
    }
}
