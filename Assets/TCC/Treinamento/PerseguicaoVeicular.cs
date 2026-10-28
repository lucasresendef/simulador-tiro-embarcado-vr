using UnityEngine;

// Cenário de perseguição: o suspeito foge pela rodovia seguido pelas viaturas, e o helicóptero
// o acompanha lado a lado. A fuga começa quando a aeronave passa da altura de início;
// termina quando o suspeito é neutralizado. Acertar viaturas ou civis conta como erro.
public class PerseguicaoVeicular : MonoBehaviour
{
    public VeiculoAlvo suspeito;
    public VeiculoAlvo[] viaturas;
    public PilotoAutomatico piloto;
    public float alturaInicioPes = 150f;

    float inicio = -1f, fim = -1f;
    float alturaSolo;

    void Start()
    {
        alturaSolo = piloto.transform.position.y;
        suspeito.Movendo = false;
        foreach (var viatura in viaturas) viatura.Movendo = false;
    }

    void Update()
    {
        if (inicio < 0f && (piloto.transform.position.y - alturaSolo) / 0.3048f >= alturaInicioPes)
        {
            inicio = Time.time;
            suspeito.Movendo = true;
            foreach (var viatura in viaturas) viatura.Movendo = true;
        }
        if (inicio >= 0f && fim < 0f && suspeito.Parado) fim = Time.time;
    }

    static string Tempo(float segundos) => $"{(int)(segundos / 60f):00}:{(int)(segundos % 60f):00}";

    void OnGUI()
    {
        var alvo = suspeito.GetComponent<AlvoTreinamento>();
        var placar = GerenciadorTreinamento.Instancia;
        string texto;
        if (inicio < 0f) texto = $"PERSEGUIÇÃO: aguardando a aeronave passar de {alturaInicioPes:F0} ft para o suspeito fugir";
        else if (fim < 0f) texto = $"PERSEGUIÇÃO: suspeito a {suspeito.VelocidadeAtual * 3.6f:F0} km/h | acertos no suspeito: {alvo.Acertos}/{suspeito.acertosParaParar} | tempo {Tempo(Time.time - inicio)}";
        else texto = $"SUSPEITO NEUTRALIZADO em {Tempo(fim - inicio)} com {placar?.Disparos ?? 0} disparos | R reinicia o cenário";
        GUI.Label(new Rect(10, 95, Screen.width - 20, 22), texto);
    }
}
