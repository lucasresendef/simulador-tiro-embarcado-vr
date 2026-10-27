using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class MagazineSpawner : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable currentMagazine;
    [ReadOnlyInspector] public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable XRSimpleInteractable;
    [ReadOnlyInspector] public XRInteractionManager XRInteractionManager;

    [ContextMenu("GetComponents")]
    public void GetComponents()
    {
        XRSimpleInteractable = GetComponent<XRSimpleInteractable>();
        XRInteractionManager = FindAnyObjectByType<XRInteractionManager>();
    }

    private void OnEnable()
    {
        XRSimpleInteractable.selectEntered.AddListener(MagazineSpawnerGrabbed);
    }
    private void OnDisable()
    {
        XRSimpleInteractable.selectEntered.RemoveListener(MagazineSpawnerGrabbed);
    }

    private void MagazineSpawnerGrabbed(SelectEnterEventArgs arg0)
    {
        if (currentMagazine == null) return;
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable magazine = Instantiate(currentMagazine,transform.position,transform.rotation);
        XRInteractionManager.SelectEnter(arg0.interactorObject, magazine);
    }
}
