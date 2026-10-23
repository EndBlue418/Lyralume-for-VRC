using UdonSharp;
using UnityEngine;

// The existing LTCGI input CRT keeps its identity. Its original material owns video conversion.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeLtcgiOutput : UdonSharpBehaviour
{
    public LyralumeFumeDisplay view;
    public bool outputEnabled = true;
    public CustomRenderTexture input;
    public Material lyricMaterial;
    [HideInInspector] public bool ownsOutput;
    private Material originalMaterial;
    private void LateUpdate() { RefreshOutput(); }
    public void RefreshOutput()
    {
        bool wanted=outputEnabled && isActiveAndEnabled && view!=null && view.isActiveAndEnabled && view.isActive && input!=null && lyricMaterial!=null;
        if(!wanted){RestoreOutput();return;}
        if(!ownsOutput){originalMaterial=input.material;input.material=lyricMaterial;ownsOutput=true;}
        if(input.material!=lyricMaterial){ownsOutput=false;return;}
        lyricMaterial.SetTexture("_MainTex",view.output);
        if(view.screenMaterials!=null && view.screenMaterials.Length>0 && view.screenMaterials[0]!=null)
        {
            var screen=view.screenMaterials[0];lyricMaterial.SetColor("_Color",screen.GetColor("_Color"));
            lyricMaterial.SetFloat("_EmissionIntensity",screen.GetFloat("_EmissionIntensity"));
            lyricMaterial.SetFloat("_ScreenOpacity",screen.GetFloat("_ScreenOpacity"));
        }
        input.Update();
    }
    public void RestoreOutput()
    {
        if(ownsOutput && input!=null && input.material==lyricMaterial)input.material=originalMaterial;
        ownsOutput=false;
    }
    private void OnDisable(){RestoreOutput();}
    private void OnDestroy(){RestoreOutput();}
}
