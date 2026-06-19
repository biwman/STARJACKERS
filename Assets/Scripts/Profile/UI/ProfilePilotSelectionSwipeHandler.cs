using UnityEngine;
using UnityEngine.EventSystems;

public class ProfilePilotSelectionSwipeHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public PlayerProfilePanelUI owner;
    Vector2 pointerDownPosition;
    bool hasPointerDownPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
        hasPointerDownPosition = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginPilotSelectionDrag(eventData, hasPointerDownPosition ? pointerDownPosition : eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdatePilotSelectionDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndPilotSelectionDrag(eventData);
        hasPointerDownPosition = false;
    }
}
