using System.Collections.Generic;
using UnityEngine;

public class RotatingBelt : MonoBehaviour
{
    [Header("Belt Shape")]
    [Tooltip("Half the total width (must be >= RadiusY)")]
    [SerializeField] private float _radiusX = 3f;
    [Tooltip("Half the height — also the radius of the rounded end caps")]
    [SerializeField] private float _radiusY = 1f;

    [Header("Movement")]
    [SerializeField] private float _speed = 1f;

    [Header("Items")]
    [SerializeField] private List<Transform> _items;

    private float _offset = 0f;

    private float StraightHalfLen => Mathf.Max(0f, _radiusX - _radiusY);
    private float StraightLen => 2f * StraightHalfLen;
    private float SemiCircLen => Mathf.PI * _radiusY;
    private float TotalPerimeter => 2f * StraightLen + 2f * SemiCircLen;

    private void Update()
    {
        if (_items == null || _items.Count == 0) return;

        float perimeter = TotalPerimeter;

        _offset = (_offset + _speed * Time.deltaTime) % perimeter;

        float spacing = perimeter / _items.Count;

        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i] == null) continue;

            float s = (_offset + spacing * i) % perimeter;

            _items[i].position = transform.TransformPoint(PointAtArc(s));

            Vector3 worldTangent = transform.TransformDirection(TangentAtArc(s));
            float zRot = Mathf.Atan2(worldTangent.y, worldTangent.x) * Mathf.Rad2Deg;
            _items[i].rotation = Quaternion.Euler(0f, 0f, zRot - 90f);
        }
    }

    Vector3 PointAtArc(float s)
    {
        float shl = StraightHalfLen;
        float sl = StraightLen;
        float sc = SemiCircLen;

        if (s < sl)
            return new Vector3(-shl + s, -_radiusY, 0f);

        s -= sl;

        if (s < sc)
        {
            float a = -Mathf.PI * 0.5f + s / _radiusY;
            return new Vector3(
                shl + _radiusY * Mathf.Cos(a),
                      _radiusY * Mathf.Sin(a),
                0f);
        }

        s -= sc;

        if (s < sl)
            return new Vector3(shl - s, _radiusY, 0f);

        s -= sl;

        {
            float a = Mathf.PI * 0.5f + s / _radiusY;
            return new Vector3(
                -shl + _radiusY * Mathf.Cos(a),
                       _radiusY * Mathf.Sin(a),
                0f);
        }
    }

    Vector3 TangentAtArc(float s)
    {
        float sl = StraightLen;
        float sc = SemiCircLen;

        if (s < sl)
            return Vector3.right;

        s -= sl;

        if (s < sc)
        {
            float a = -Mathf.PI * 0.5f + s / _radiusY;
            return new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
        }

        s -= sc;

        if (s < sl)
            return Vector3.left;

        s -= sl;

        {
            float a = Mathf.PI * 0.5f + s / _radiusY;
            return new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        int samples = 128;
        float perimeter = TotalPerimeter;

        for (int i = 0; i < samples; i++)
        {
            float s1 = perimeter * i / samples;
            float s2 = perimeter * (i + 1) / samples;

            Gizmos.DrawLine(
                transform.TransformPoint(PointAtArc(s1)),
                transform.TransformPoint(PointAtArc(s2)));
        }

        if (_items == null || _items.Count == 0) return;

        Gizmos.color = Color.cyan;
        float spacing = perimeter / _items.Count;

        for (int i = 0; i < _items.Count; i++)
        {
            float s = spacing * i;
            Gizmos.DrawWireSphere(transform.TransformPoint(PointAtArc(s)), 0.08f);
        }
    }
}