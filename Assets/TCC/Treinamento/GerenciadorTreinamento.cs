using Autohand;
using UnityEngine;

// Conta disparos e acertos nos alvos móveis. Escuta os eventos de todos os AutoGun da cena,
// então funciona igual no VR e no modo desktop.
public class GerenciadorTreinamento : MonoBehaviour
{
    public static GerenciadorTreinamento Instancia { get; private set; }

    public int Disparos { get; private set; }
    public int AcertosVeiculos { get; private set; }
    public int AcertosPessoas { get; private set; }

    void Awake() => Instancia = this;

    void Start()
    {
        foreach (var arma in FindObjectsByType<AutoGun>(FindObjectsSortMode.None))
        {
            arma.OnShoot.AddListener(_ => Disparos++);
            arma.OnHitEvent.AddListener(AoAcertar);
        }
    }

    void AoAcertar(AutoGun arma, RaycastHit hit)
    {
        var alvo = hit.collider.GetComponentInParent<AlvoTreinamento>();
        if (alvo == null || !alvo.RegistrarAcerto(hit)) return;

        if (alvo.tipo == TipoAlvo.Veiculo) AcertosVeiculos++;
        else AcertosPessoas++;
        Debug.Log($"[Treinamento] Acerto em {alvo.name} ({alvo.tipo}) a {hit.distance:F0} m, alvo a {alvo.Velocidade.magnitude * 3.6f:F0} km/h.");
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 75, Screen.width - 20, 22),
            $"Alvos móveis: {AcertosVeiculos + AcertosPessoas} acertos (veículos {AcertosVeiculos}, pessoas {AcertosPessoas}) | disparos: {Disparos}");
    }
}
