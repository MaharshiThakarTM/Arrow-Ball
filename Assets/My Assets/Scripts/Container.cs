using UnityEngine;

public class Container : MonoBehaviour
{
    public bool IsFull;
    public Color ContainerColor;

    private int _availableSpots;

    private void Awake()
    {
        _availableSpots = transform.childCount - 1;
        transform.GetChild(0).GetComponent<MeshRenderer>().material.color = ContainerColor;
    }

    public void AddMarble()
    {
        _availableSpots--;
        if (_availableSpots <= 0)
        {
            IsFull = true;
            transform.parent.GetComponent<ContainerHolder>().OnContainerFull();

            LeanTween.scale(transform.gameObject, Vector3.zero, 0.2f).setOnComplete(() =>
            {
                transform.gameObject.SetActive(false);
            });
        }
    }
}
