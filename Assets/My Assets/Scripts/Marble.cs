using UnityEngine;

public class Marble : MonoBehaviour
{
    private Collider _collider;
    private Rigidbody _rb;
    private Collider _other1;
    private Color _color;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _rb = GetComponent<Rigidbody>();
        _color = GetComponent<MeshRenderer>().material.color;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BeltSpot") && other.transform.childCount == 0)
        {
            _other1 = other;
            _collider.enabled = false;
            _rb.isKinematic = true;

            transform.parent = other.transform;

            LeanTween.moveLocal(this.gameObject, Vector3.zero, 0.2f).setOnComplete(() =>
            {
                other.transform.GetComponent<MeshRenderer>().enabled = false;
                _collider.enabled = true;
            });

            return;
        }

        var Container = other.transform.parent.GetComponent<Container>();
        if (other.CompareTag("ContainerSpot") && Container.ContainerColor == _color && other.transform.childCount == 0)
        {
            _other1.transform.GetComponent<MeshRenderer>().enabled = true;
            _collider.enabled = false;
            _rb.isKinematic = true;

            transform.parent = other.transform;

            Container.AddMarble();
            LeanTween.moveLocal(this.gameObject, Vector3.zero, 0.2f).setOnComplete(() =>
            {
                other.transform.GetComponent<MeshRenderer>().enabled = false;
            });
        }
    }
}
