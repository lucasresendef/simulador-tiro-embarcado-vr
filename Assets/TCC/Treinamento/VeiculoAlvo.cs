using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

// Veículo que percorre uma rota fechada (Spline) com velocidade de cruzeiro.
// Mantém distância do veículo à frente e cede a vez nos cruzamentos, para não se atravessarem.
// Ao ser neutralizado (número de acertos), solta fumaça e para por alguns segundos ou de vez.
[RequireComponent(typeof(AlvoTreinamento))]
public class VeiculoAlvo : MonoBehaviour
{
    public SplineContainer rota;
    [Tooltip("Velocidade de cruzeiro, em km/h")]
    public float velocidadeKmh = 40f;
    [Range(0f, 1f)] public float posicaoInicial;
    [Tooltip("Acertos necessários para neutralizar o veículo")]
    public int acertosParaParar = 1;
    [Tooltip("Tempo parado depois de neutralizado; 0 = para de vez")]
    public float tempoParado = 6f;
    [Tooltip("Desvio lateral máximo, em metros (zigue-zague de quem foge)")]
    public float zigueZague;
    public float aceleracao = 3f;
    public float frenagem = 7f;
    public Material materialFumaca;

    const int Amostras = 1024;
    const float Comprimento = 5f, LarguraFaixa = 2.6f;

    static readonly List<VeiculoAlvo> todos = new List<VeiculoAlvo>();

    public float VelocidadeAtual => velocidade;
    public bool Parado => neutralizado;
    public bool Movendo { get; set; } = true;

    AlvoTreinamento alvo;
    Vector3[] pontos;
    float[] distancias;
    float comprimento, distancia, velocidade, retomarEm;
    int acertosRecebidos;
    bool neutralizado;
    VeiculoAlvo bloqueador;
    ParticleSystem fumaca;

    void OnEnable() => todos.Add(this);
    void OnDisable() => todos.Remove(this);

    void Start()
    {
        alvo = GetComponent<AlvoTreinamento>();
        alvo.AoSerAtingido += (_, _) => AoSerAtingido();
        // Movido pelo transform: a interpolação do corpo rígido só atrasaria a posição
        GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
        AmostrarRota();
        distancia = posicaoInicial * comprimento;
        velocidade = Movendo ? velocidadeKmh / 3.6f : 0f;
        Posicionar();
    }

    // Tabela de distâncias ao longo da rota, para andar com velocidade uniforme
    void AmostrarRota()
    {
        pontos = new Vector3[Amostras + 1];
        distancias = new float[Amostras + 1];
        for (int i = 0; i <= Amostras; i++)
        {
            pontos[i] = rota.EvaluatePosition(i / (float)Amostras);
            if (i > 0) distancias[i] = distancias[i - 1] + Vector3.Distance(pontos[i - 1], pontos[i]);
        }
        comprimento = distancias[Amostras];
    }

    void Update()
    {
        if (neutralizado && tempoParado > 0f && Time.time >= retomarEm) Retomar();

        float alvoVelocidade = neutralizado || !Movendo ? 0f : Mathf.Min(velocidadeKmh / 3.6f, VelocidadeSegura());
        float taxa = alvoVelocidade < velocidade ? frenagem : aceleracao;
        velocidade = Mathf.MoveTowards(velocidade, alvoVelocidade, taxa * Time.deltaTime);
        distancia = Mathf.Repeat(distancia + velocidade * Time.deltaTime, comprimento);
        Posicionar();
        alvo.InformarVelocidade(transform.forward * velocidade);
    }

