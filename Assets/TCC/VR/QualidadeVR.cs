using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Qualidade gráfica do simulador, escolhida no menu TCC → Qualidade VR (ver Editor/MenuQualidadeVR.cs).
// Completa (padrão): nada é alterado.
// Reduzida: para testar o funcionamento do VR em PC sem placa de vídeo dedicada. Renderiza o óculos
// com metade da resolução por olho e desliga pós-processamento, sombras e antisserrilhado, além de
// encurtar o alcance de visão, das árvores e dos detalhes do terreno. As mudanças valem só durante a
// execução: a cena e os assets não são modificados.
// A escolha fica salva em Assets/TCC/Resources/QualidadeVR.asset e vai junto no executável.
public class QualidadeVR : ScriptableObject
{
    public enum Modo { Completa, Reduzida }

    public Modo modo = Modo.Completa;

    public const string NomeNoResources = "QualidadeVR";

    // Fração da resolução por olho pedida pelo runtime (0,5 = um quarto dos pixels)
    const float EscalaResolucaoReduzida = 0.5f;
    // Alcance da câmera em metros (o helicóptero voa a ~60 m de altura)
    const float AlcanceVisaoReduzido = 1500f;
    // Distância entre a mão virtual e o controle até a mão soltar o objeto (o AutoHand usa 0,5 m)
    const float DistanciaMaximaMao = 3f;

    public static Modo Atual
    {
        get
        {
            var config = Resources.Load<QualidadeVR>(NomeNoResources);
            return config != null ? config.modo : Modo.Completa;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modoCarga)
    {
        if (Atual != Modo.Reduzida)
        {
            RestaurarPipeline();
            XRSettings.eyeTextureResolutionScale = 1f;
            return;
        }
        AplicarReduzida(cena);
    }

    static RenderPipelineAsset pipelineOriginal;
    static bool pipelineTrocado;

    // O URP sobrescreve XRSettings.eyeTextureResolutionScale a cada quadro com o renderScale do seu asset.
    // Por isso a resolução é reduzida numa cópia do asset em uso, criada só em memória: o arquivo em
    // Assets/Settings não é alterado, e o original volta ao sair do Play ou fechar o executável.
    static void TrocarPipeline()
    {
        if (pipelineTrocado) return;
        var atual = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
        if (!(atual is UniversalRenderPipelineAsset urp)) return;

        var copia = Object.Instantiate(urp);
        copia.name = urp.name + " (Qualidade VR reduzida)";
        copia.renderScale = EscalaResolucaoReduzida;
        copia.shadowDistance = 0f;
        copia.msaaSampleCount = 1;
        copia.supportsHDR = false;

        pipelineOriginal = QualitySettings.renderPipeline;
        QualitySettings.renderPipeline = copia;
        pipelineTrocado = true;
        Application.quitting += RestaurarPipeline;
    }

    static void RestaurarPipeline()
    {
        Application.quitting -= RestaurarPipeline;
        if (!pipelineTrocado) return;
        QualitySettings.renderPipeline = pipelineOriginal;
        pipelineTrocado = false;
    }

    static void AplicarReduzida(Scene cena)
    {
        TrocarPipeline();
        XRSettings.eyeTextureResolutionScale = EscalaResolucaoReduzida;

        // A poucos quadros por segundo, o controle "salta" mais de 0,5 m entre um quadro e outro e o
        // AutoHand solta o que a mão segura (HandBase.maxFollowDistance): o fuzil caía do helicóptero
        foreach (var mao in Object.FindObjectsByType<Autohand.Hand>(FindObjectsInactive.Include))
            mao.maxFollowDistance = Mathf.Max(mao.maxFollowDistance, DistanciaMaximaMao);
        // A mão segura o objeto por uma junta física que quebra acima de jointBreakForce. Com passos de
        // física longos (poucos quadros por segundo e helicóptero em movimento) a força estoura e a mão
        // solta o fuzil sozinha; aqui a junta só se desfaz ao soltar o grip
        foreach (var pegavel in Object.FindObjectsByType<Autohand.Grabbable>(FindObjectsInactive.Include))
            pegavel.jointBreakForce = float.PositiveInfinity;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.farClipPlane = Mathf.Min(camera.farClipPlane, AlcanceVisaoReduzido);
            var dados = camera.GetUniversalAdditionalCameraData();
            if (dados == null) continue;
            dados.renderPostProcessing = false;
            dados.renderShadows = false;
            dados.antialiasing = AntialiasingMode.None;
        }

        foreach (var luz in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            luz.shadows = LightShadows.None;

        // Pós-processamento e bruma volumétrica dos Volumes também ficam desligados
        foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
            volume.enabled = false;

        foreach (var terreno in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            terreno.heightmapPixelError = Mathf.Max(terreno.heightmapPixelError, 20f);
            terreno.basemapDistance = Mathf.Min(terreno.basemapDistance, 300f);
            terreno.treeDistance = Mathf.Min(terreno.treeDistance, 600f);
            terreno.treeBillboardDistance = Mathf.Min(terreno.treeBillboardDistance, 80f);
            terreno.detailObjectDistance = Mathf.Min(terreno.detailObjectDistance, 40f);
            terreno.shadowCastingMode = ShadowCastingMode.Off;
        }

        foreach (var sonda in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include))
            if (sonda.mode == ReflectionProbeMode.Realtime && sonda.refreshMode == ReflectionProbeRefreshMode.EveryFrame)
                sonda.refreshMode = ReflectionProbeRefreshMode.ViaScripting;

        Debug.Log($"[Qualidade VR] Reduzida na cena '{cena.name}': resolução por olho {EscalaResolucaoReduzida:P0}, " +
                  $"sem pós-processamento, sombras e antisserrilhado, alcance de visão {AlcanceVisaoReduzido:0} m, " +
                  $"mão solta o objeto só a {DistanciaMaximaMao:0} m do controle e a pegada não quebra por força. " +
                  "Para voltar: menu TCC → Qualidade VR → Completa.");
    }
}
