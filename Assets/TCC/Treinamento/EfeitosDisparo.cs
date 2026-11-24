using Autohand;
using UnityEngine;

// Efeitos de cada disparo, num único sistema feito por código (sem assets pagos): clarão curto no
// cano, traçante fino até o impacto e, no ponto atingido, poeira e detritos (solo e estruturas),
// faíscas pequenas (veículos) ou uma nuvem discreta (pessoas). Os tamanhos são os de um impacto
// real de fuzil até 14 m e crescem com a distância depois disso, para continuar visíveis a 100–300 m.
// Tudo é geometria no mundo, então aparece igual no modo desktop e no óculos.
public class EfeitosDisparo : MonoBehaviour
{
    public Material materialAditivo;
    [Tooltip("Material do traçante (cor HDR, para brilhar com o bloom)")]
    public Material materialTracante;
    public Material materialPoeira;
    [Tooltip("Intensidade do traçante (multiplica a cor HDR do material)")]
    [Range(0f, 1f)] public float brilhoTracante = 0.5f;

    public void Disparo(AutoGun arma, bool acertou, RaycastHit hit, AlvoTreinamento alvo)
    {
        var cano = arma.shootForward;
        Clarao(cano.position, cano.forward);
        var fim = acertou ? hit.point : cano.position + cano.forward * Mathf.Min(arma.maxHitDistance, 400f);
        Tracante(cano.position, fim, brilhoTracante);
        if (!acertou) return;

        float escala = EscalaPelaDistancia(hit.point);
        if (alvo == null) Poeira(hit.point, hit.normal, escala, true);
        else if (alvo.tipo == TipoAlvo.Veiculo) { Faiscas(hit.point, hit.normal, escala); Poeira(hit.point, hit.normal, escala * 0.5f, false); }
        else Poeira(hit.point, hit.normal, escala * 0.6f, false);
    }

    // Tamanho real até 14 m; depois cresce com a distância, mantendo cerca de 1,8° de tamanho aparente,
    // o suficiente para o impacto ser visto ao redor da mira a 100–300 m sem virar uma explosão
    static float EscalaPelaDistancia(Vector3 ponto)
    {
        var camera = Camera.main;
        float distancia = camera != null ? Vector3.Distance(camera.transform.position, ponto) : 100f;
        return Mathf.Max(1f, distancia / 14f);
    }

