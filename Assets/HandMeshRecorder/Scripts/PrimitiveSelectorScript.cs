using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrimitiveSelectorScript : MonoBehaviour
{
    public GameObject primitiveCollection = null;
    public GameObject leftScrollButton = null;
    public GameObject rightScrollButton = null;
    int numPrimitives = 0;
    int currPrimPointer = 0;
    Color originalColor;

    [NonSerialized]
    public GameObject handRecorderContainer;
    [NonSerialized]
    public GameObject handRecorder;

    void Start()
    {
        numPrimitives = primitiveCollection.transform.childCount;
        originalColor = primitiveCollection.transform.GetChild(0).GetComponent<Renderer>().material.color;
        if (numPrimitives > 1)
        {
            // Set all primitives to be inactive except the first one
            for (int i = 1; i < numPrimitives; i++)
            {
                primitiveCollection.transform.GetChild(i).gameObject.SetActive(false);
            }
        }
        // scroll functionality
        if (leftScrollButton != null)
        {
            leftScrollButton.GetComponent<Interactable>().OnClick.AddListener(() => ScrollGameObject(false));
        }
        if (rightScrollButton != null)
        {
            rightScrollButton.GetComponent<Interactable>().OnClick.AddListener(() => ScrollGameObject(true));
        }

        // primitive GameObject selection functionality
        // clicking the gameobject selects that
        foreach (Transform child in primitiveCollection.transform)
        {
            FocusHandler focusHandler = child.gameObject.AddComponent<FocusHandler>();
            focusHandler.OnFocusEnterEvent.AddListener(() => { child.gameObject.GetComponent<Renderer>().material.color = Color.yellow; });
            focusHandler.OnFocusExitEvent.AddListener(() => { child.gameObject.GetComponent<Renderer>().material.color = originalColor; });
            PointerHandler pointerHandler = child.gameObject.AddComponent<PointerHandler>();
            pointerHandler.OnPointerClicked.AddListener((evt) => PrimitiveSelected(child.gameObject));
        }
    }

    private void PrimitiveSelected(GameObject goSelected)
    {
        // clone the GO selected, delete the whole primitive selector component
        GameObject clone = Instantiate(goSelected);
        clone.GetComponent<Renderer>().material.color = originalColor;
        clone.transform.parent = handRecorderContainer.transform;
        if (handRecorder.GetComponent<HandRecordingScript>().gameobjectToRecord != null)
        {
            Destroy(handRecorder.GetComponent<HandRecordingScript>().gameobjectToRecord);
        }
        handRecorder.GetComponent<HandRecordingScript>().gameobjectToRecord = clone;
        Destroy(clone.GetComponent<PrimitiveSelectorScript>());
        Destroy(clone.GetComponent<FocusHandler>());
        Destroy(clone.GetComponent<PointerHandler>());


        BoundingBox cloneBoundingBox = clone.AddComponent<BoundingBox>();
        cloneBoundingBox.BoundingBoxActivation = BoundingBox.BoundingBoxActivationType.ActivateByProximityAndPointer;
        cloneBoundingBox.ShowWireFrame = false;

        // ManipulationHandler Component
        ManipulationHandler cloneManipulationHandler = clone.AddComponent<ManipulationHandler>();
        cloneManipulationHandler.ManipulationType = ManipulationHandler.HandMovementType.OneHandedOnly;
        cloneManipulationHandler.OneHandRotationModeFar = ManipulationHandler.RotateInOneHandType.MaintainOriginalRotation;
        cloneManipulationHandler.OneHandRotationModeNear = ManipulationHandler.RotateInOneHandType.MaintainOriginalRotation;
        clone.AddComponent<NearInteractionGrabbable>();

        handRecorderContainer.SetActive(true);
        GameObject primitiveSelectorContainer = gameObject.transform.parent.gameObject;
        primitiveSelectorContainer.SetActive(false);
    }

    private void ScrollGameObject(bool isScrollRight)
    {
        primitiveCollection.transform.GetChild(currPrimPointer).gameObject.SetActive(false);
        if (isScrollRight) { currPrimPointer = (currPrimPointer + 1) % numPrimitives; }
        else { currPrimPointer -= 1; }
        if (currPrimPointer == -1) { currPrimPointer = numPrimitives-1; }
        primitiveCollection.transform.GetChild(currPrimPointer).gameObject.SetActive(true);
    }
}
