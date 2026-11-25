using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

// VR no PC (Windows): troca o Oculus XR Plugin herdado do projeto base pelo OpenXR, que atende
// o PICO 4 pelo PICO Connect + SteamVR (e também o Meta Quest pelo Link), e ativa os perfis de
// controle que o SteamVR usa para mapear os controles. Não altera a configuração do Android.
// Pela linha de comando: Unity -batchmode -quit -projectPath <projeto> -executeMethod ConfigurarVR.Run
public static class ConfigurarVR
{
    const string LoaderOpenXR = "UnityEngine.XR.OpenXR.OpenXRLoader";
    const string LoaderOculus = "Unity.XR.Oculus.OculusLoader";

    [MenuItem("TCC/Configurar VR (PICO 4 via OpenXR)")]
    public static void Run()
    {
        var grupo = BuildTargetGroup.Standalone;
        var geral = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(grupo);
        if (geral == null || geral.Manager == null)
        {
            Debug.LogError("[TCC] Configurações do XR Plug-in Management para Standalone não encontradas.");
            return;
        }
        geral.InitManagerOnStart = true;
        XRPackageMetadataStore.RemoveLoader(geral.Manager, LoaderOculus, grupo);
        XRPackageMetadataStore.AssignLoader(geral.Manager, LoaderOpenXR, grupo);

        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(grupo);
        // Perfis de controle: o SteamVR escolhe um que o app suporte e mapeia os controles do PICO 4
        Ativar<OculusTouchControllerProfile>(openxr);
        Ativar<MetaQuestTouchPlusControllerProfile>(openxr);
        Ativar<ValveIndexControllerProfile>(openxr);
        Ativar<HTCViveControllerProfile>(openxr);
        Ativar<KHRSimpleControllerProfile>(openxr);
        // Multi-pass: cada olho é desenhado separadamente. No Single Pass Instanced a interface (uGUI:
        // menu, placar, textos) só aparecia no olho esquerdo; os objetos 3D apareciam nos dois
        openxr.renderMode = OpenXRSettings.RenderMode.MultiPass;
        EditorUtility.SetDirty(openxr);
        EditorUtility.SetDirty(geral);
        EditorUtility.SetDirty(geral.Manager);
        AssetDatabase.SaveAssets();

        var carregadores = string.Join(", ", geral.Manager.activeLoaders.Select(l => l.GetType().Name));
        var perfis = string.Join(", ", openxr.GetFeatures<OpenXRInteractionFeature>().Where(f => f.enabled).Select(f => f.GetType().Name));
        Debug.Log($"[TCC] VR no Windows: carregadores [{carregadores}]; perfis de controle [{perfis}].");
    }

    static void Ativar<T>(OpenXRSettings openxr) where T : OpenXRFeature
    {
        var recurso = openxr.GetFeature<T>();
        if (recurso == null) { Debug.LogWarning($"[TCC] Recurso OpenXR {typeof(T).Name} não encontrado."); return; }
        recurso.enabled = true;
        EditorUtility.SetDirty(recurso);
    }
}