    void Clarao(Vector3 posicao, Vector3 direcao)
    {
        var objeto = new GameObject("Clarão");
        objeto.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(direcao));
        var luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.78f, 0.5f);
        luz.range = 2f;
        luz.intensity = 0.4f;
        var sistema = Particulas(objeto, materialAditivo, 0.04f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        principal.startColor = new Color(1f, 0.82f, 0.55f, 0.7f);
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 10f;
        forma.radius = 0.01f;
        sistema.Emit(4);
        Destroy(objeto, 0.06f);
    }

    void Tracante(Vector3 de, Vector3 para, float brilho)
    {
        var objeto = new GameObject("Traçante");
        var linha = objeto.AddComponent<LineRenderer>();
        linha.sharedMaterial = materialTracante != null ? materialTracante : materialAditivo;
        linha.positionCount = 2;
        // No material aditivo a cor do vértice multiplica a cor HDR: o brilho controla a intensidade
        linha.startColor = new Color(brilho, brilho, brilho, 0f);
        linha.endColor = new Color(brilho, brilho, brilho, 1f);
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        linha.numCapVertices = 2;
        linha.allowOcclusionWhenDynamic = false;
        objeto.AddComponent<RastroTracante>().Iniciar(linha, de, para);
    }

    // Rastro curto que viaja do cano ao impacto, com espessura aparente parecida de perto e de longe
    class RastroTracante : MonoBehaviour
    {
        const float Duracao = 0.12f, Comprimento = 12f;
        LineRenderer linha;
        Vector3 de, para;
        float inicio;

        public void Iniciar(LineRenderer linhaRastro, Vector3 origem, Vector3 destino)
        {
            linha = linhaRastro; de = origem; para = destino; inicio = Time.time;
            Atualizar();
        }

        void Update() => Atualizar();

        void Atualizar()
        {
            float k = (Time.time - inicio) / Duracao;
            if (k >= 1f) { Destroy(gameObject); return; }
            float distancia = Vector3.Distance(de, para);
            var direcao = (para - de) / Mathf.Max(distancia, 0.01f);
            float frente = Mathf.Lerp(Mathf.Min(Comprimento, distancia), distancia, k);
            float tras = Mathf.Max(0f, frente - Comprimento);
            linha.SetPosition(0, de + direcao * tras);
            linha.SetPosition(1, de + direcao * frente);
            // Cerca de 0,15° de espessura aparente, com mínimo de 1 cm perto do operador
            linha.startWidth = Mathf.Max(0.01f, tras * 0.0026f);
            linha.endWidth = Mathf.Max(0.01f, frente * 0.0026f);
        }
    }

    void Faiscas(Vector3 ponto, Vector3 normal, float escala)
    {
        var objeto = new GameObject("Faíscas");
        objeto.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(normal));
        var sistema = Particulas(objeto, materialAditivo, 0.22f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.025f * escala, 0.06f * escala);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(3f * escala, 7f * escala);
        principal.startColor = new Color(1f, 0.85f, 0.5f, 0.9f);
        principal.gravityModifier = 1f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 40f;
        var renderizador = objeto.GetComponent<ParticleSystemRenderer>();
        renderizador.renderMode = ParticleSystemRenderMode.Stretch;
        renderizador.velocityScale = 0.04f;
        renderizador.lengthScale = 1f;
        sistema.Emit(10);
        Destroy(objeto, 0.4f);
    }

    // Nuvem de poeira que se abre e some em cerca de 1 s; com detritos, alguns pedaços sobem e caem
    void Poeira(Vector3 ponto, Vector3 normal, float escala, bool detritos)
    {
        var objeto = new GameObject("Poeira");
        objeto.transform.SetPositionAndRotation(ponto + normal * 0.05f, Quaternion.LookRotation(normal));
        var sistema = Particulas(objeto, materialPoeira, 1.1f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.3f * escala, 0.7f * escala);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * escala, 2.2f * escala);
        // Poeira levantada fica mais clara que o chão; opaca no núcleo para aparecer sobre qualquer piso
        principal.startColor = new ParticleSystem.MinMaxGradient(new Color(0.86f, 0.83f, 0.77f, 1f), new Color(0.72f, 0.68f, 0.61f, 1f));
        principal.gravityModifier = 0.05f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 25f;
        forma.radius = 0.05f;
        var arrasto = sistema.limitVelocityOverLifetime;
        arrasto.enabled = true;
        arrasto.limit = 0.5f * escala;
        arrasto.dampen = 0.15f;
        var cor = sistema.colorOverLifetime;
        cor.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.35f), new GradientAlphaKey(0f, 1f) });
        cor.color = gradiente;
        var tamanho = sistema.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
        sistema.Emit(14);
        if (detritos) Detritos(ponto, normal, escala);
        Destroy(objeto, 1.3f);
    }

    void Detritos(Vector3 ponto, Vector3 normal, float escala)
    {
        var objeto = new GameObject("Detritos");
        objeto.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(normal));
        var sistema = Particulas(objeto, materialPoeira, 0.6f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.03f * escala, 0.07f * escala);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(2f * escala, 4.5f * escala);
        principal.startColor = new Color(0.3f, 0.27f, 0.23f, 1f);
        principal.gravityModifier = 1.2f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 30f;
        sistema.Emit(6);
        Destroy(objeto, 0.7f);
    }

    static ParticleSystem Particulas(GameObject objeto, Material material, float vida)
    {
        var sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var principal = sistema.main;
        principal.loop = false;
        principal.playOnAwake = false;
        principal.startLifetime = new ParticleSystem.MinMaxCurve(vida * 0.6f, vida);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        var emissao = sistema.emission;
        emissao.enabled = false;
        var renderizador = objeto.GetComponent<ParticleSystemRenderer>();
        renderizador.sharedMaterial = material;
        renderizador.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // A cidade tem oclusão pré-calculada; sem isto, efeitos vistos do alto eram descartados
        renderizador.allowOcclusionWhenDynamic = false;
        return sistema;
    }
}
