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

    void Start()
    {
        StartCoroutine(GrabRoutine());
    }

    IEnumerator GrabRoutine()
    {
        // --- AQUI ESTÁ A MUDANÇA PRINCIPAL ---
        // Espera um pequeno tempo ANTES de fazer qualquer coisa.
        // Isso dá tempo para outros scripts (como o PlacePoint do carregador) inicializarem.
        yield return new WaitForSeconds(initialSetupDelay);

        // Pega com a mão primária (direita)
        if (primaryHand != null && primaryGrabbable != null)
            primaryHand.ForceGrab(primaryGrabbable);
        
        // Espera um pequeno instante entre as mãos
        yield return new WaitForSeconds(grabDelay);

        // Pega com a mão secundária (esquerda)
        if (secondaryHand != null && secondaryGrabbable != null)
            secondaryHand.ForceGrab(secondaryGrabbable);
    }
}