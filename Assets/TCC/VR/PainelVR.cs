using Autohand;
using TMPro;
using UnityEngine;
using UnityEngine.XR;

// Placar dentro do óculos: o HUD de texto (OnGUI) só aparece no monitor do PC, então no VR o
// operador veria os impactos mas nenhum número. Este painel, montado pela ferramenta
// TCC → Adicionar painel VR, fica na cabine ao lado do operador (para o lado do nariz da
// aeronave, fora da linha de tiro pela porta) e mostra disparos, acertos e a situação do cenário.
public class PainelVR : MonoBehaviour
{
    public TextMeshProUGUI texto;
    [Tooltip("Ângulo para o lado, a partir do olhar inicial (positivo = direita)")]
    public float anguloLateral = 70f;
    [Tooltip("Distância dos olhos, em metros")]
    public float distancia = 0.55f;
    [Tooltip("Altura em relação aos olhos, em metros")]
    public float altura = -0.32f;
    [Tooltip("Mostrar também sem óculos (para testar o painel no modo desktop)")]
    public bool mostrarSemOculos;

    const string Destaque = "#F5A82E";
    const string Alerta = "#FF6A5C";

    AutoHandPlayer jogador;
    PilotoAutomatico piloto;
    PerseguicaoVeicular perseguicao;
    AutoGun fuzil;
    float proximaAtualizacao;

    void Start()
    {
        bool emVR = XRSettings.isDeviceActive && !ModoDesktop.Ativo;
        if (!emVR && !mostrarSemOculos) { gameObject.SetActive(false); return; }
        jogador = FindFirstObjectByType<AutoHandPlayer>();
        piloto = FindFirstObjectByType<PilotoAutomatico>();
        perseguicao = FindFirstObjectByType<PerseguicaoVeicular>();
        fuzil = FindFirstObjectByType<AutoGun>();
        CalibracaoOperador.Calibrado += Posicionar;
        Invoke(nameof(Posicionar), 0.2f);
    }

    void OnDestroy() => CalibracaoOperador.Calibrado -= Posicionar;

    // Preso ao espaço de rastreamento (anda com a aeronave e com a cabeça calibrada)
    void Posicionar()
    {
        Transform cabeca = jogador != null && jogador.headCamera != null ? jogador.headCamera.transform : Camera.main != null ? Camera.main.transform : null;
        if (cabeca == null) return;
        Transform pai = jogador != null && jogador.trackingContainer != null ? jogador.trackingContainer : cabeca.parent;
        transform.SetParent(pai, true);

        var frente = Vector3.ProjectOnPlane(jogador != null ? jogador.transform.forward : cabeca.forward, Vector3.up).normalized;
        // Para o lado do nariz da aeronave: com a porta à esquerda o operador olha para a esquerda e o nariz fica à direita
        float lado = piloto != null && piloto.porta == PilotoAutomatico.LadoPorta.Direita ? -1f : 1f;
        var direcao = Quaternion.AngleAxis(lado * anguloLateral, Vector3.up) * frente;
        transform.position = cabeca.position + direcao * distancia + Vector3.up * altura;
        transform.rotation = Quaternion.LookRotation(transform.position - cabeca.position, Vector3.up);
    }

    void Update()
    {
        if (Time.time < proximaAtualizacao || texto == null) return;
        proximaAtualizacao = Time.time + 0.2f;

        var placar = GerenciadorTreinamento.Instancia;
        var sb = new System.Text.StringBuilder();
        if (placar != null)
        {
            int acertos = placar.AcertosVeiculos + placar.AcertosPessoas;
            sb.Append($"<color={Destaque}>Acertos</color> <b>{acertos}</b>/{placar.Disparos}");
            if (placar.AcertosProibidos > 0) sb.Append($"\n<color={Alerta}>Não autorizados: {placar.AcertosProibidos}</color>");
        }
        if (perseguicao != null) sb.Append('\n').Append(perseguicao.Resumo);
        if (fuzil != null) sb.Append($"\n<size=75%><alpha=#AA>Munição {fuzil.GetAmmo()}</size>");
        texto.text = sb.ToString();
    }
}