    // Velocidade que mantém 1,5 s de distância do veículo à frente (ou de quem cruza a frente)
    float VelocidadeSegura()
    {
        float alcance = 8f + velocidade * 2f;
        float maisPerto = float.MaxValue;
        bloqueador = null;
        foreach (var outro in todos)
        {
            if (outro == this) continue;
            float distanciaAFrente;
            if (outro.rota == rota)
            {
                // Mesma rota: distância medida ao longo dela, que funciona também nas curvas
                distanciaAFrente = Mathf.Repeat(outro.distancia - distancia, comprimento);
                if (distanciaAFrente > alcance) continue;
            }
            else
            {
                var relativo = transform.InverseTransformPoint(outro.transform.position);
                if (relativo.z <= 0f || relativo.z > alcance || Mathf.Abs(relativo.x) > LarguraFaixa) continue;
                // Dois veículos que se veem no cruzamento: um deles passa primeiro
                if (outro.bloqueador == this && GetInstanceID() < outro.GetInstanceID()) continue;
                distanciaAFrente = relativo.z;
            }
            if (distanciaAFrente < maisPerto) { maisPerto = distanciaAFrente; bloqueador = outro; }
        }
        if (bloqueador == null) return float.MaxValue;
        float folga = maisPerto - Comprimento - 2f;
        return folga <= 0f ? 0f : folga / 1.5f;
    }

    void AoSerAtingido()
    {
        if (neutralizado) return;
        acertosRecebidos++;
        if (acertosRecebidos < acertosParaParar) return;
        neutralizado = true;
        alvo.Neutralizado = true;
        retomarEm = Time.time + tempoParado;
        SoltarFumaca(true);
    }

    void Retomar()
    {
        neutralizado = false;
        alvo.Neutralizado = false;
        acertosRecebidos = 0;
        SoltarFumaca(false);
    }

    void SoltarFumaca(bool ligar)
    {
        if (fumaca == null)
        {
            if (!ligar || materialFumaca == null) return;
            fumaca = CriarFumaca();
        }
        if (ligar) fumaca.Play();
        else fumaca.Stop();
    }

    ParticleSystem CriarFumaca()
    {
        var objeto = new GameObject("Fumaça");
        objeto.transform.SetParent(transform, false);
        objeto.transform.localPosition = new Vector3(0f, 1.2f, 1.5f);
        var sistema = objeto.AddComponent<ParticleSystem>();
        sistema.Stop();
        var principal = sistema.main;
        principal.startLifetime = 3f;
        principal.startSpeed = 1.5f;
        principal.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        principal.startColor = new Color(0.25f, 0.25f, 0.25f, 0.6f);
        principal.simulationSpace = ParticleSystemSimulationSpace.World;
        var emissao = sistema.emission;
        emissao.rateOverTime = 12f;
        var forma = sistema.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 15f;
        forma.radius = 0.3f;
        forma.rotation = new Vector3(-90f, 0f, 0f);
        var cor = sistema.colorOverLifetime;
        cor.enabled = true;
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] { new GradientColorKey(Color.gray, 0f), new GradientColorKey(Color.gray, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        cor.color = gradiente;
        var tamanho = sistema.sizeOverLifetime;
        tamanho.enabled = true;
        tamanho.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));
        objeto.GetComponent<ParticleSystemRenderer>().material = materialFumaca;
        return sistema;
    }

    void Posicionar()
    {
        var posicao = PontoEm(distancia);
        var frente = PontoEm(distancia + 2f) - posicao;
        if (zigueZague > 0f && frente.sqrMagnitude > 0.0001f)
        {
            // Troca de faixa suave, como quem tenta despistar
            float lateral = Mathf.Sin(distancia / 60f) * Mathf.Sin(distancia / 23f) * zigueZague;
            posicao += Vector3.Cross(Vector3.up, frente.normalized) * lateral;
        }
        transform.position = posicao;
        if (frente.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
    }

    Vector3 PontoEm(float d)
    {
        d = Mathf.Repeat(d, comprimento);
        int i = System.Array.BinarySearch(distancias, d);
        if (i >= 0) return pontos[i];
        i = ~i;
        float t = Mathf.InverseLerp(distancias[i - 1], distancias[i], d);
        return Vector3.Lerp(pontos[i - 1], pontos[i], t);
    }
}
