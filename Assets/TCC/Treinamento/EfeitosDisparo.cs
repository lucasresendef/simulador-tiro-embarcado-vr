using Autohand;
using UnityEngine;

// Efeitos de cada disparo: clarão no cano, traçante até o impacto e, no ponto atingido,
// faíscas (alvo) ou poeira (solo/estruturas). Partículas criadas por código, sem assets pagos.
public class EfeitosDisparo : MonoBehaviour
{
    public Material materialAditivo;
    [Tooltip("Material do traçante (cor HDR, para brilhar com o bloom)")]
    public Material materialTracante;
    public Material materialPoeira;

    public void Disparo(AutoGun arma, bool acertou, RaycastHit hit, bool emAlvo)
    {
        var cano = arma.shootForward;
        Clarao(cano.position, cano.forward);
        var fim = acertou ? hit.point : cano.position + cano.forward * Mathf.Min(arma.maxHitDistance, 400f);
        Tracante(cano.position, fim);
        if (!acertou) return;
        if (emAlvo) Faiscas(hit.point, hit.normal);
        else Poeira(hit.point, hit.normal);
    }

    void Clarao(Vector3 posicao, Vector3 direcao)
    {
        var objeto = new GameObject("Clarão");
        objeto.transform.SetPositionAndRotation(posicao, Quaternion.LookRotation(direcao));
        var luz = objeto.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.75f, 0.4f);
        luz.range = 6f;
        luz.intensity = 4f;
        var sistema = Particulas(objeto, materialAditivo, 0.06f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        principal.startColor = new Color(1f, 0.7f, 0.3f, 1f);
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 12f;
        forma.radius = 0.02f;
        sistema.Emit(8);
        Destroy(objeto, 0.08f);
    }

    void Tracante(Vector3 de, Vector3 para)
    {
        var objeto = new GameObject("Traçante");
        var linha = objeto.AddComponent<LineRenderer>();
        linha.sharedMaterial = materialTracante != null ? materialTracante : materialAditivo;
        linha.positionCount = 2;
        linha.startColor = new Color(1f, 1f, 1f, 0.2f);
        linha.endColor = Color.white;
        linha.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        objeto.AddComponent<RastroTracante>().Iniciar(linha, de, para);
    }

    // Rastro luminoso que viaja do cano ao impacto (mais largo ao longe, para ser visto de longe)
    class RastroTracante : MonoBehaviour
    {
        const float Duracao = 0.18f, Comprimento = 20f;
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
            linha.SetPosition(0, de + direcao * Mathf.Max(0f, frente - Comprimento));
            linha.SetPosition(1, de + direcao * frente);
            float largura = Mathf.Lerp(0.1f, 0.6f, frente / Mathf.Max(distancia, 1f));
            linha.startWidth = largura * 0.4f;
            linha.endWidth = largura;
        }
    }

    void Faiscas(Vector3 ponto, Vector3 normal)
    {
        var objeto = new GameObject("Faíscas");
        objeto.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(normal));
        var sistema = Particulas(objeto, materialAditivo, 0.5f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
        principal.startColor = new Color(1f, 0.75f, 0.35f, 1f);
        principal.gravityModifier = 1.5f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 45f;
        sistema.Emit(25);
        Poeira(ponto, normal, 0.5f);
        Destroy(objeto, 1f);
    }

    void Poeira(Vector3 ponto, Vector3 normal, float escala = 1f)
    {
        var objeto = new GameObject("Poeira");
        objeto.transform.SetPositionAndRotation(ponto, Quaternion.LookRotation(normal));
        var sistema = Particulas(objeto, materialPoeira, 1.6f);
        var principal = sistema.main;
        principal.startSize = new ParticleSystem.MinMaxCurve(1.2f * escala, 2.8f * escala);
        principal.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        principal.startColor = new Color(0.78f, 0.72f, 0.62f, 0.9f);
        principal.gravityModifier = 0.15f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 35f;
        var cor = sistema.colorOverLifetime;
        cor.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        cor.color = gradiente;
        var tamanho = sistema.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
        sistema.Emit(Mathf.RoundToInt(24 * escala));
        Destroy(objeto, 2f);
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
        return sistema;
    }
}
