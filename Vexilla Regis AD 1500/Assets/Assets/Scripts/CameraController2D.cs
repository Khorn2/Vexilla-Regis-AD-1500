using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Camera))]
public class CameraController2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float moveSpeedBoost = 2.0f;
    [SerializeField] private bool disableWasdWhileDragging = true;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 20f;

    [Header("Clamp")]
    [SerializeField] private bool useClamp = true;
    [SerializeField] private Vector2 worldMin = new Vector2(0, 0);
    [SerializeField] private Vector2 worldMax = new Vector2(50, 50);

    [Header("Mouse Drag")]
    [SerializeField] private float dragSpeed = 1.0f;
    [SerializeField] private bool invertDrag = false;

    private Camera cam;
    private bool isDragging;
    private Vector3 dragStartWorld;

    public bool DragEnabled { get; set; } = true;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
    }

    private void Update()
    {
        HandleRmbDrag();
        HandleMove();
        HandleZoom();
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void HandleMove()
    {
        if (disableWasdWhileDragging && isDragging)
            return;

        float x = 0f;
        float y = 0f;

        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;

        Vector3 dir = new Vector3(x, y, 0f);
        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? moveSpeedBoost : 1f);
        transform.position += dir * speed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scroll, 0f))
            return;

        if (IsPointerOverUI())
            return;

        float target = cam.orthographicSize - scroll * zoomSpeed * Time.deltaTime * 10f;
        cam.orthographicSize = Mathf.Clamp(target, minOrthoSize, maxOrthoSize);
    }

    private void HandleRmbDrag()
    {
        if (!DragEnabled)
        {
            isDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (IsPointerOverUI())
                return;

            isDragging = true;
            dragStartWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            return;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
            return;
        }

        if (!isDragging)
            return;

        Vector3 currentWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 delta = dragStartWorld - currentWorld;

        if (invertDrag)
            delta = -delta;

        transform.position += delta * dragSpeed;
        dragStartWorld = cam.ScreenToWorldPoint(Input.mousePosition);
    }

    private void LateUpdate()
    {
        if (!useClamp)
            return;

        float maxFit = GetMaxOrthoSizeThatFitsBounds();
        float effectiveMax = Mathf.Min(maxOrthoSize, maxFit);
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minOrthoSize, effectiveMax);

        ClampToBounds();
    }

    private float GetMaxOrthoSizeThatFitsBounds()
    {
        float mapWidth = worldMax.x - worldMin.x;
        float mapHeight = worldMax.y - worldMin.y;

        float maxByHeight = mapHeight * 0.5f;
        float maxByWidth = (mapWidth * 0.5f) / cam.aspect;

        return Mathf.Min(maxByHeight, maxByWidth);
    }

    private void ClampToBounds()
    {
        float halfH = cam.orthographicSize;
        float halfW = cam.orthographicSize * cam.aspect;

        Vector3 p = transform.position;

        float minX = worldMin.x + halfW;
        float maxX = worldMax.x - halfW;
        float minY = worldMin.y + halfH;
        float maxY = worldMax.y - halfH;

        if (minX > maxX)
            p.x = (worldMin.x + worldMax.x) * 0.5f;
        else
            p.x = Mathf.Clamp(p.x, minX, maxX);

        if (minY > maxY)
            p.y = (worldMin.y + worldMax.y) * 0.5f;
        else
            p.y = Mathf.Clamp(p.y, minY, maxY);

        transform.position = p;
    }

    public void SetBounds(Vector2 min, Vector2 max)
    {
        worldMin = min;
        worldMax = max;
    }
}