using UnityEngine;
using UnityEngine.EventSystems;

public class ProfileShipSelectionSwipeHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
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
        owner?.BeginShipSelectionDrag(eventData, hasPointerDownPosition ? pointerDownPosition : eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.UpdateShipSelectionDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.EndShipSelectionDrag(eventData);
        hasPointerDownPosition = false;
    }
}
