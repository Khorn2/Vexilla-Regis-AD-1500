using UnityEngine;

public class SelectionManager2D : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask unitMask;
    [SerializeField] private GridManager grid;
    [SerializeField] private DeploymentManager deploymentManager;
    [SerializeField] private CameraController2D cameraController;

    public GameUnit Selected => selected;

    private GameUnit selected;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (grid == null) grid = FindObjectOfType<GridManager>();
        if (deploymentManager == null) deploymentManager = FindObjectOfType<DeploymentManager>();
        if (cameraController == null) cameraController = FindObjectOfType<CameraController2D>();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            HandleLeftClick_SelectUnit();

        if (Input.GetMouseButtonDown(1))
            HandleRightClick_Action();

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
            if (unit != null && unit.TeamId == 0)
                break;
        }

        if (unit == null)
        {
            ClearSelection();
            return;
        }

        Select(unit);
    }

    private void HandleRightClick_Action()
    {
        if (selected == null || grid == null) return;

        GameUnit clickedEnemy = GetEnemyUnderMouse();

        if (clickedEnemy != null)
        {
            HandleEnemyClick(clickedEnemy);
            return;
        }

        HandleGroundClick();
    }

    private void HandleEnemyClick(GameUnit enemy)
    {
        if (selected == null || enemy == null) return;

        if (selected.CurrentOrder == OrderType.Shoot)
        {
            selected.Shoot(enemy);
            return;
        }

        // March / Charge
        if (selected.IsAdjacentTo(enemy))
        {
            selected.AttackMelee(enemy);
            return;
        }

        if (grid.TryGetFreeAdjacentTile(enemy.GridPosition, selected.GridPosition, out Vector2Int attackTile))
        {
            selected.MoveToGrid(attackTile, () =>
            {
                if (selected != null && enemy != null)
                    selected.AttackMelee(enemy);
            });
        }
    }

    private void HandleGroundClick()
    {
        if (selected == null || grid == null) return;

        if (selected.CurrentOrder == OrderType.Shoot)
            return; // Shoot blokuje ruch

        Vector3 mp = Input.mousePosition;
        mp.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mp);

        Vector2Int target = grid.WorldToGrid(world);
        if (!grid.IsInside(target)) return;

        // deployment = teleport
        if (deploymentManager != null && deploymentManager.DeploymentActive)
        {
            if (!deploymentManager.IsInsideDeploymentZone(target)) return;
            if (grid.IsOccupied(target)) return;

            selected.SnapToGrid(target);
            return;
        }

        // normalny ruch
        if (grid.IsOccupied(target)) return;

        selected.MoveToGrid(target);
    }

    private GameUnit GetEnemyUnderMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray, Mathf.Infinity, unitMask);

        for (int i = 0; i < hits.Length; i++)
        {
            GameUnit unit = hits[i].collider.GetComponentInParent<GameUnit>();

            if (unit != null && selected != null && unit.TeamId != selected.TeamId)
                return unit;
        }

        return null;
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
        if (selected != null)
        {
            selected.SetSelected(false);
            selected = null;
        }

        if (cameraController != null)
            cameraController.DragEnabled = true;
    }
}