using UdonSharp;
using UnityEngine;
using VRC.Udon;
using VRCLightVolumes;

// Each instance temporarily owns only its configured screen emitters.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeLightVolumeOutput : UdonSharpBehaviour
{
    public LyralumeFumeDisplay view;
    public bool outputEnabled = true;
    public UdonBehaviour videoAdaptor;
    public PointLightVolumeInstance[] emitters;
    public Material lightingMaterial;
    [HideInInspector] public bool ownsLights;
    private Color[] originalColors;
    private Material[] originalMaterials;
    private Texture[] originalTextures;
    private int[] originalProjectionModes;
    private bool[] originalAutoUpdates;
    private PointLightVolumeInstance[] ownedEmitters;
    private UdonBehaviour ownedAdaptor;
    private bool adaptorWasEnabled;
    private bool quitting;

    private void LateUpdate() { RefreshLighting(); }
    private void OnDisable() { RestoreLighting(); }
    private void OnDestroy() { RestoreLighting(); }
    private void OnApplicationQuit() { quitting = true; RestoreLighting(); }

    public void RefreshLighting()
    {
        bool wanted = !quitting && outputEnabled && isActiveAndEnabled && view != null && view.isActiveAndEnabled && view.isActive;
        if (!wanted || lightingMaterial == null || view.output == null || view.screenMaterials == null || view.screenMaterials.Length == 0 || view.screenMaterials[0] == null)
        {
            RestoreLighting();
            return;
        }
        var screen = view.screenMaterials[0];
        lightingMaterial.SetTexture("_MainTex", view.output);
        lightingMaterial.SetColor("_Color", screen.GetColor("_Color"));
        lightingMaterial.SetFloat("_EmissionIntensity", screen.GetFloat("_EmissionIntensity"));
        lightingMaterial.SetFloat("_ScreenOpacity", screen.GetFloat("_ScreenOpacity"));
        if (ownsLights)
        {
            // VizVid can have a delayed cookie-update event queued before disable.
            // Keep our live source active even if that event changes its update flag.
            for (int i = 0; i < ownedEmitters.Length; i++)
                if (ownedEmitters[i] != null && (ownedEmitters[i].CustomTextureMaterial != lightingMaterial || !ownedEmitters[i].AutoUpdateCustomTexture))
                    ownedEmitters[i].SetCustomMaterial(lightingMaterial, true);
            return;
        }
        if (videoAdaptor == null || emitters == null || emitters.Length == 0) return;
        // Area lights can already project VizVid's video cookie material or texture.
        for (int i = 0; i < emitters.Length; i++)
            if (emitters[i] == null || emitters[i].LightType != 2 || emitters[i].ProjectionMode < 0 || emitters[i].ProjectionMode > 2) return;
        ownedEmitters = new PointLightVolumeInstance[emitters.Length];
        originalColors = new Color[emitters.Length];
        originalMaterials = new Material[emitters.Length];
        originalTextures = new Texture[emitters.Length];
        originalProjectionModes = new int[emitters.Length];
        originalAutoUpdates = new bool[emitters.Length];
        for (int i = 0; i < emitters.Length; i++)
        {
            var light = emitters[i]; ownedEmitters[i] = light;
            originalColors[i] = light.Color;
            originalMaterials[i] = light.CustomTextureMaterial;
            originalTextures[i] = light.CustomTexture;
            originalProjectionModes[i] = light.ProjectionMode;
            originalAutoUpdates[i] = light.AutoUpdateCustomTexture;
        }
        ownedAdaptor = videoAdaptor;
        adaptorWasEnabled = ownedAdaptor.enabled;
        ownedAdaptor.enabled = false; // Also blocks already-pending GPU readback callbacks.
        ownsLights = true;
        for (int i = 0; i < ownedEmitters.Length; i++)
        {
            ownedEmitters[i].SetColor(Color.white);
            ownedEmitters[i].SetCustomMaterial(lightingMaterial, true);
        }
    }

    public void RestoreLighting()
    {
        if (!ownsLights) return;
        ownsLights = false;
        for (int i = 0; i < ownedEmitters.Length; i++)
        {
            var light = ownedEmitters[i];
            if (light == null) continue;
            if (originalMaterials[i] != null) light.SetCustomMaterial(originalMaterials[i], originalAutoUpdates[i]);
            else light.SetCustomTexture(originalTextures[i], false, originalAutoUpdates[i]);
            if (originalProjectionModes[i] == 0) light.SetParametric();
            else if (originalProjectionModes[i] == 1) light.SetLut();
            else if (light.ProjectionMode != 2) light.SetCustomTexture();
            light.SetColor(originalColors[i]);
        }
        if (ownedAdaptor != null) ownedAdaptor.enabled = adaptorWasEnabled;
        ownedEmitters = null; ownedAdaptor = null;
    }
}
