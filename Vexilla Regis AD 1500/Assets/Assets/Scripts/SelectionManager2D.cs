using UnityEngine;

public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitMask;

    private GameUnit selected;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleLeftClick();
    }

    private void HandleLeftClick()
    {
    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
    var hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

    GameUnit unit = null;
    for (int i = 0; i < hits.Length; i++)
    {
        unit = hits[i].collider.GetComponentInParent<GameUnit>();
        if (unit != null) break;
    }

    if (unit == null)
    {
        ClearSelection();
        return;
    }

    Select(unit);
    }

    private void Select(GameUnit unit)
    {
        if (selected == unit) return;

        ClearSelection();
        selected = unit;
        selected.SetSelected(true);
    }

    private void ClearSelection()
    {
        if (selected == null) return;

        selected.SetSelected(false);
        selected = null;
    }
}