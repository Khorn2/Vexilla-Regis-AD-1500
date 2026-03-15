using UnityEngine;

public class Tile : MonoBehaviour
{
    [SerializeField] private Color _baseColor, _offsetColor;
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private GameObject _highlight;

    private Color _originalColor;

    public void Init(bool isOffset)
    {
        _renderer.color = isOffset ? _offsetColor : _baseColor;
        _originalColor = _renderer.color;
    }

    void OnMouseEnter()
    {
        if (_highlight != null)
            _highlight.SetActive(true);
    }

    void OnMouseExit()
    {
        if (_highlight != null)
            _highlight.SetActive(false);
    }

    public void SetRangeHighlight(bool active)
    {
        if (active)
            _renderer.color = Color.Lerp(_originalColor, Color.yellow, 0.35f);
        else
            _renderer.color = _originalColor;
    }
}
