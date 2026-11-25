using UnityEngine;
using Autohand;
using System.Collections;

public class AdvancedInitialGrab : MonoBehaviour
{
    [Header("Referências da Arma")]
    public Grabbable primaryGrabbable;
    public Grabbable secondaryGrabbable;

    [Header("Referências das Mãos")]
    public Hand primaryHand;
    public Hand secondaryHand;

    [Header("Configurações de Atraso (Delay)")]
    [Tooltip("Delay inicial para garantir que a cena e os PlacePoints sejam carregados antes da pegada.")]
    public float initialSetupDelay = 0.1f; // Atraso inicial crucial!

    [Tooltip("Delay entre a pegada da mão primária e da secundária.")]
    public float grabDelay = 0.05f;

    [Tooltip("Por quanto tempo, depois do início da cena e de cada calibração, a pegada é conferida e refeita se a mão tiver soltado a arma.")]
    public float tempoGarantia = 10f;

    Coroutine garantia;

    // A calibração do operador (CalibracaoOperador) move o jogador logo depois do início da cena:
    // as mãos ficam longe da arma e o AutoHand a solta. Depois de cada calibração, a arma é
    // entregue de novo às mãos.
    void OnEnable()
    {
        CalibracaoOperador.Calibrado += AoCalibrar;
    }

    void OnDisable()
    {
        CalibracaoOperador.Calibrado -= AoCalibrar;
    }

    [Header("Resposta da mira (duas mãos)")]
    [Tooltip("Força com que as mãos seguem a posição dos controles enquanto seguram a arma (o AutoHand usa 60).")]
    public float forcaPosicaoMaos = 90f;
    [Tooltip("Força com que as mãos seguem a rotação dos controles enquanto seguram a arma (o AutoHand usa 110).")]
    public float forcaRotacaoMaos = 170f;
    [Tooltip("Velocidade máxima da arma ao acompanhar as mãos, em m/s (no prefab do fuzil: 8).")]
    public float velocidadeMaximaArma = 20f;

    [Header("Modo de empunhadura")]
    [Tooltip("Ligado: a arma fica só na mão principal (direita), que comanda a mira sozinha. " +
             "Desligado: as duas mãos seguram a arma (a esquerda na frente), como no projeto original.")]
    public bool somenteMaoPrincipal = false;

    [Tooltip("Ligado: a arma e as mãos são posicionadas a partir dos controles (mão direita = posição, linha entre as mãos = direção do cano), " +
             "em vez de cada mão puxar a arma pela física. Ver TCC/VR/MiraEstabilizada.cs.")]
    public bool miraEstabilizada = true;

    void Start()
    {
        if (somenteMaoPrincipal) UsarSomenteMaoPrincipal();
        ReforcarResposta();
        if (miraEstabilizada)
            gameObject.AddComponent<MiraEstabilizada>().Configurar(primaryHand, primaryGrabbable, secondaryHand, secondaryGrabbable);
        StartCoroutine(GrabRoutine());
    }

    // A mão secundária deixa de pegar a frente da arma, e os StableGripEnforcer (que forçam as duas
    // mãos a segurá-la) são desligados; a mão esquerda fica livre
    void UsarSomenteMaoPrincipal()
    {
        foreach (var forcador in FindObjectsByType<StableGripEnforcer>(FindObjectsInactive.Include))
            forcador.enabled = false;
        if (secondaryHand != null && secondaryGrabbable != null && secondaryHand.holdingObj == secondaryGrabbable)
            secondaryGrabbable.ForceHandRelease(secondaryHand);
        secondaryHand = null;
        Debug.Log("[TCC] Fuzil só na mão direita (AdvancedInitialGrab.somenteMaoPrincipal).");
    }

