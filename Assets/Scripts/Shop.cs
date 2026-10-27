using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Shop : MonoBehaviour
{
    public XRInteractionManager XRInteractionManager;
    public PocketSocket[] PocketSockets;

    [ContextMenu("GetComponents")]
    public void GetComponents()
    {
        XRInteractionManager = FindAnyObjectByType<XRInteractionManager>();
    }

    public void BuyGun(GunManager pGunManager)
    {
        UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor temp = GetAvailablePocket();
        XRInteractionManager.SelectEnter(temp, Instantiate(pGunManager, temp.transform.position, temp.transform.rotation).GrabInteractable);
    }

    public UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor GetAvailablePocket()
    {
        foreach (var pocket in PocketSockets)
        {
            if (pocket.interactablesSelected.Count <= 0)
            {
                return pocket;
            }
            
        }
        return null;
    }
}
