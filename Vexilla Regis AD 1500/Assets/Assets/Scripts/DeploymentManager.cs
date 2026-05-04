using UnityEngine;

public class DeploymentManager : MonoBehaviour
{
    [SerializeField] private GridManager grid;
    [SerializeField] private int deploymentRows = 3;
    [SerializeField] private bool deploymentActive = true;
    [SerializeField] private Transform boundaryLine;
    [SerializeField] private float boundaryThickness = 0.25f;

    public bool DeploymentActive => deploymentActive;

    private void Awake()
    {
        if (grid == null)
            grid = FindObjectOfType<GridManager>();
    }

    private void Start()
    {
        UpdateBoundaryLine();
        SetBoundaryVisibility(deploymentActive);
    }

    private void Update()
    {
        if (!deploymentActive) return;

        if (Input.GetKeyDown(KeyCode.Return))
            FinishDeployment();
    }

    public void FinishDeployment()
    {
        if (!deploymentActive)
            return;

        deploymentActive = false;
        SetBoundaryVisibility(false);

        Debug.Log("Deployment finished");
    }

    public bool IsInsideDeploymentZone(Vector2Int gridPos)
    {
        if (grid == null) return false;
        if (!grid.IsInside(gridPos)) return false;

        return gridPos.y < deploymentRows;
    }

    private void UpdateBoundaryLine()
    {
        if (grid == null || boundaryLine == null) return;

        float x = (grid.Width - 1) * 0.5f;
        float y = deploymentRows - 0.5f;

        boundaryLine.position = new Vector3(x, y, -0.1f);
        boundaryLine.localScale = new Vector3(grid.Width, boundaryThickness, 1f);
    }

    private void SetBoundaryVisibility(bool visible)
    {
        if (boundaryLine == null) return;

        boundaryLine.gameObject.SetActive(visible);
    }
}