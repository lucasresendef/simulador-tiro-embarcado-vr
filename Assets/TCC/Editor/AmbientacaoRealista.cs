using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Ambientação mais realista com recursos gratuitos: céu HDRI do Poly Haven (CC0) iluminando a
// cena, sol alinhado com o do céu, bruma no horizonte e pós-processamento (ACES, cor, bloom)
public static class AmbientacaoRealista
{
    const string Pasta = "Assets/TCC/Cenario";
    const string Hdri = "Assets/TCC/Externos/PolyHaven/kloofendal_48d_partly_cloudy_puresky_4k.hdr";
    // Posição do sol na imagem do céu (medida no arquivo HDR): u = 0,595, elevação de 48°
    const float SolU = 0.595f, SolElevacao = 48f;
    static readonly Color CorBruma = new Color(0.72f, 0.78f, 0.85f);

    public static void Aplicar(Scene cena, float inicioBruma, float fimBruma)
    {
        RenderSettings.skybox = MaterialCeu();
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = CorBruma;
        RenderSettings.fogStartDistance = inicioBruma;
        RenderSettings.fogEndDistance = fimBruma;

        // Sol na mesma direção do sol do céu (mapeamento do Skybox/Panoramic do Unity)
        float longitude = (0.5f - SolU) * 2f * Mathf.PI;
        float elevacao = SolElevacao * Mathf.Deg2Rad;
        var paraOSol = new Vector3(Mathf.Cos(longitude) * Mathf.Cos(elevacao), Mathf.Sin(elevacao), Mathf.Sin(longitude) * Mathf.Cos(elevacao));
        var sol = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sol != null)
        {
            sol.transform.rotation = Quaternion.LookRotation(-paraOSol);
            sol.intensity = 1.6f;
            sol.color = new Color(1f, 0.95f, 0.88f);
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.85f;
            RenderSettings.sun = sol;
            EditorUtility.SetDirty(sol);
        }

        foreach (var antigo in cena.GetRootGameObjects().Where(r => r.name == "[TCC] Ambiente")) Object.DestroyImmediate(antigo);
        var ambiente = new GameObject("[TCC] Ambiente");
        ambiente.AddComponent<AmbienteRealista>();
        var volume = ambiente.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = PerfilPosProcessamento();

        // Reflexos do céu nos carros e vidros (sonda que só captura o céu, uma vez)
        var reflexos = new GameObject("Reflexos do céu").AddComponent<ReflectionProbe>();
        reflexos.transform.SetParent(ambiente.transform);
        reflexos.mode = ReflectionProbeMode.Realtime;
        reflexos.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        reflexos.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        reflexos.size = new Vector3(6000f, 2000f, 6000f);
        reflexos.resolution = 128;
        reflexos.cullingMask = 0;
        reflexos.importance = 0;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var dados = camera.GetUniversalAdditionalCameraData();
            dados.renderPostProcessing = true;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, fimBruma + 500f);
            EditorUtility.SetDirty(dados);
            EditorUtility.SetDirty(camera);
        }

        // Vista aérea: sombras até mais longe
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && urp.shadowDistance < 250f)
        {
            urp.shadowDistance = 250f;
            EditorUtility.SetDirty(urp);
        }
    }

    static Material MaterialCeu()
    {
        var caminho = Pasta + "/Céu HDRI.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Skybox/Panoramic"));
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Hdri));
        material.SetFloat("_Mapping", 1f);
        material.SetFloat("_ImageType", 0f);
        material.SetFloat("_Exposure", 1f);
        material.SetFloat("_Rotation", 0f);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    static VolumeProfile PerfilPosProcessamento()
    {
        var caminho = Pasta + "/Pós-processamento.asset";
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(caminho);
        if (perfil != null) return perfil;
        perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(perfil, caminho);

        var tonemapping = perfil.Add<Tonemapping>(true);
        tonemapping.mode.value = TonemappingMode.ACES;
        var cor = perfil.Add<ColorAdjustments>(true);
        cor.postExposure.value = 0.15f;
        cor.contrast.value = 12f;
        cor.saturation.value = 6f;
        var bloom = perfil.Add<Bloom>(true);
        bloom.threshold.value = 1.1f;
        bloom.intensity.value = 0.25f;
        bloom.scatter.value = 0.6f;
        var vinheta = perfil.Add<Vignette>(true);
        vinheta.intensity.value = 0.18f;
        vinheta.smoothness.value = 0.4f;
        foreach (var componente in perfil.components) AssetDatabase.AddObjectToAsset(componente, perfil);
        AssetDatabase.SaveAssets();
        return perfil;
    }
}
