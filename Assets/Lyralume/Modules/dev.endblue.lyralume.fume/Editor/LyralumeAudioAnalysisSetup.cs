using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using UdonSharp;
using UdonSharpEditor;

public static class LyralumeAudioAnalysisSetup
{
    public static void EnsureProgram()
    {
        string script=AssetDatabase.FindAssets("LyralumeAudioAnalysis t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).First(p=>Path.GetFileName(p)=="LyralumeAudioAnalysis.cs");
        string programPath=Path.ChangeExtension(script,"asset");
        if(AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath)!=null)return;
        var program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=AssetDatabase.LoadAssetAtPath<MonoScript>(script);program.ScriptVersion=UdonSharpProgramVersion.CurrentVersion;AssetDatabase.CreateAsset(program,programPath);
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync(new UdonSharp.Compiler.UdonSharpCompileOptions{IsEditorBuild=false});
    }
    public static LyralumeAudioAnalysis Ensure(LyralumeFumeDisplay view,LyralumePlaybackState playback)
    {
        if(view==null || playback==null)throw new InvalidOperationException("缺少歌詞畫面或播放器狀態。");
        if(view.localAudio!=null) {
            if(view.localAudio.playback!=playback || view.localAudio.view!=view)throw new InvalidOperationException("音樂分析接點屬於另一套 Lyralume。");
            return view.localAudio;
        }
        var template=AssetDatabase.LoadAssetAtPath<CustomRenderTexture>("Packages/com.llealloo.audiolink/Runtime/RenderTextures/rt_AudioLink.asset");
        if(template==null || template.material==null)throw new InvalidOperationException("缺少 AudioLink 分析貼圖，請先安裝 AudioLink。");
        EnsureProgram();
        string folder=AssetDatabase.GenerateUniqueAssetPath("Assets/LyralumeGenerated/AudioAnalysis");Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var material=new Material(template.material);material.name="Lyralume Audio Analysis";AssetDatabase.CreateAsset(material,folder+"/Analysis.mat");
        var texture=UnityEngine.Object.Instantiate(template);texture.name="Lyralume Private Audio Data";texture.material=material;texture.updateMode=CustomRenderTextureUpdateMode.OnDemand;texture.initializationMode=CustomRenderTextureUpdateMode.OnDemand;AssetDatabase.CreateAsset(texture,folder+"/Analysis.renderTexture");
        var go=new GameObject("Lyralume Audio Analysis");go.transform.SetParent(view.transform,false);
        var analyzer=go.AddUdonSharpComponent<LyralumeAudioAnalysis>();analyzer.playback=playback;analyzer.view=view;analyzer.output=texture;analyzer.analysisMaterial=material;view.localAudio=analyzer;
        UdonSharpEditorUtility.CopyProxyToUdon(analyzer);UdonSharpEditorUtility.CopyProxyToUdon(view);
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);return analyzer;
    }
}

// Existing installations get their private analysis in the build copy, without
// forcing users to replace their scene or changing the world's shared AudioLink.
public class LyralumeAudioAnalysisBuild : IProcessSceneWithReport
{
    public int callbackOrder=>-10010;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        foreach(var root in scene.GetRootGameObjects())foreach(var instance in root.GetComponentsInChildren<LyralumeInstance>(true))
            if(instance.lyrics!=null && instance.lyrics.visualDisplay is LyralumeFumeDisplay)
                LyralumeAudioAnalysisSetup.Ensure((LyralumeFumeDisplay)instance.lyrics.visualDisplay,instance.playback);
    }
}
