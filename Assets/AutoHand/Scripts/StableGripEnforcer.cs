using UnityEngine;
using Autohand;

public class StableGripEnforcer : MonoBehaviour
{
    [Header("Alvo Principal")]
    [Tooltip("A parte principal da arma que determina se ela está sendo segurada (ex: Handle)")]
    public Grabbable primaryGrabbable;

    [Header("Alvo a Forçar")]
    [Tooltip("A mão que deve permanecer conectada (ex: a mão esquerda)")]
    public Hand handToEnforce;
    
    [Tooltip("A parte da arma que essa mão deve segurar (ex: o Core)")]
    public Grabbable grabbableToEnforce;


    void Update()
    {
        // Garante que tudo está configurado
        if (primaryGrabbable != null && handToEnforce != null && grabbableToEnforce != null)
        {
            // CONDIÇÃO PRINCIPAL: A arma está sendo segurada pela mão primária?
            if (primaryGrabbable.IsHeld())
            {
                // Se sim, verificamos se a nossa mão secundária está segurando sua parte designada.
                if (handToEnforce.holdingObj != grabbableToEnforce)
                {
                    // Se não estiver, força a reconexão.
                    handToEnforce.TryGrab(grabbableToEnforce);
                }
            }
        }
    }
}