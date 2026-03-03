using UnityEngine;

public class GameUnit : MonoBehaviour
{
    [SerializeField] private GameObject selectionRing;

    public bool IsSelected { get; private set; }

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
}
