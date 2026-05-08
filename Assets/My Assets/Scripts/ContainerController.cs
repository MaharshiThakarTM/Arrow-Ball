using System.Collections.Generic;
using UnityEngine;

public class ContainerController : MonoBehaviour
{
    public LevelController LevelController;

    private List<ContainerHolder> _containerHolders = new List<ContainerHolder>();
    private int count;

    private void Awake()
    {
        foreach (Transform child in transform)
        {
            _containerHolders.Add(child.GetComponent<ContainerHolder>());
        }
    }

    public void OnContainerHolderEmpty()
    {
        count++;
        if(count == _containerHolders.Count)
        {
            LevelController.OnLevelCompleted();
        }
    }
}
