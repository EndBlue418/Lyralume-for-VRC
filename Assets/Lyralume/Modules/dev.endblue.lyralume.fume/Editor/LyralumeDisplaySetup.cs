using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using UdonSharpEditor;

public class LyralumeDisplaySetup : EditorWindow
{
    LyralumeInstance target;
    TMP_FontAsset font;
    int layer = 31;
    [MenuItem("Tools/Lyralume/進階/配置 Lyralume 畫面")]
    public static void Open() { GetWindow<LyralumeDisplaySetup>("Lyralume 畫面"); }
    void OnGUI()
    {
        target=(LyralumeInstance)EditorGUILayout.ObjectField("目標 Lyralume",target,typeof(LyralumeInstance),true);
        font=(TMP_FontAsset)EditorGUILayout.ObjectField("字型（使用者提供）",font,typeof(TMP_FontAsset),false);
        layer=EditorGUILayout.LayerField("舞台專用圖層",layer);
        EditorGUILayout.HelpBox("先在目標 ScreenOutput 設定螢幕與材質槽。每套播放器建立自己的相機、RenderTexture、材質與字形資料；不修改原始字型。字型至少要包含 ? 字元。",MessageType.Info);
        if(GUILayout.Button("建立 Lyralume 畫面"))
        {
            try { Install(target,font,layer); }
            catch(Exception e){Debug.LogException(e);ShowNotification(new GUIContent(e.Message));}
        }
    }
    public static LyralumeFumeDisplay Install(LyralumeInstance target,TMP_FontAsset font,int layer)
    {
        if(target==null || target.screenOutput==null || target.lyrics==null || target.playback==null)throw new InvalidOperationException("請指定完整 Lyralume 實例。");
        if(font==null || font.atlasTextures.Length==0)throw new InvalidOperationException("請指定已產生字形的 TMP 字型。");
        if(target.lyrics.visualDisplay!=null)throw new InvalidOperationException("此實例已安裝畫面；請調整既有畫面，避免重複安裝。");
        if(layer<0 || layer>31)throw new InvalidOperationException("圖層無效。");
        LyralumeSetupWindow.ValidateScreenBindings(target);
        var binding=target.screenOutput;
        if(binding.screens.Length==0)throw new InvalidOperationException("請先指定至少一個螢幕與材質槽。");
        var chars=font.characterTable.Where(c=>c.unicode<=65535 && c.glyph.atlasIndex==0).OrderBy(c=>c.unicode).ToArray();
        if(!chars.Any(c=>c.unicode==63))throw new InvalidOperationException("字型需要 ? 後備字元。");
        string folder="Assets/LyralumeGenerated";
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","LyralumeGenerated");
        folder=AssetDatabase.GenerateUniqueAssetPath(folder+"/Display"); Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var root=new GameObject("Lyralume Display"); Undo.RegisterCreatedObjectUndo(root,"Create Lyralume Display");
        root.transform.SetParent(target.transform,false); root.transform.position=LyralumeRenderIsolation.AcquirePosition(-1000f); root.layer=layer;
        var view=root.AddUdonSharpComponent<LyralumeFumeDisplay>();
        view.core=target.playback; view.lyrics=target.lyrics; view.articleRoot=root.transform; view.screenOutput=binding; view.displayMode=2;
        view.glyphCodes=new int[chars.Length];view.glyphUVs=new Vector4[chars.Length];view.glyphMetrics=new Vector4[chars.Length];view.glyphAdvances=new float[chars.Length];
        float em=font.faceInfo.pointSize,pad=Mathf.Max(font.atlasPadding-1,0);
        for(int n=0;n<chars.Length;n++)
        {
            var c=chars[n];var r=c.glyph.glyphRect;var m=c.glyph.metrics;
            view.glyphCodes[n]=(int)c.unicode;
            view.glyphUVs[n]=new Vector4((r.x-pad)/font.atlasWidth,(r.y-pad)/font.atlasHeight,(r.x+r.width+pad)/font.atlasWidth,(r.y+r.height+pad)/font.atlasHeight);
            view.glyphMetrics[n]=new Vector4((m.horizontalBearingX-pad)/em,(m.horizontalBearingY-m.height-pad)/em,(m.width+pad*2)/em,(m.height+pad*2)/em);
            view.glyphAdvances[n]=m.horizontalAdvance/em;
        }
        view.output=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32){name="Lyralume Output"};
        AssetDatabase.CreateAsset(view.output,folder+"/Output.renderTexture");
        var cam=Child(root.transform,"Lyralume Camera",layer).AddComponent<Camera>();
        cam.transform.localPosition=new Vector3(0,0,-10);cam.orthographic=true;cam.orthographicSize=4.7f;cam.aspect=16f/9;
        cam.cullingMask=1<<layer;cam.targetTexture=view.output;cam.stereoTargetEye=StereoTargetEyeMask.None;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.nearClipPlane=.1f;cam.farClipPlane=50;cam.allowHDR=false;cam.allowMSAA=false;cam.enabled=false;
        view.stageCamera=cam;
        view.glyphMaterial=MakeMaterial("Lyralume/Fume Glyph Display",folder+"/Glyph.mat");view.glyphMaterial.mainTexture=font.atlasTextures[0];
        view.secondaryMaterial=MakeMaterial("Lyralume/Fume Glyph Display",folder+"/Secondary.mat");view.secondaryMaterial.mainTexture=font.atlasTextures[0];
        view.lineMeshes=new MeshFilter[96];
        for(int n=0;n<view.lineMeshes.Length;n++)
        {
            var line=Child(root.transform,"Line "+n,layer);view.lineMeshes[n]=line.AddComponent<MeshFilter>();
            var renderer=line.AddComponent<MeshRenderer>();renderer.sharedMaterial=view.glyphMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;line.SetActive(false);
        }
        var paper=GameObject.CreatePrimitive(PrimitiveType.Quad);paper.name="Lyralume Background";DestroyImmediate(paper.GetComponent<Collider>());paper.layer=layer;paper.transform.SetParent(cam.transform,false);
        paper.transform.localPosition=new Vector3(0,0,15);paper.transform.localScale=new Vector3(24,14,1);
        view.backgroundMaterial=MakeMaterial("Lyralume/Fume Paper Display",folder+"/Background.mat");paper.GetComponent<Renderer>().sharedMaterial=view.backgroundMaterial;
        view.statusMesh=MeshLabel(root.transform,"Lyralume Status Glyphs",view.glyphMaterial,layer);view.statusMesh.gameObject.SetActive(false);
        view.statusText=Label(cam.transform,"Lyralume Status",font,new Vector3(0,-3.5f,9),2.4f,layer);
        view.subtitleText=Label(cam.transform,"Lyralume Subtitle",font,new Vector3(0,-4f,9),2.2f,layer);
        view.statusText.text="";view.statusText.enabled=false;view.subtitleText.text="";view.subtitleText.enabled=false;
        view.translationMesh=MeshLabel(cam.transform,"Translation",view.secondaryMaterial,layer);
        view.romanizationMesh=MeshLabel(cam.transform,"Romanization",view.secondaryMaterial,layer);
        view.screens=binding.screens;view.originalMaterials=new Material[binding.screens.Length];view.screenMaterials=new Material[binding.screens.Length];
        for(int n=0;n<binding.screens.Length;n++)
        {
            view.originalMaterials[n]=binding.screens[n].sharedMaterials[binding.materialSlots[n]];
            view.screenMaterials[n]=MakeMaterial("Lyralume/Fume Screen Display",folder+"/Screen-"+n+".mat");view.screenMaterials[n].SetTexture("_FoliaTex",view.output);
        }
        binding.originalMaterials=view.originalMaterials;binding.displayMaterials=view.screenMaterials;binding.texture=view.output;binding.sourceCamera=null;binding.externallyDriven=true;binding.outputEnabled=true;
        target.lyrics.visualDisplay=view;
        UdonSharpEditorUtility.CopyProxyToUdon(view);UdonSharpEditorUtility.CopyProxyToUdon(binding);UdonSharpEditorUtility.CopyProxyToUdon(target.lyrics);
        LyralumeSetupWindow.RecordInstance(target);
        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);AssetDatabase.SaveAssets();
        return view;
    }
    static Material MakeMaterial(string shader,string path)
    {
        var s=Shader.Find(shader);if(s==null)throw new InvalidOperationException("找不到 Shader："+shader);
        var preset = shader == "Lyralume/Fume Glyph Display" ? (path.EndsWith("Secondary.mat") ? "ReferenceSecondary" : "ReferenceGlyphs") : shader == "Lyralume/Fume Paper Display" ? "ReferenceBackground" : null;
        var template = preset == null ? null : AssetDatabase.FindAssets(preset + " t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name == preset);
        var m=template == null ? new Material(s) : new Material(template);m.shader=s;AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject Child(Transform parent,string name,int layer){var o=new GameObject(name);o.transform.SetParent(parent,false);o.layer=layer;return o;}
    static TMP_Text Label(Transform parent,string name,TMP_FontAsset font,Vector3 pos,float size,int layer)
    {
        var label=Child(parent,name,layer).AddComponent<TextMeshPro>();label.font=font;label.fontSize=size;label.transform.localPosition=pos;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(15,.6f);label.richText=false;return label;
    }
    static MeshFilter MeshLabel(Transform parent,string name,Material material,int layer)
    {
        var o=Child(parent,name,layer);var filter=o.AddComponent<MeshFilter>();o.AddComponent<MeshRenderer>().sharedMaterial=material;return filter;
    }
}
