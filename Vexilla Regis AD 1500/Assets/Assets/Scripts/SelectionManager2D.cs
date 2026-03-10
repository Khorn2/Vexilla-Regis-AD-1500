using UnityEngine;

public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitMask;
    [SerializeField] private GridManager grid;
    [SerializeField] private CameraController2D cameraController;
    [SerializeField] private DeploymentManager deploymentManager;

    public GameUnit Selected => selected;

    private GameUnit selected;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindObjectOfType<GridManager>();
        if (cameraController == null) cameraController = FindObjectOfType<CameraController2D>();
        if (deploymentManager == null) deploymentManager = FindObjectOfType<DeploymentManager>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleLeftClick_SelectUnit();

        if (Input.GetMouseButtonDown(1))
            HandleRightClick_Move();

            HandleOrderInput();
    }

    private void HandleLeftClick_SelectUnit()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, unitMask);

        GameUnit unit = null;

        for (int i = 0; i < hits.Length; i++)
        {
            unit = hits[i].collider.GetComponentInParent<GameUnit>();
            if (unit != null)
                break;
        }

        if (unit == null)
        {
            ClearSelection();
            return;
        }

        Select(unit);
    }

    private void HandleRightClick_Move()
    {
    if (selected == null || grid == null) return;

    Vector3 mp = Input.mousePosition;
    mp.z = -cam.transform.position.z;
    Vector3 world = cam.ScreenToWorldPoint(mp);

    Vector2Int target = grid.WorldToGrid(world);
    if (!grid.IsInside(target)) return;

    // Deployment = teleport tylko w strefie
    if (deploymentManager != null && deploymentManager.DeploymentActive)
    {
        if (!deploymentManager.IsInsideDeploymentZone(target)) return;

        selected.SnapToGrid(target);
        return;
    }

    // Normalna gra = płynny ruch
    if (selected.IsMoving) return;
    selected.MoveToGrid(target);
    }

    private void Select(GameUnit unit)
    {
        if (selected == unit) return;

        ClearSelection();
        selected = unit;
        selected.SetSelected(true);
        if (cameraController != null)
        cameraController.DragEnabled = false;
    }

    private void ClearSelection()
    {
        if (selected == null) return;

        selected.SetSelected(false);
        selected = null;
        if (cameraController != null)
        cameraController.DragEnabled = true;
    }

    private void HandleOrderInput()
    {
    if (selected == null) return;

    if (Input.GetKeyDown(KeyCode.Alpha1))
        selected.SetOrder(OrderType.March);

    if (Input.GetKeyDown(KeyCode.Alpha2))
        selected.SetOrder(OrderType.Charge);

    if (Input.GetKeyDown(KeyCode.Alpha3))
        selected.SetOrder(OrderType.Shoot);
    }
}