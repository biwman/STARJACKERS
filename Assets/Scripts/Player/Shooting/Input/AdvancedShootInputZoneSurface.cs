using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Photon.Pun;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public sealed class AdvancedShootInputZoneSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public AdvancedShootInputZone Owner;

    public void OnPointerDown(PointerEventData eventData)
    {
        Owner?.HandlePointerDown(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Owner?.HandleDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Owner?.HandlePointerUp(eventData);
    }
}
