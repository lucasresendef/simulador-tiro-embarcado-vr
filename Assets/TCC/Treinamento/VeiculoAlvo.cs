using UnityEngine;
using UnityEngine.Splines;

// Veículo que percorre uma rota fechada (Spline) com velocidade constante.
// Ao ser atingido, para por alguns segundos e depois retoma o percurso.
[RequireComponent(typeof(AlvoTreinamento))]
public class VeiculoAlvo : MonoBehaviour
{
    public SplineContainer rota;
    [Tooltip("Velocidade de cruzeiro, em km/h")]
    public float velocidadeKmh = 40f;
    [Range(0f, 1f)] public float posicaoInicial;
    public float tempoParado = 6f;
    public float aceleracao = 3f;

    const int Amostras = 512;

    AlvoTreinamento alvo;
    Vector3[] pontos;
    float[] distancias;
    float comprimento, distancia, velocidade, retomarEm;

    void Start()
    {
        alvo = GetComponent<AlvoTreinamento>();
        alvo.AoSerAtingido += (_, _) => Parar();
        AmostrarRota();
        distancia = posicaoInicial * comprimento;
        velocidade = velocidadeKmh / 3.6f;
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
        bool parado = Time.time < retomarEm;
        if (!parado) alvo.Neutralizado = false;
        float alvoVelocidade = parado ? 0f : velocidadeKmh / 3.6f;
        velocidade = Mathf.MoveTowards(velocidade, alvoVelocidade, aceleracao * Time.deltaTime);
        distancia = Mathf.Repeat(distancia + velocidade * Time.deltaTime, comprimento);
        Posicionar();
    }

    void Parar()
    {
        alvo.Neutralizado = true;
        retomarEm = Time.time + tempoParado;
    }

    void Posicionar()
    {
        var posicao = PontoEm(distancia);
        var frente = PontoEm(distancia + 2f) - posicao;
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
