using Autohand;
using UnityEngine;

// Conta disparos e acertos nos alvos móveis e marca o ponto de impacto de cada disparo,
// inclusive dos que erram (sugestão dos operadores na avaliação do simulador original).
// Escuta os eventos de todos os AutoGun da cena, então funciona igual no VR e no modo desktop.
public class GerenciadorTreinamento : MonoBehaviour
{
    public static GerenciadorTreinamento Instancia { get; private set; }

    [Tooltip("Efeito no ponto de impacto de um acerto em alvo")]
    public GameObject efeitoAcerto;
    [Tooltip("Efeito no ponto de impacto de um disparo que não acertou alvo")]
    public GameObject efeitoErro;
    public bool mostrarPlacar = true;

    public int Disparos { get; private set; }
    public int AcertosVeiculos { get; private set; }
    public int AcertosPessoas { get; private set; }
    public int AcertosProibidos { get; private set; }

    void Awake() => Instancia = this;

    void Start()
    {
        foreach (var arma in FindObjectsByType<AutoGun>(FindObjectsSortMode.None))
        {
            arma.OnShoot.AddListener(AoDisparar);
            arma.OnHitEvent.AddListener(AoAcertar);
        }
    }

    // O OnShoot vem antes do raycast do AutoGun; refaz o mesmo raycast só para marcar o impacto
    void AoDisparar(AutoGun arma)
    {
        Disparos++;
        var cano = arma.shootForward;
        if (!Physics.Raycast(cano.position, cano.forward, out var hit, arma.maxHitDistance, ~0, QueryTriggerInteraction.Ignore)) return;
        bool emAlvo = hit.collider.GetComponentInParent<AlvoTreinamento>() != null;
        Marcar(emAlvo ? efeitoAcerto : efeitoErro, hit);
    }

    void AoAcertar(AutoGun arma, RaycastHit hit)
    {
        var alvo = hit.collider.GetComponentInParent<AlvoTreinamento>();
        if (alvo == null || !alvo.RegistrarAcerto(hit)) return;

        if (alvo.proibido) AcertosProibidos++;
        else if (alvo.tipo == TipoAlvo.Veiculo) AcertosVeiculos++;
        else AcertosPessoas++;
        Debug.Log($"[Treinamento] Acerto em {alvo.name} ({alvo.tipo}{(alvo.proibido ? ", não autorizado" : "")}) a {hit.distance:F0} m, alvo a {alvo.Velocidade.magnitude * 3.6f:F0} km/h.");
    }

    static void Marcar(GameObject efeito, RaycastHit hit)
    {
        if (efeito == null) return;
        var instancia = Instantiate(efeito, hit.point + hit.normal * 0.05f, Quaternion.LookRotation(hit.normal));
        Destroy(instancia, 3f);
    }

    void OnGUI()
    {
        if (!mostrarPlacar) return;
        string proibidos = AcertosProibidos > 0 ? $" | NÃO AUTORIZADOS: {AcertosProibidos}" : "";
        GUI.Label(new Rect(10, 75, Screen.width - 20, 22),
            $"Alvos móveis: {AcertosVeiculos + AcertosPessoas} acertos (veículos {AcertosVeiculos}, pessoas {AcertosPessoas}) | disparos: {Disparos}{proibidos}");
    }
}
