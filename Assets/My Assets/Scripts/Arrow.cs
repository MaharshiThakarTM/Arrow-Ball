using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class Arrow : MonoBehaviour
{
    public bool Release;
    public Color ArrowColor;

    [Header("Arrow Settings")]
    [SerializeField] private float _arrowLength = 1;
    [SerializeField] private float _arrowThickness = 1f;
    [SerializeField] private float _tailPartSpacing = 0.05f;
    [SerializeField] private float _rayDistance = 1f;
    [SerializeField] private LayerMask _layerMask;

    [Header("Checkpoints")]
    public List<Transform> checkpoints;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;

    [Header("Tail Settings")]
    public float pathRecordInterval = 0.05f;

    [Header("Head Rotation Offset")]
    public Vector3 headRotationOffset = Vector3.zero;

    [Header("Balls")]
    [Tooltip("How many tail parts must disappear before one ball is activated")]
    [SerializeField] private int _tailPartsPerBall = 10;

    [Header("Movement Bounds (World Space X / Y)")]
    [SerializeField] private float _xMin = -10f;
    [SerializeField] private float _xMax = 10f;
    [SerializeField] private float _yMin = -10f;
    [SerializeField] private float _yMax = 10f;

    [Header("Box Cast Gizmo")]
    [SerializeField] private Color _gizmoHitColor = Color.red;
    [SerializeField] private Color _gizmoMissColor = Color.green;

    // ── Transforms ────────────────────────────────────────────────────────────
    private Transform _arrowHead;
    private Transform _arrowTailCylinders;   // child(1) — holds cylinder objects
    private Transform _arrowTailSpheres;     // child(2) — holds sphere objects at junctions
    private Transform _ballHolder;           // child(3)

    private List<Transform> _tailCylinders;  // was _tailParts
    private List<Transform> _tailSpheres;    // new — junction fill spheres
    private List<Transform> _balls;

    // Cached per-cylinder base thickness so UpdateTail only changes Y scale
    private float _cylinderThicknessX;
    private float _cylinderThicknessZ;
    private float _sphereSize;

    private List<Vector3> positionHistory = new List<Vector3>();
    private BoxCollider _headCollider;

    // ── State ─────────────────────────────────────────────────────────────────
    private int currentCheckpointIndex = 0;
    private bool reachedEnd = false;
    private bool checkpointsUpdated = false;
    private List<Transform> _selectedCheckpoints = new List<Transform>();
    private ClickableBox _clickableBox;
    /*private Vector3 _arrowHeadStartPosition;
    private Vector3 _tailSpheres3Position;
    private bool _noteArrowHeadStartPosition = true;
    private float _ArrowHeadTravelDistance = 0f;*/
    private LTDescr ln1, ln2, ln3, ln4;

    private bool _isDissolvingTail = false;
    private float _dissolveTimer = 0f;
    private int _nextTailToDisable = 0;
    private int _disabledTailCount = 0;
    private int _activatedBallCount = 0;
    private Vector3 _lastCheckpointWorldPos;

    private Vector3 _virtualHeadPos;
    private Vector3 _dissolveDirection;

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public float ArrowLength
    {
        get => _arrowLength;
        set { if (_arrowLength == value) return; _arrowLength = value; /*OnArrowLengthChanged();*/ }
    }

    public float ArrowThickness
    {
        get => _arrowThickness;
        set { if (_arrowThickness == value) return; _arrowThickness = value; /*OnArrowLengthChanged();*/ }
    }

    private void OnValidate()
    {
        /*if (_arrowHead != null && _arrowTailCylinders != null)
            OnArrowLengthChanged();*/
    }

    // =========================================================================
    // AWAKE
    // =========================================================================

    private void Awake()
    {
        _arrowHead = transform.GetChild(0);
        _arrowTailCylinders = transform.GetChild(1);
        _arrowTailSpheres = transform.GetChild(2);
        _ballHolder = transform.GetChild(3);

        _arrowHead.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;

        _clickableBox = _arrowHead.GetComponent<ClickableBox>();
        _clickableBox.OnClicked.AddListener(OnArrowClicked);

        _headCollider = _arrowHead.GetComponent<BoxCollider>();

        // ── Collect cylinders ─────────────────────────────────────────────────
        _tailCylinders = new List<Transform>();
        foreach (Transform item in _arrowTailCylinders)
        {
            _tailCylinders.Add(item);
            item.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;
        }

        // ── Collect spheres ───────────────────────────────────────────────────
        _tailSpheres = new List<Transform>();
        foreach (Transform item in _arrowTailSpheres)
        {
            _tailSpheres.Add(item);
            item.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;
        }

        // ── Collect balls ─────────────────────────────────────────────────────
        _balls = new List<Transform>();
        foreach (Transform item in _ballHolder)
        {
            _balls.Add(item);
            item.gameObject.SetActive(false);
            item.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;
        }

        float length = 0;

        foreach (var item in _tailCylinders)
        {
            if (item.gameObject.activeSelf)
                length += item.localScale.y;
        }

        _arrowLength = length;
    }

    // =========================================================================
    // START
    // =========================================================================

    private void Start()
    {
        positionHistory.Add(_arrowHead.position);
    }

    // =========================================================================
    // ON ARROW CLICKED
    // =========================================================================

    private void OnArrowClicked()
    {
        if (!IsBoxOverlapping())
        {
            MoveArrow();
            Release = true;
        }
    }

    private bool IsBoxOverlapping()
    {
        Vector3 worldCenter = transform.TransformPoint(_headCollider.center);
        Vector3 halfExtents = Vector3.Scale(_arrowHead.transform.localScale / 2, transform.lossyScale);
        Vector3 direction = transform.up;

        return Physics.BoxCast(
            worldCenter, halfExtents, direction,
            out RaycastHit hit, transform.rotation, _rayDistance, _layerMask);
    }

    // =========================================================================
    // ON ARROW LENGTH CHANGED
    // =========================================================================

    /*private void OnArrowLengthChanged()
    {
        int arrowLength = Mathf.Abs(_arrowLength);
        _arrowHead.localScale = new Vector3(_arrowThickness * 100, 100, _arrowThickness * 100);

        // ── Grow cylinders if needed ──────────────────────────────────────────
        if (arrowLength > _tailCylinders.Count)
        {
            for (int i = _tailCylinders.Count + 1; i <= arrowLength; i++)
            {
                GameObject g = Instantiate(_tailCylinders[0].gameObject, _arrowTailCylinders);
                g.name = $"TailCylinder ({i})";
                g.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;
                _tailCylinders.Add(g.transform);
            }
        }

        // ── Grow spheres to match cylinders (one per junction) ────────────────
        // Spheres = cylinders + 1 (one at each front junction + one at the very back)
        int spheresNeeded = arrowLength + 1;
        if (spheresNeeded > _tailSpheres.Count)
        {
            for (int i = _tailSpheres.Count + 1; i <= spheresNeeded; i++)
            {
                GameObject g = Instantiate(_tailSpheres[0].gameObject, _arrowTailSpheres);
                g.name = $"TailSphere ({i})";
                g.GetComponentInChildren<MeshRenderer>().material.color = ArrowColor;
                _tailSpheres.Add(g.transform);
            }
        }

        // ── Grow balls if needed ──────────────────────────────────────────────
        int ballsNeeded = arrowLength / _tailPartsPerBall;
        if (ballsNeeded > _balls.Count)
        {
            for (int i = _balls.Count + 1; i <= ballsNeeded; i++)
            {
                GameObject g = Instantiate(_balls[0].gameObject, _ballHolder);
                g.name = $"Ball ({i})";
                _balls.Add(g.transform);
                g.SetActive(false);
            }
        }

        // ── Activate/deactivate cylinders ─────────────────────────────────────
        for (int i = 0; i < _tailCylinders.Count; i++)
            _tailCylinders[i].gameObject.SetActive(i < arrowLength);

        // ── Activate/deactivate spheres ───────────────────────────────────────
        for (int i = 0; i < _tailSpheres.Count; i++)
            _tailSpheres[i].gameObject.SetActive(i < spheresNeeded && i <= arrowLength);

        // ── Cache thickness values for UpdateTail ─────────────────────────────
        _cylinderThicknessX = _arrowThickness * .1f;
        _cylinderThicknessZ = _arrowThickness * .1f;
        _sphereSize = _arrowThickness * .1f;

        // Apply base scale (Y will be overridden per-frame in UpdateTail)
        foreach (var cyl in _tailCylinders)
            cyl.localScale = new Vector3(_cylinderThicknessX, 0.01f, _cylinderThicknessZ);

        foreach (var sph in _tailSpheres)
            sph.localScale = Vector3.one * _sphereSize;

        foreach (var ball in _balls)
            ball.localScale = Vector3.one * _arrowThickness * 50f;
    }*/

    // =========================================================================
    // UPDATE
    // =========================================================================

    void Update()
    {
        if (_isDissolvingTail)
        {
            AdvanceVirtualHead();
            UpdateTail();
            DissolveTail();
            return;
        }

        if (reachedEnd) return;

        //MoveHead();
        RecordHeadPath();
        UpdateTail();
    }

    // =========================================================================
    // MOVE HEAD
    // =========================================================================

    void MoveArrow()
    {
        float arrowXLocalPosition = _arrowHead.localPosition.x;
        float arrowYLocalPosition = _arrowHead.localPosition.y;

        float arrowXWorldPosition = _arrowHead.position.x;
        float arrowYWorldPosition = _arrowHead.position.y;

        if (!checkpointsUpdated)
        {
            if (Mathf.Approximately(_arrowHead.eulerAngles.z, 0f))
            {
                checkpoints[0].position = new Vector3(arrowXWorldPosition, 0, 0);
                checkpoints[0].localPosition = new Vector3(checkpoints[0].localPosition.x, _yMax, 0);
                if (Vector3.Distance(checkpoints[0].position, checkpoints[1].position) <= Vector3.Distance(checkpoints[0].position, checkpoints[2].position))
                    _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[1], checkpoints[5], checkpoints[4], checkpoints[6] };
                else
                    _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[2], checkpoints[3], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 180f) || Mathf.Approximately(_arrowHead.eulerAngles.z, -180f))
            {
                checkpoints[0].position = new Vector3(arrowXWorldPosition, 0, 0);
                checkpoints[0].localPosition = new Vector3(checkpoints[0].localPosition.x, _yMin, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 270f))
            {
                checkpoints[0].position = new Vector3(0, arrowYWorldPosition, 0);
                checkpoints[0].localPosition = new Vector3(_xMax, checkpoints[0].localPosition.y, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[5], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 90f))
            {
                checkpoints[0].position = new Vector3(0, arrowYWorldPosition, 0);
                checkpoints[0].localPosition = new Vector3(_xMin, checkpoints[0].localPosition.y, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[3], checkpoints[4], checkpoints[6] };
            }
            checkpointsUpdated = true;
        }

        /*bool activeStatus = false;
        float startValue = 0;

        float _cylinder3Scale = _tailCylinders[3].localScale.y;
        Vector3 _sphere3Position = _tailSpheres[3].localPosition;

        Vector3 _arrowHeadPosition = _arrowHead.localPosition;
        float _cylinder0Scale = _tailCylinders[0].localScale.y;

        float checkpointDis = Vector3.Distance(_arrowHead.localPosition, checkpoints[0].localPosition);

        if (checkpointDis <= _cylinder3Scale)
        {
            startValue = checkpointDis;
            activeStatus = true;
        }
        else
        {
            startValue = _cylinder3Scale;
            activeStatus = false;
        }
        float t = startValue / moveSpeed;

        ln1 = LeanTween.value(startValue, 0, t).setOnUpdate((float value) =>
        {
            if (activeStatus)
            {
                _tailCylinders[3].localScale = new Vector3(50, _cylinder3Scale - (startValue - value), 50);
            }
            else
            {
                _tailCylinders[3].localScale = new Vector3(50, value, 50);
            }
            var dirS1 = _tailSpheres[3].position - _tailCylinders[3].position;
            dirS1.Normalize();
            _tailSpheres[3].localPosition = _sphere3Position - (dirS1 * (startValue - value));

            _arrowHead.localPosition = _arrowHeadPosition + (Vector3.up * (startValue - value));
            _tailCylinders[0].position = _arrowHead.position;
            _tailCylinders[0].localScale = new Vector3(50, _cylinder0Scale + (startValue - value), 50);
        }).setOnComplete(() =>
        {
            _tailCylinders[3].gameObject.SetActive(activeStatus);
            _tailSpheres[3].gameObject.SetActive(activeStatus);
            ln1 = null;
            if (activeStatus)
                return;

            float _cylinder2Scale = _tailCylinders[2].localScale.y;
            Vector3 _sphere2Position = _tailSpheres[2].localPosition;

            Vector3 _arrowHeadPosition = _arrowHead.localPosition;
            float _cylinder0Scale = _tailCylinders[0].localScale.y;

            checkpointDis = Vector3.Distance(_arrowHead.localPosition, checkpoints[0].localPosition);

            if (checkpointDis <= _cylinder2Scale)
            {
                startValue = checkpointDis;
                activeStatus = true;
            }
            else
            {
                startValue = _cylinder2Scale;
                activeStatus = false;
            }

            t = startValue / moveSpeed;

            ln1 = LeanTween.value(startValue, 0, t).setOnUpdate((float value) =>
            {
                if (activeStatus)
                {
                    _tailCylinders[2].localScale = new Vector3(50, _cylinder2Scale - (startValue - value), 50);
                }
                else
                {
                    _tailCylinders[2].localScale = new Vector3(50, value, 50);
                }
                var dirS1 = _tailSpheres[2].position - _tailCylinders[2].position;
                dirS1.Normalize();
                _tailSpheres[2].localPosition = _sphere2Position - (dirS1 * (startValue - value));

                _arrowHead.localPosition = _arrowHeadPosition + (Vector3.up * (startValue - value));
                _tailCylinders[0].position = _arrowHead.position;
                _tailCylinders[0].localScale = new Vector3(50, _cylinder0Scale + (startValue - value), 50);
            }).setOnComplete(() =>
            {
                _tailCylinders[2].gameObject.SetActive(activeStatus);
                _tailSpheres[2].gameObject.SetActive(activeStatus);
                ln1 = null;
                if (activeStatus)
                    return;

                float _cylinder1Scale = _tailCylinders[1].localScale.y;
                Vector3 _sphere1Position = _tailSpheres[1].localPosition;

                Vector3 _arrowHeadPosition = _arrowHead.localPosition;
                float _cylinder0Scale = _tailCylinders[0].localScale.y;


                checkpointDis = Vector3.Distance(_arrowHead.localPosition, checkpoints[0].localPosition);

                if (checkpointDis <= _cylinder1Scale)
                {
                    startValue = checkpointDis;
                    activeStatus = true;
                }
                else
                {
                    startValue = _cylinder1Scale;
                    activeStatus = false;
                }
                t = startValue / moveSpeed;

                ln1 = LeanTween.value(startValue, 0, t).setOnUpdate((float value) =>
                {
                    if (activeStatus)
                    {
                        _tailCylinders[1].localScale = new Vector3(50, _cylinder1Scale - (startValue - value), 50);
                    }
                    else
                    {
                        _tailCylinders[1].localScale = new Vector3(50, value, 50);
                    }
                    var dirS1 = _tailSpheres[1].position - _tailCylinders[1].position;
                    dirS1.Normalize();
                    _tailSpheres[1].localPosition = _sphere1Position - (dirS1 * (startValue - value));

                    _arrowHead.localPosition = _arrowHeadPosition + (Vector3.up * (startValue - value));
                    _tailCylinders[0].position = _arrowHead.position;
                    _tailCylinders[0].localScale = new Vector3(50, _cylinder0Scale + (startValue - value), 50);
                }).setOnComplete(() =>
                {
                    _tailCylinders[1].gameObject.SetActive(activeStatus);
                    _tailSpheres[1].gameObject.SetActive(activeStatus);
                    ln1 = null;
                    if (activeStatus)
                        return;

                    Vector3 _arrowHeadPosition = _arrowHead.localPosition;
                    float _cylinder0Scale = _tailCylinders[0].localScale.y;
                    Vector3 _sphere0Position = _tailSpheres[1].localPosition;

                    checkpointDis = Vector3.Distance(_arrowHead.localPosition, checkpoints[0].localPosition);

                    startValue = checkpointDis;
                    activeStatus = true;
                    if (checkpointDis <= _cylinder1Scale)
                    t = startValue / moveSpeed;

                    ln1 = LeanTween.value(startValue, 0, t).setOnUpdate((float value) =>
                    {
                        *//*if (activeStatus)
                        {
                            _tailCylinders[1].localScale = new Vector3(50, _cylinder1Scale - (startValue - value), 50);
                        }
                        else
                        {
                            _tailCylinders[1].localScale = new Vector3(50, value, 50);
                        }*//*
                        var dirS1 = _tailSpheres[0].position - _tailCylinders[0].position;
                        dirS1.Normalize();
                        _tailSpheres[0].localPosition = _sphere0Position - (dirS1 * (startValue - value));

                        _arrowHead.localPosition = _arrowHeadPosition + (Vector3.up * (startValue - value));
                        _tailCylinders[0].position = _arrowHead.position;
                        //_tailCylinders[0].localScale = new Vector3(50, _cylinder0Scale + (startValue - value), 50);
                    }).setOnComplete(() => 
                    {
                        _tailCylinders[0].gameObject.SetActive(activeStatus);
                        _tailSpheres[0].gameObject.SetActive(activeStatus);
                        ln1 = null;
                    });
                });
            });
        });*/

        StartCollapseSequence();
    }

    private void StartCollapseSequence()
    {
        int activeChildCount = 0;
        foreach (var item in _tailCylinders)
        {
            if (item.gameObject.activeSelf)
            {
                activeChildCount++;
            }
        }
        AnimateCylinder(activeChildCount - 1);
    }

    private void AnimateCylinder(int index)
    {
        // ── Cache state at the moment this step starts ────────────────────────
        float cylScale = _tailCylinders[index].localScale.y;
        Vector3 spherePos = _tailSpheres[index].localPosition;
        Vector3 headPos = _arrowHead.localPosition;
        float cyl0Scale = _tailCylinders[0].localScale.y;

        float checkpointDis = Vector3.Distance(_arrowHead.localPosition, _selectedCheckpoints[currentCheckpointIndex].localPosition);

        bool activeStatus;
        float startValue;

        // Last cylinder (index 0) always active, no scale condition
        if (index == 0)
        {
            startValue = checkpointDis;
            activeStatus = true;
        }
        else
        {
            activeStatus = checkpointDis <= cylScale;
            startValue = activeStatus ? checkpointDis : cylScale;
        }

        float duration = startValue / moveSpeed;

        ln1 = LeanTween.value(startValue, 0f, duration).setOnUpdate((float value) =>
        {
            float delta = startValue - value;

            // Scale current cylinder (skip on last — it's being replaced by cyl[0])
            if (index > 0)
            {
                float newScale = activeStatus ? cylScale - delta : value;
                _tailCylinders[index].localScale = new Vector3(50f, newScale, 50f);
            }

            // Move junction sphere along cylinder direction
            Vector3 dir = (_tailSpheres[index].position - _tailCylinders[index].position).normalized;
            _tailSpheres[index].localPosition = spherePos - dir * delta;

            // Slide arrow head up and stretch cylinder[0] to follow
            if (Mathf.Approximately(_arrowHead.localEulerAngles.z, 0))
                _arrowHead.localPosition = headPos + Vector3.up * delta;
            else if (Mathf.Approximately(_arrowHead.localEulerAngles.z, 270))
                _arrowHead.localPosition = headPos + Vector3.right * delta;
            else if (Mathf.Approximately(_arrowHead.localEulerAngles.z, 90))
                _arrowHead.localPosition = headPos + Vector3.left * delta;
            else if (Mathf.Approximately(_arrowHead.localEulerAngles.z, 180) || Mathf.Approximately(_arrowHead.localEulerAngles.z, -180))
                _arrowHead.localPosition = headPos + Vector3.down * delta;

            _tailCylinders[0].position = _arrowHead.position;

            if (index > 0)
                _tailCylinders[0].localScale = new Vector3(50f, cyl0Scale + delta, 50f);
        }).setOnComplete(() =>
        {
            _tailCylinders[index].gameObject.SetActive(activeStatus);
            _tailSpheres[index].gameObject.SetActive(activeStatus);
            ln1 = null;

            // Chain to next cylinder, or stop if we've reached the last
            if (!activeStatus && index > 0)
                AnimateCylinder(index - 1);
            else
            {
                if (currentCheckpointIndex <= _selectedCheckpoints.Count - 2)
                {
                    var dir1 = _arrowHead.position - _selectedCheckpoints[currentCheckpointIndex + 1].position;
                    dir1.Normalize();
                    if (dir1.x == -1)
                    {
                        _arrowHead.localEulerAngles = new Vector3(0, 0, -90);
                    }
                    if (dir1.x == 1)
                    {
                        _arrowHead.localEulerAngles = new Vector3(0, 0, 90);
                    }
                    if (dir1.y == 1)
                    {
                        _arrowHead.localEulerAngles = new Vector3(0, 0, 180);
                    }
                    if (dir1.y == -1)
                    {
                        _arrowHead.localEulerAngles = new Vector3(0, 0, 0);
                    }

                    int activeChildCount = 0;
                    foreach (var item in _tailCylinders)
                    {
                        if (item.gameObject.activeSelf)
                        {
                            activeChildCount++;
                        }
                    }

                    for (int i = activeChildCount; i >= 0; i--)
                    {
                        if (i > 0)
                        {
                            _tailCylinders[i].position = _tailCylinders[i - 1].position;
                            _tailCylinders[i].rotation = _tailCylinders[i - 1].rotation;
                            _tailCylinders[i].localScale = _tailCylinders[i - 1].localScale;



                            if (i == activeChildCount)
                            {
                                _tailCylinders[i].gameObject.SetActive(true);
                                _tailSpheres[i].gameObject.SetActive(true);
                            }
                        }
                        else
                        {
                            _tailCylinders[i].position = _arrowHead.position;
                            _tailCylinders[i].rotation = _arrowHead.rotation;
                            _tailCylinders[i].localScale = new Vector3(50, 0, 50);
                        }

                        if (i != activeChildCount)
                            _tailSpheres[i].localPosition = _tailCylinders[i + 1].localPosition;
                        else
                        {
                            _tailSpheres[i].localPosition = _tailCylinders[i].localPosition;
                            float scale = _tailCylinders[i].localScale.y;
                            if (_tailCylinders[i].localEulerAngles.z == 0)
                            {
                                _tailSpheres[i].localPosition = new Vector3(_tailSpheres[i].localPosition.x, _tailSpheres[i].localPosition.y - scale, _tailSpheres[i].localPosition.z);
                            }
                            else if (_tailCylinders[i].localEulerAngles.z == 90)
                            {
                                _tailSpheres[i].localPosition = new Vector3(_tailSpheres[i].localPosition.x + scale, _tailSpheres[i].localPosition.y, _tailSpheres[i].localPosition.z);
                            }
                            else if (_tailCylinders[i].localEulerAngles.z == -90)
                            {
                                _tailSpheres[i].localPosition = new Vector3(_tailSpheres[i].localPosition.x - scale, _tailSpheres[i].localPosition.y, _tailSpheres[i].localPosition.z);
                            }
                            else if (_tailCylinders[i].localEulerAngles.z == 180 || _tailCylinders[i].localEulerAngles.z == -180)
                            {
                                _tailSpheres[i].localPosition = new Vector3(_tailSpheres[i].localPosition.x, _tailSpheres[i].localPosition.y + scale, _tailSpheres[i].localPosition.z);
                            }
                        }
                    }
                    currentCheckpointIndex++;
                    AnimateCylinder(activeChildCount);
                }
                else
                {
                    DissolveThisArrow1();
                }
            }
        });
    }

    void DissolveThisArrow1()
    {
        Debug.Log("1st The END is HERE !!!!!!!!!!!!!!!!");
        _arrowHead.gameObject.SetActive(false);

        int activeChildCount = 0;
        foreach (var item in _tailCylinders)
        {
            if (item.gameObject.activeSelf)
            {
                activeChildCount++;
            }
        }

        for (int i = _tailCylinders.Count; i > 0; i--)
        {
            if (i <= activeChildCount)
            {
                _tailCylinders[i].localPosition = _tailCylinders[i - 1].localPosition;
                _tailCylinders[i].rotation = _tailCylinders[i - 1].rotation;
                _tailCylinders[i].localScale = _tailCylinders[i - 1].localScale;
                _tailSpheres[i].localPosition = _tailSpheres[i - 1].localPosition;
            }
        }
        _tailCylinders[activeChildCount].gameObject.SetActive(true);
        _tailSpheres[activeChildCount].gameObject.SetActive(true);

        Dissolve(activeChildCount);
    }

    void DissolveThisArrow2()
    {
        Debug.Log("2nd The END is HERE !!!!!!!!!!!!!!!!");

        int activeChildCount = 0;
        foreach (var item in _tailCylinders)
        {
            if (item.gameObject.activeSelf)
            {
                activeChildCount++;
            }
        }

        for (int i = _tailCylinders.Count; i > 0; i--)
        {
            if (i <= activeChildCount)
            {
                _tailCylinders[i].localPosition = _tailCylinders[i - 1].localPosition;
                _tailCylinders[i].rotation = _tailCylinders[i - 1].rotation;
                _tailCylinders[i].localScale = _tailCylinders[i - 1].localScale;
                _tailSpheres[i].localPosition = _tailSpheres[i - 1].localPosition;
            }
        }

        Dissolve(activeChildCount);
    }

    void Dissolve(int activeChildCount)
    {
        int currIndex = 0;
        float finalCylinderScale = _tailCylinders[0].localScale.y;
        float startValue;
        startValue = _tailCylinders[currIndex].localScale.y;
        float duration = startValue / moveSpeed;
        _tailCylinders[currIndex + 1].localPosition = _tailCylinders[currIndex].localPosition + new Vector3(0, startValue, 0);
        float nextCylinderPosition = _tailCylinders[currIndex + 1].localPosition.y;
        _tailCylinders[currIndex + 1].localScale = new Vector3(50, 0, 50);
        float lastCylinderScale = _tailCylinders[activeChildCount].localScale.y;
        Vector3 lastSpherePos = _tailSpheres[activeChildCount].localPosition;

        ln1 = LeanTween.value(startValue, 0, duration).setOnUpdate((float value) =>
        {
            _tailCylinders[currIndex].localScale = new Vector3(50, value, 50);
            _tailCylinders[currIndex + 1].localPosition = new Vector3(0, nextCylinderPosition - (startValue - value), 0);
            _tailCylinders[currIndex + 1].localScale = new Vector3(50, startValue - value, 50);
            _tailCylinders[activeChildCount].localScale = new Vector3(50, lastCylinderScale - (startValue - value), 50);
            Vector3 dir = (_tailSpheres[activeChildCount].position - _tailCylinders[activeChildCount].position).normalized;
            _tailSpheres[activeChildCount].localPosition = lastSpherePos - dir * (startValue - value);

        }).setOnComplete(() =>
        {
            ln1 = null;
            _tailCylinders[currIndex].localScale = new Vector3(50, startValue, 50);
            _tailCylinders[currIndex + 1].localScale = new Vector3(50, 0, 50);
            _tailCylinders[currIndex + 1].localPosition = _tailCylinders[currIndex].localPosition + new Vector3(0, startValue, 0);
            if (_tailCylinders[activeChildCount].localScale.y > finalCylinderScale)
                Dissolve(activeChildCount);
            else if(_tailCylinders[activeChildCount].localScale.y > 0)
            {
                Debug.Log("Last Cylinder Going !!!!!!!!");
                startValue = _tailCylinders[activeChildCount].localScale.y;
                float duration = startValue / moveSpeed;
                _tailCylinders[currIndex + 1].localPosition = _tailCylinders[currIndex].localPosition + new Vector3(0, _tailCylinders[currIndex].localScale.y, 0);
                float nextCylinderPosition = _tailCylinders[currIndex + 1].localPosition.y;
                _tailCylinders[currIndex + 1].localScale = new Vector3(50, 0, 50);
                float lastCylinderScale = _tailCylinders[activeChildCount].localScale.y;
                Vector3 lastSpherePos = _tailSpheres[activeChildCount].localPosition;
                float currentCylinderScale = _tailCylinders[currIndex].localScale.y;

                ln1 = LeanTween.value(startValue, 0, duration).setOnUpdate((float value) =>
                {
                    _tailCylinders[currIndex].localScale = new Vector3(50, currentCylinderScale - (startValue - value), 50);
                    _tailCylinders[currIndex + 1].localPosition = new Vector3(0, nextCylinderPosition - (startValue - value), 0);
                    _tailCylinders[currIndex + 1].localScale = new Vector3(50, startValue - value, 50);
                    _tailCylinders[activeChildCount].localScale = new Vector3(50, lastCylinderScale - (startValue - value), 50);
                    Vector3 dir = (_tailSpheres[activeChildCount].position - _tailCylinders[activeChildCount].position).normalized;
                    _tailSpheres[activeChildCount].localPosition = lastSpherePos - dir * (startValue - value);

                }).setOnComplete(() =>
                {
                    Debug.Log("Test - 1");
                    ln1 = null;
                    _tailCylinders[currIndex].localScale = new Vector3(50, currentCylinderScale, 50);
                    _tailCylinders[currIndex + 1].localPosition = _tailCylinders[currIndex].localPosition + new Vector3(0, _tailCylinders[currIndex].localScale.y, 0);
                    _tailCylinders[currIndex + 1].localScale = new Vector3(50, 0, 50);

                    _tailCylinders[activeChildCount].gameObject.SetActive(false);
                    _tailSpheres[activeChildCount].gameObject.SetActive(false);

                    DissolveThisArrow2();
                });
            }
        });
    }

    /*void MoveHead()
    {
        if (!Release) return;

        float arrowXLocalPosition = _arrowHead.localPosition.x;
        float arrowYLocalPosition = _arrowHead.localPosition.y;

        float arrowXWorldPosition = _arrowHead.position.x;
        float arrowYWorldPosition = _arrowHead.position.y;

        if (!checkpointsUpdated)
        {
            if (Mathf.Approximately(_arrowHead.eulerAngles.z, 0f))
            {
                checkpoints[0].position = new Vector3(arrowXWorldPosition, 0, 0);
                checkpoints[0].localPosition = new Vector3(checkpoints[0].localPosition.x, _yMax, 0);
                if (Vector3.Distance(checkpoints[0].position, checkpoints[1].position) <= Vector3.Distance(checkpoints[0].position, checkpoints[2].position))
                    _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[1], checkpoints[5], checkpoints[4], checkpoints[6] };
                else
                    _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[2], checkpoints[3], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 180f) || Mathf.Approximately(_arrowHead.eulerAngles.z, -180f))
            {
                checkpoints[0].position = new Vector3(arrowXWorldPosition, 0, 0);
                checkpoints[0].localPosition = new Vector3(checkpoints[0].localPosition.x, _yMin, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 270f))
            {
                checkpoints[0].position = new Vector3(0, arrowYWorldPosition, 0);
                checkpoints[0].localPosition = new Vector3(_xMax, checkpoints[0].localPosition.y, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[5], checkpoints[4], checkpoints[6] };
            }

            else if (Mathf.Approximately(_arrowHead.eulerAngles.z, 90f))
            {
                checkpoints[0].position = new Vector3(0, arrowYWorldPosition, 0);
                checkpoints[0].localPosition = new Vector3(_xMin, checkpoints[0].localPosition.y, 0);
                _selectedCheckpoints = new List<Transform> { checkpoints[0], checkpoints[3], checkpoints[4], checkpoints[6] };
            }
            checkpointsUpdated = true;
        }

        else
        {
            if (currentCheckpointIndex >= _selectedCheckpoints.Count)
            {
                _lastCheckpointWorldPos = _selectedCheckpoints[_selectedCheckpoints.Count - 1].position;

                if (_selectedCheckpoints.Count >= 2)
                    _dissolveDirection = (_selectedCheckpoints[_selectedCheckpoints.Count - 1].position
                                       - _selectedCheckpoints[_selectedCheckpoints.Count - 2].position).normalized;
                else
                    _dissolveDirection = _arrowHead.forward;

                _virtualHeadPos = _lastCheckpointWorldPos;
                reachedEnd = true;
                _arrowHead.gameObject.SetActive(false);
                _isDissolvingTail = true;
                _nextTailToDisable = 0;
                _dissolveTimer = 0f;
                return;
            }

            if (_noteArrowHeadStartPosition)
            {
                _arrowHeadStartPosition = _arrowHead.localPosition;
                _tailSpheres3Position = _tailSpheres[3].localPosition;
                _noteArrowHeadStartPosition = false;
            }

            Vector3 target = _selectedCheckpoints[currentCheckpointIndex].position;
            Vector3 dir = target - _arrowHead.position;

            _arrowHead.position = Vector3.MoveTowards(_arrowHead.position, target, moveSpeed * Time.deltaTime);

            _ArrowHeadTravelDistance = Vector3.Distance(_arrowHead.localPosition, _arrowHeadStartPosition);

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
                Quaternion offset = Quaternion.Euler(headRotationOffset);
                _arrowHead.rotation = Quaternion.Slerp(_arrowHead.rotation, lookRot * offset, rotationSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(_arrowHead.position, target) < 0.05f)
            {
                _tailSpheres[3].position = _tailSpheres[2].position;
                _tailSpheres[2].position = _tailSpheres[1].position;
                _tailSpheres[1].position = _tailSpheres[0].position;
                _tailSpheres[0].position = _selectedCheckpoints[currentCheckpointIndex].position;

                _tailCylinders[1].position = _tailSpheres[0].position;
                Vector3 dir1 = _tailSpheres[1].position - _tailCylinders[1].position;
                float angle1 = Mathf.Atan2(dir1.y, dir1.x) * Mathf.Rad2Deg;
                _tailCylinders[1].rotation = Quaternion.Euler(0f, 0f, angle1 + 90);

                _tailCylinders[2].position = _tailSpheres[1].position;
                Vector3 dir2 = _tailSpheres[2].position - _tailCylinders[2].position;
                float angle2 = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
                _tailCylinders[2].rotation = Quaternion.Euler(0f, 0f, angle2 + 90);

                _tailCylinders[3].position = _tailSpheres[2].position;
                Vector3 dir3 = _tailSpheres[3].position - _tailCylinders[3].position;
                float angle3 = Mathf.Atan2(dir3.y, dir3.x) * Mathf.Rad2Deg;
                _tailCylinders[3].rotation = Quaternion.Euler(0f, 0f, angle3 + 90);

                currentCheckpointIndex++;
            }

            _tailCylinders[0].rotation = _arrowHead.rotation;
            _tailCylinders[0].position = _arrowHead.position;

            _tailCylinders[0].localScale = new Vector3(50, Vector3.Distance(_tailSpheres[0].localPosition, _tailCylinders[0].localPosition), 50);
            _tailCylinders[1].localScale = new Vector3(50, Vector3.Distance(_tailSpheres[1].localPosition, _tailCylinders[1].localPosition), 50);
            _tailCylinders[2].localScale = new Vector3(50, Vector3.Distance(_tailSpheres[2].localPosition, _tailCylinders[2].localPosition), 50);
            _tailCylinders[3].localScale = new Vector3(50, Vector3.Distance(_tailSpheres[3].localPosition, _tailCylinders[3].localPosition), 50);


            if (_tailCylinders[2].localScale.y > 0.15f && !_tailSpheres[3].gameObject.activeSelf)
            {
                var dirS1 = _tailSpheres[2].position - _tailCylinders[2].position;
                dirS1.Normalize();
                _tailSpheres[2].localPosition = new Vector3(_tailSpheres[2].localPosition.x, _tailSpheres[2].localPosition.y, 0) - (dirS1 * _ArrowHeadTravelDistance);
            }

            if (_tailCylinders[3].localScale.y > 15f)
            {
                var dirS1 = _tailSpheres[3].position - _tailCylinders[3].position;
                dirS1.Normalize();
                _tailSpheres[3].localPosition = _tailSpheres3Position - (dirS1 * _ArrowHeadTravelDistance);
            }
            else if (_tailSpheres[3].gameObject.activeSelf)
            {
                _tailSpheres[3].gameObject.SetActive(false);
                _tailCylinders[3].gameObject.SetActive(false);
                _noteArrowHeadStartPosition = true;
            }
        }
    }*/

    // =========================================================================
    // PATH RECORDING
    // =========================================================================

    void RecordHeadPath()
    {
        Vector3 last = positionHistory[positionHistory.Count - 1];
        if (Vector3.Distance(_arrowHead.position, last) >= pathRecordInterval)
            positionHistory.Add(_arrowHead.position);
    }

    void AdvanceVirtualHead()
    {
        _virtualHeadPos += _dissolveDirection * moveSpeed * Time.deltaTime;

        Vector3 last = positionHistory[positionHistory.Count - 1];
        if (Vector3.Distance(_virtualHeadPos, last) >= pathRecordInterval)
            positionHistory.Add(_virtualHeadPos);
    }

    // =========================================================================
    // UPDATE TAIL — cylinders stretch between path samples, spheres fill joints
    // =========================================================================

    void UpdateTail()
    {
        if (positionHistory.Count < 2) return;

        // ── Cylinders ─────────────────────────────────────────────────────────
        // Cylinder[i] spans from distFront (i*spacing) to distBack ((i+1)*spacing)
        // It is positioned at the midpoint and Y-scaled to fill that gap exactly.
        /*for (int i = 0; i < _tailCylinders.Count; i++)
        {
            if (!_tailCylinders[i].gameObject.activeSelf) continue;

            float distFront = i * _tailPartSpacing;
            float distBack = (i + 1) * _tailPartSpacing;

            Vector3 frontPos = SamplePathAtDistance(distFront);
            Vector3 backPos = SamplePathAtDistance(distBack);

            Vector3 dir = frontPos - backPos;
            float length = dir.magnitude;

            // Midpoint position
            _tailCylinders[i].position = (frontPos + backPos) * 0.5f;

            // Rotate so cylinder Y axis aligns with travel direction
            if (dir.sqrMagnitude > 0.0001f)
                _tailCylinders[i].rotation = Quaternion.LookRotation(dir.normalized)
                                           * Quaternion.Euler(90f, 0f, 0f);

            // Stretch to fill exactly the gap (Unity cylinder height = 2 × localScale.y)
            _tailCylinders[i].localScale = new Vector3(
                _cylinderThicknessX,
                length * 0.5f,
                _cylinderThicknessZ);
        }*/

        // ── Spheres ───────────────────────────────────────────────────────────
        // Sphere[i] sits at the front junction of cylinder[i] (distance = i * spacing).
        // This is the same point where cylinder[i-1] back meets cylinder[i] front,
        // hiding the seam at every corner.
        /*for (int i = 0; i < _tailSpheres.Count; i++)
        {
            if (!_tailSpheres[i].gameObject.activeSelf) continue;

            float dist = i * _tailPartSpacing;
            _tailSpheres[i].position = SamplePathAtDistance(dist);
        }*/


    }

    // =========================================================================
    // DISSOLVE TAIL
    // Disables cylinder[i] and sphere[i] together at the same step so the
    // junction sphere never outlives the cylinder it was covering.
    // =========================================================================

    void DissolveTail()
    {
        float timePerPart = _tailPartSpacing / moveSpeed;
        _dissolveTimer += Time.deltaTime;

        while (_dissolveTimer >= timePerPart)
        {
            _dissolveTimer -= timePerPart;

            // Skip already-inactive cylinders
            while (_nextTailToDisable < _tailCylinders.Count
                   && !_tailCylinders[_nextTailToDisable].gameObject.activeSelf)
            {
                _nextTailToDisable++;
            }

            if (_nextTailToDisable >= _tailCylinders.Count)
            {
                // All cylinders gone — hide the containers and finish
                _isDissolvingTail = false;
                _arrowTailCylinders.gameObject.SetActive(false);
                _arrowTailSpheres.gameObject.SetActive(false);
                return;
            }

            // Disable cylinder and its matching junction sphere together
            _tailCylinders[_nextTailToDisable].gameObject.SetActive(false);

            if (_nextTailToDisable < _tailSpheres.Count)
                _tailSpheres[_nextTailToDisable].gameObject.SetActive(false);

            _nextTailToDisable++;
            _disabledTailCount++;

            if (_disabledTailCount % _tailPartsPerBall == 0)
                ActivateNextBall();
        }
    }

    // =========================================================================
    // ACTIVATE BALL
    // =========================================================================

    void ActivateNextBall()
    {
        if (_activatedBallCount >= _balls.Count)
        {
            Debug.LogWarning($"Arrow: Tried to activate ball #{_activatedBallCount + 1} " +
                             $"but only {_balls.Count} balls exist.");
            return;
        }

        Transform ball = _balls[_activatedBallCount];
        ball.position = _lastCheckpointWorldPos;
        ball.gameObject.SetActive(true);
        _activatedBallCount++;
    }

    // =========================================================================
    // PATH SAMPLING
    // =========================================================================

    Vector3 SamplePathAtDistance(float distance)
    {
        float accumulated = 0f;
        int last = positionHistory.Count - 1;

        for (int i = last; i > 0; i--)
        {
            float segLen = Vector3.Distance(positionHistory[i], positionHistory[i - 1]);

            if (accumulated + segLen >= distance)
            {
                float t = (distance - accumulated) / segLen;
                return Vector3.Lerp(positionHistory[i], positionHistory[i - 1], t);
            }

            accumulated += segLen;
        }

        return positionHistory[0];
    }

    // =========================================================================
    // GIZMOS
    // =========================================================================

    void OnDrawGizmos()
    {
        if (_headCollider == null) return;

        Vector3 worldCenter = transform.TransformPoint(_headCollider.center);
        Vector3 halfExtents = Vector3.Scale(_arrowHead.transform.localScale / 2, transform.lossyScale);
        Vector3 direction = transform.up;

        bool isHitting = Physics.BoxCast(
            worldCenter, halfExtents, direction,
            out RaycastHit hit, transform.rotation, _rayDistance, _layerMask);

        Gizmos.color = isHitting ? _gizmoHitColor : _gizmoMissColor;

        float castLength = isHitting ? hit.distance : _rayDistance;
        Vector3 castEnd = worldCenter + direction * castLength;

        Gizmos.matrix = Matrix4x4.TRS(worldCenter, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

        Gizmos.matrix = Matrix4x4.TRS(castEnd, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = Matrix4x4.identity;

        Vector3[] offsets = new Vector3[]
        {
            new Vector3( halfExtents.x,  halfExtents.y,  halfExtents.z),
            new Vector3(-halfExtents.x,  halfExtents.y,  halfExtents.z),
            new Vector3( halfExtents.x,  halfExtents.y, -halfExtents.z),
            new Vector3(-halfExtents.x,  halfExtents.y, -halfExtents.z),
        };

        foreach (Vector3 offset in offsets)
        {
            Vector3 rotatedOffset = transform.rotation * offset;
            Gizmos.DrawLine(worldCenter + rotatedOffset, castEnd + rotatedOffset);
        }

        if (isHitting)
            Gizmos.DrawWireSphere(hit.point, 0.05f);
    }
}