    // Deixa a arma acompanhar mais depressa o movimento dos dois controles, sem mudar o prefab
    void ReforcarResposta()
    {
        foreach (var mao in new[] { primaryHand, secondaryHand })
        {
            if (mao == null) continue;
            mao.followPositionStrength = Mathf.Max(mao.followPositionStrength, forcaPosicaoMaos);
            mao.followRotationStrength = Mathf.Max(mao.followRotationStrength, forcaRotacaoMaos);
        }
        foreach (var arma in new[] { primaryGrabbable, secondaryGrabbable })
        {
            if (arma == null) continue;
            arma.maxHeldVelocity = Mathf.Max(arma.maxHeldVelocity, velocidadeMaximaArma);
        }
    }

    IEnumerator GrabRoutine()
    {
        // --- AQUI ESTÁ A MUDANÇA PRINCIPAL ---
        // Espera um pequeno tempo ANTES de fazer qualquer coisa.
        // Isso dá tempo para outros scripts (como o PlacePoint do carregador) inicializarem.
        yield return new WaitForSeconds(initialSetupDelay);

        // Pega com a mão primária (direita)
        Pegar(primaryHand, primaryGrabbable);

        // Espera um pequeno instante entre as mãos
        yield return new WaitForSeconds(grabDelay);

        // Pega com a mão secundária (esquerda)
        Pegar(secondaryHand, secondaryGrabbable);

        IniciarGarantia();
    }

    void AoCalibrar()
    {
        if (isActiveAndEnabled) IniciarGarantia();
    }

    void IniciarGarantia()
    {
        if (garantia != null) StopCoroutine(garantia);
        garantia = StartCoroutine(GarantirPegada());
    }

    // Enquanto o rastreamento se estabiliza, refaz a pegada se a mão tiver soltado a arma
    IEnumerator GarantirPegada()
    {
        float fim = Time.time + tempoGarantia;
        while (Time.time < fim)
        {
            // Espera o movimento do jogador (calibração) chegar à física antes de pegar de novo
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            // Se a arma caiu longe, traz de volta para a mão principal antes de pegar: o ForceGrab
            // leva a mão até a arma, e uma mão longe do controle solta a arma de novo
            TrazerArmaParaMao(primaryHand, primaryGrabbable);
            yield return new WaitForFixedUpdate();
            Pegar(primaryHand, primaryGrabbable);
            Pegar(secondaryHand, secondaryGrabbable);
            yield return new WaitForSeconds(0.5f);
        }
        garantia = null;
    }

    // Distância entre a mão e o ponto de pegada acima da qual a arma é trazida até a mão
    const float DistanciaParaTrazer = 0.4f;

    static void TrazerArmaParaMao(Hand mao, Grabbable arma)
    {
        if (mao == null || arma == null || !mao.isActiveAndEnabled || !arma.isActiveAndEnabled) return;
        if (mao.holdingObj != null || arma.HeldCount(false, false, false) > 0) return;

        var palma = mao.palmTransform != null ? mao.palmTransform : mao.transform;
        var deslocamento = palma.position - arma.transform.position;
        if (deslocamento.magnitude < DistanciaParaTrazer) return;

        // Move a arma inteira (o corpo rígido que contém a peça de pegada)
        var corpo = arma.body;
        var raiz = corpo != null ? corpo.transform : arma.transform;
        raiz.position += deslocamento;
        if (corpo != null)
        {
            corpo.position = raiz.position;
            if (!corpo.isKinematic)
            {
                corpo.linearVelocity = Vector3.zero;
                corpo.angularVelocity = Vector3.zero;
            }
        }
        Physics.SyncTransforms();
    }

    void Pegar(Hand mao, Grabbable arma)
    {
        if (mao == null || arma == null || !mao.isActiveAndEnabled || !arma.isActiveAndEnabled) return;
        // Já está na mão, ou a mão segura outra coisa, ou a peça está em outra mão: não interfere
        if (mao.holdingObj != null || arma.HeldCount(false, false, false) > 0) return;
        // A mira estabilizada deixa a arma cinemática, e o AutoHand não pega objeto cinemático
        var mira = GetComponent<MiraEstabilizada>();
        if (mira != null) mira.Pausar();
        mao.ForceGrab(arma);
    }
}
