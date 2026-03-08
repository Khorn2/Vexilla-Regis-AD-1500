using System.Collections;
using UnityEngine;

public class GameUnit : MonoBehaviour
{
    [SerializeField] private GameObject selectionRing;

    [Header("Movement")]
    [SerializeField, Min(0.1f)] private float moveSpeedTilesPerSec = 4f; // fallback
    [SerializeField] private UnitStats stats;

    public bool IsSelected { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public bool IsMoving => _moveRoutine != null;

    private Coroutine _moveRoutine;

    private float CurrentMoveSpeed
    {
        get
        {
            if (stats != null)
                return Mathf.Max(0.1f, stats.moveSpeedTilesPerSec);

            return Mathf.Max(0.1f, moveSpeedTilesPerSec);
        }
    }

    private void Awake()
    {
        if (selectionRing != null)
            selectionRing.SetActive(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;

        if (selectionRing != null)
            selectionRing.SetActive(selected);
    }

    public void SnapToGrid(Vector2Int gridPos)
    {
        GridPosition = gridPos;
        transform.position = new Vector3(gridPos.x, gridPos.y, 0f);
    }

    public void MoveToGrid(Vector2Int targetGrid)
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        _moveRoutine = StartCoroutine(MoveRoutine(targetGrid));
    }

    private IEnumerator MoveRoutine(Vector2Int targetGrid)
    {
        Vector3 start = transform.position;
        Vector3 end = new Vector3(targetGrid.x, targetGrid.y, 0f);

        float distanceTiles = Vector3.Distance(start, end);

        if (distanceTiles <= 0.001f)
        {
            GridPosition = targetGrid;
            _moveRoutine = null;
            yield break;
        }

        float duration = distanceTiles / CurrentMoveSpeed;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = end;
        GridPosition = targetGrid;
        _moveRoutine = null;
    }
}