using Autohand;
using UnityEngine;

// Conta disparos e acertos nos alvos móveis e marca o ponto de impacto de cada disparo,
// inclusive dos que erram (sugestão dos operadores na avaliação do simulador original).
// Escuta os eventos de todos os AutoGun da cena, então funciona igual no VR e no modo desktop.
public class GerenciadorTreinamento : MonoBehaviour
{
    public static GerenciadorTreinamento Instancia { get; private set; }

    public bool mostrarPlacar = true;
    [Header("Vibração do controle (VR)")]
    [Tooltip("Pulso a cada disparo: duração (s) e intensidade (0 a 1)")]
    public Vector2 vibracaoDisparo = new Vector2(0.06f, 0.6f);
    [Tooltip("Pulso extra ao acertar um alvo: duração (s) e intensidade (0 a 1)")]
    public Vector2 vibracaoAcerto = new Vector2(0.14f, 1f);

    public int Disparos { get; private set; }
    public int AcertosVeiculos { get; private set; }
    public int AcertosPessoas { get; private set; }
    public int AcertosProibidos { get; private set; }

    EfeitosDisparo efeitos;

    void Awake()
    {
        Instancia = this;
        efeitos = GetComponent<EfeitosDisparo>();
    }

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
        Vibrar(arma, vibracaoDisparo);
        var cano = arma.shootForward;
        bool acertou = Physics.Raycast(cano.position, cano.forward, out var hit, arma.maxHitDistance, ~0, QueryTriggerInteraction.Ignore);
        var alvo = acertou ? hit.collider.GetComponentInParent<AlvoTreinamento>() : null;
        // Um único sistema de efeitos (clarão, traçante e impacto); o impacto também marca os erros
        if (efeitos != null) efeitos.Disparo(arma, acertou, hit, alvo);
    }

    void AoAcertar(AutoGun arma, RaycastHit hit)
    {
        var alvo = hit.collider.GetComponentInParent<AlvoTreinamento>();
        if (alvo == null || !alvo.RegistrarAcerto(hit)) return;
        Vibrar(arma, vibracaoAcerto);

        if (alvo.proibido) AcertosProibidos++;
        else if (alvo.tipo == TipoAlvo.Veiculo) AcertosVeiculos++;
        else AcertosPessoas++;
        Debug.Log($"[Treinamento] Acerto em {alvo.name} ({alvo.tipo}{(alvo.proibido ? ", não autorizado" : "")}) a {hit.distance:F0} m, alvo a {alvo.Velocidade.magnitude * 3.6f:F0} km/h.");
    }

    // Vibra o controle da mão que segura o fuzil (sem óculos, ninguém segura e não faz nada)
    static void Vibrar(AutoGun arma, Vector2 pulso)
    {
        var pegavel = arma.GetComponentInParent<Grabbable>();
        if (pegavel != null && pegavel.IsHeld()) pegavel.PlayHapticVibration(pulso.x, pulso.y);
    }


    void OnGUI()
    {
        if (!mostrarPlacar) return;
        string proibidos = AcertosProibidos > 0 ? $" | NÃO AUTORIZADOS: {AcertosProibidos}" : "";
        GUI.Label(new Rect(10, 75, Screen.width - 20, 22),
            $"Alvos móveis: {AcertosVeiculos + AcertosPessoas} acertos (veículos {AcertosVeiculos}, pessoas {AcertosPessoas}) | disparos: {Disparos}{proibidos}");
    }
}
