using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;

public class LyralumeLightingSetup : EditorWindow
{
    LyralumeFumeDisplay view;
    CustomRenderTexture ltcgiInput;
    UdonBehaviour videoAdaptor;
    Component[] emitters = new Component[0];
    [MenuItem("Tools/Lyralume/進階/配置 LTCGI 與 VRCLV")]
    public static void Open(){GetWindow<LyralumeLightingSetup>("Lyralume 照明");}
    void OnGUI()
    {
        view=(LyralumeFumeDisplay)EditorGUILayout.ObjectField("目標 Lyralume 畫面",view,typeof(LyralumeFumeDisplay),true);
        EditorGUILayout.HelpBox("每個接點只控制指定輸出。安裝前先安裝對應的 Lyralume LTCGI / VRCLV 套件；不會搜尋或修改其他播放器。",MessageType.Info);
        ltcgiInput=(CustomRenderTexture)EditorGUILayout.ObjectField("既有 LTCGI 輸入 CRT",ltcgiInput,typeof(CustomRenderTexture),false);
        if(GUILayout.Button("安裝 LTCGI 輸出"))Run(()=>InstallLtcgi(view,ltcgiInput));
        videoAdaptor=(UdonBehaviour)EditorGUILayout.ObjectField("原 VRCLV 影片接點",videoAdaptor,typeof(UdonBehaviour),true);
        int count=Mathf.Clamp(EditorGUILayout.IntField("VRCLV 發光源數量",emitters.Length),0,64);
        if(count!=emitters.Length)Array.Resize(ref emitters,count);
        for(int n=0;n<emitters.Length;n++)emitters[n]=(Component)EditorGUILayout.ObjectField("發光源 "+(n+1),emitters[n],typeof(Component),true);
        if(GUILayout.Button("安裝 VRCLV 輸出"))Run(()=>InstallVrclv(view,videoAdaptor,emitters));
    }
    void Run(Action a){try{a();AssetDatabase.SaveAssets();}catch(Exception e){Debug.LogException(e);ShowNotification(new GUIContent(e.Message));}}
    static Type Find(string name){return AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);}
    static Material Material(string shader)
    {
        string dir="Assets/LyralumeGenerated";if(!AssetDatabase.IsValidFolder(dir))AssetDatabase.CreateFolder("Assets","LyralumeGenerated");
        var s=Shader.Find(shader);if(s==null)throw new InvalidOperationException("缺少 Shader："+shader);
        var m=new Material(s);AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(dir+"/Lighting.mat"));return m;
    }
    static UdonSharpBehaviour Add(LyralumeFumeDisplay view,string className)
    {
        if(view==null)throw new InvalidOperationException("請指定 Lyralume 畫面。");
        var t=Find(className);if(t==null)throw new InvalidOperationException("請先安裝對應的 Lyralume 接點套件。");
        if(view.GetComponentInChildren(t,true)!=null)throw new InvalidOperationException("此畫面已安裝該接點。");
        EnsureProgram(t);
        var o=new GameObject(className);o.transform.SetParent(view.transform,false);Undo.RegisterCreatedObjectUndo(o,"Install Lyralume lighting");
        var component=o.AddUdonSharpComponent(t);t.GetField("view").SetValue(component,view);return component;
    }
    static void EnsureProgram(Type type)
    {
        string source = AssetDatabase.FindAssets(type.Name + " t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).First(p => System.IO.Path.GetFileNameWithoutExtension(p) == type.Name);
        string path = System.IO.Path.ChangeExtension(source, "asset");
        var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
        if (program != null) return;
        program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(source); program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
        AssetDatabase.CreateAsset(program, path);
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions { IsEditorBuild = false });
    }
    public static UdonSharpBehaviour InstallLtcgi(LyralumeFumeDisplay view,CustomRenderTexture input)
    {
        if(input==null)throw new InvalidOperationException("請指定 LTCGI 的既有輸入 CRT。");
        var type=Find("LyralumeLtcgiOutput");
        if(type!=null)foreach(var other in Resources.FindObjectsOfTypeAll(type))
            if(type.GetField("input").GetValue(other) as CustomRenderTexture==input)throw new InvalidOperationException("此 CRT 已由另一個 Lyralume 接點控制。");
        var c=Add(view,"LyralumeLtcgiOutput");var t=c.GetType();t.GetField("input").SetValue(c,input);t.GetField("lyricMaterial").SetValue(c,Material("Lyralume/LTCGI Output"));
        UdonSharpEditorUtility.CopyProxyToUdon(c);return c;
    }
    public static UdonSharpBehaviour InstallVrclv(LyralumeFumeDisplay view,UdonBehaviour adaptor,Component[] lights)
    {
        if(adaptor==null || lights==null || lights.Length==0)throw new InvalidOperationException("請指定原影片接點與發光源。");
        var type=Find("LyralumeLightVolumeOutput");if(type==null)throw new InvalidOperationException("請先安裝 Lyralume VRCLV 套件。");
        Type lightType=type.GetField("emitters").FieldType.GetElementType();
        var values=Array.CreateInstance(lightType,lights.Length);
        for(int n=0;n<lights.Length;n++)
        {
            if(lights[n]==null || !lightType.IsInstanceOfType(lights[n]))throw new InvalidOperationException("發光源型別不正確。");
            if ((int)lightType.GetField("LightType").GetValue(lights[n]) != 2) throw new InvalidOperationException("Lyralume 螢幕照明需要 Area Light 類型的 VRCLV 發光源。");
            if (view == null || lights[n].gameObject.scene != view.gameObject.scene || adaptor.gameObject.scene != view.gameObject.scene) throw new InvalidOperationException("請指定同一場景的畫面、影片接點與發光源。");
            if(lights.Take(n).Contains(lights[n]))throw new InvalidOperationException("發光源重複。");
            foreach(var other in Resources.FindObjectsOfTypeAll(type))
            {
                var used=type.GetField("emitters").GetValue(other) as Array;
                if(used!=null)foreach(var value in used)if(Equals(value,lights[n]))throw new InvalidOperationException("發光源已綁定另一個 Lyralume 接點。");
            }
            values.SetValue(lights[n],n);
        }
        var c=Add(view,"LyralumeLightVolumeOutput");type.GetField("videoAdaptor").SetValue(c,adaptor);type.GetField("emitters").SetValue(c,values);type.GetField("lightingMaterial").SetValue(c,Material("Lyralume/Light Volume Source"));
        UdonSharpEditorUtility.CopyProxyToUdon(c);return c;
    }
}
