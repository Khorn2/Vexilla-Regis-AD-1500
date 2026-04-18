using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private GameObject _highlight;
    [SerializeField] private Sprite _terrainSpriteOverride;

    [Header("Terrain")]
    [SerializeField] private TerrainType _terrainType = TerrainType.Plain;
    [SerializeField, Range(1, 6)] private int _heightLevel = 1;

    [Header("Base Colors")]
    [SerializeField] private Color _plainBaseColor = new Color(0.63f, 0.85f, 0.45f);
    [SerializeField] private Color _plainOffsetColor = new Color(0.58f, 0.80f, 0.40f);

    [Header("Terrain Debug Colors")]
    [SerializeField] private Color _forestColor = new Color(0.16f, 0.42f, 0.18f);
    [SerializeField] private Color _shallowWaterColor = new Color(0.45f, 0.78f, 0.95f);
    [SerializeField] private Color _deepWaterColor = new Color(0.10f, 0.26f, 0.60f);
    [SerializeField] private Color _roadColor = new Color(0.76f, 0.68f, 0.40f);
    [SerializeField] private Color _roughTerrainColor = new Color(0.48f, 0.50f, 0.30f);

    [Header("Height Tint")]
    [SerializeField, Range(0f, 1f)] private float _offsetTintStrength = 0.06f;
    [SerializeField, Range(0f, 1f)] private float _heightDarkenPerLevel = 0.12f;
    [SerializeField, Range(0f, 1f)] private float _heightDesaturatePerLevel = 0.18f;

    private bool _isOffset;
    private Color _originalColor;

    public TerrainType TerrainType => _terrainType;
    public int HeightLevel => _heightLevel;
    public Sprite TerrainSpriteOverride => _terrainSpriteOverride;

    public void Init(bool isOffset)
    {
        _isOffset = isOffset;
        RefreshVisual();
    }

    private void OnMouseEnter()
    {
        if (_highlight != null)
            _highlight.SetActive(true);
    }

    private void OnMouseExit()
    {
        if (_highlight != null)
            _highlight.SetActive(false);
    }

    public void SetRangeHighlight(bool active)
    {
        if (_renderer == null)
            return;

        if (active)
            _renderer.color = Color.Lerp(_originalColor, Color.yellow, 0.35f);
        else
            _renderer.color = _originalColor;
    }

    public void SetTerrain(TerrainType terrainType, int heightLevel)
    {
        _terrainType = terrainType;
        _heightLevel = Mathf.Clamp(heightLevel, 1, 6);
        RefreshVisual();
    }

    public void SetTerrainSprite(Sprite spriteOverride)
    {
        _terrainSpriteOverride = spriteOverride;
        RefreshVisual();
    }

    public bool IsWalkable()
    {
        return _terrainType != TerrainType.DeepWater;
    }

    public int GetBaseMovementCost()
    {
        switch (_terrainType)
        {
            case TerrainType.Forest:
                return 2;

            case TerrainType.ShallowWater:
                return 2;

            case TerrainType.RoughTerrain:
                return 2;

            case TerrainType.DeepWater:
                return int.MaxValue;

            case TerrainType.Road:
                return 0;

            case TerrainType.Plain:
            default:
                return 1;
        }
    }

    public int GetShootRangeBonus()
    {
        if (_heightLevel >= 6) return 3;
        if (_heightLevel >= 4) return 2;
        if (_heightLevel >= 2) return 1;
        return 0;
    }

    public bool ReducesMusketDamage()
    {
        return _terrainType == TerrainType.Forest;
    }

    private void RefreshVisual()
    {
        if (_renderer == null)
            return;

        if (_terrainSpriteOverride != null)
            _renderer.sprite = _terrainSpriteOverride;

        Color targetColor = GetDebugColor();
        _renderer.color = targetColor;
        _originalColor = targetColor;
    }

    private Color GetDebugColor()
    {
        Color color;

        switch (_terrainType)
        {
            case TerrainType.Forest:
                color = _forestColor;
                break;

            case TerrainType.ShallowWater:
                color = _shallowWaterColor;
                break;

            case TerrainType.DeepWater:
                color = _deepWaterColor;
                break;

            case TerrainType.Road:
                color = _roadColor;
                break;

            case TerrainType.RoughTerrain:
                color = _roughTerrainColor;
                break;

            case TerrainType.Plain:
            default:
                color = _isOffset ? _plainOffsetColor : _plainBaseColor;
                color = ApplyHeightTint(color, _heightLevel);
                break;
        }

        if (_terrainType != TerrainType.Plain && _isOffset)
            color = Color.Lerp(color, Color.black, _offsetTintStrength);

        return color;
    }

    private Color ApplyHeightTint(Color baseColor, int heightLevel)
    {
        if (heightLevel <= 1)
            return baseColor;

        int extraLevels = heightLevel - 1;

        float luminance = (baseColor.r + baseColor.g + baseColor.b) / 3f;
        Color grayscale = new Color(luminance, luminance, luminance, baseColor.a);

        float desaturateAmount = Mathf.Clamp01(extraLevels * _heightDesaturatePerLevel);
        float darkenAmount = Mathf.Clamp01(extraLevels * _heightDarkenPerLevel);

        Color mixed = Color.Lerp(baseColor, grayscale, desaturateAmount);
        mixed = Color.Lerp(mixed, Color.black, darkenAmount);

        return mixed;
    }
}