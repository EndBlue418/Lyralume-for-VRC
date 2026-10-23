using UdonSharp;
using UnityEngine;

// Materials and original slots are baked per instance by the editor; never mutate shared world materials.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class LyralumeScreenOutput : UdonSharpBehaviour
{
    public LyralumeController lyrics;
    public bool outputEnabled = true;
    public bool externallyDriven;
    public Renderer[] screens = new Renderer[0];
    public int[] materialSlots = new int[0];
    public Material[] displayMaterials = new Material[0];
    public Material[] originalMaterials = new Material[0];
    public RenderTexture texture;
    public Camera sourceCamera;
    private bool applied;
    private void Update()
    {
        if (externallyDriven) return;
        bool target = outputEnabled && lyrics != null && lyrics.systemEnabled;
        if (target != applied) SetVisible(target);
    }
    public void SetVisible(bool visible)
    {
        visible = visible && outputEnabled && lyrics != null && lyrics.systemEnabled;
        if (visible == applied) return;
        if (screens == null || materialSlots == null || displayMaterials == null || originalMaterials == null) return;
        for (int i = 0; i < screens.Length && i < materialSlots.Length && i < displayMaterials.Length && i < originalMaterials.Length; i++)
        {
            if (screens[i] == null) continue;
            Material[] materials = screens[i].sharedMaterials;
            int slot = materialSlots[i];
            if (slot < 0 || slot >= materials.Length) continue;
            if (!visible && materials[slot] != displayMaterials[i]) continue;
            materials[slot] = visible ? displayMaterials[i] : originalMaterials[i];
            screens[i].sharedMaterials = materials;
        }
        if (sourceCamera != null) { sourceCamera.targetTexture = texture; sourceCamera.enabled = visible; }
        applied = visible;
    }
    private void OnDisable() { SetVisible(false); }
}
