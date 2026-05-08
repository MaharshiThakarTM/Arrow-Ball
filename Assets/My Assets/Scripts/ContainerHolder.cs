using System.Collections.Generic;
using UnityEngine;

public class ContainerHolder : MonoBehaviour
{
    private List<Container> _containers = new List<Container>();

    private void Awake()
    {
        foreach (Transform child in transform)
        {
            _containers.Add(child.GetComponent<Container>());
        }
    }

    public void OnContainerFull()
    {
        var count = 0;
        for (int i = 0; i < _containers.Count; i++)
        {
            if (_containers[i].IsFull)
            {
                count++;
                continue;
            }

            LeanTween.moveLocal(_containers[i].gameObject, _containers[i - 1].transform.localPosition, 0.2f);
        }
        if(count == _containers.Count)
        {
            transform.parent.GetComponent<ContainerController>().OnContainerHolderEmpty();
        }
    }
}
