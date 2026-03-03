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
    if (cam == null) cam = Camera.main;

    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
    var hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity);

    Debug.Log($"hits={hits.Length}");
    for (int i = 0; i < hits.Length; i++)
    {
        Debug.Log($"  hit[{i}] name={hits[i].collider.name} layer={LayerMask.LayerToName(hits[i].collider.gameObject.layer)}");
    }

    // tymczasowo: wybierz pierwszy trafiony collider jako “unit”
    if (hits.Length == 0)
    {
        ClearSelection();
        return;
    }

    var unit = hits[0].collider.GetComponentInParent<GameUnit>();
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