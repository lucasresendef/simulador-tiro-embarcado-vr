using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PocketSocket : UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor
{
    
    public override UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType? selectedInteractableMovementTypeOverride => UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
}
