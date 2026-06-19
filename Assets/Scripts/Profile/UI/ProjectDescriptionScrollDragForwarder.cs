using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProjectDescriptionScrollDragForwarder : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public ScrollRect scrollRect;

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        scrollRect?.OnInitializePotentialDrag(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        scrollRect?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        scrollRect?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        scrollRect?.OnEndDrag(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        scrollRect?.OnScroll(eventData);
    }
}
