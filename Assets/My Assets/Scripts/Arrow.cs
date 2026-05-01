using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class Arrow : MonoBehaviour
{
    [Header("Arrow Settings")]
    [SerializeField] private int _arrowLength = 1;
    [SerializeField] private float _arrowThickness = 1f;
    [SerializeField] private float _tailPartSpacing = 0.05f;

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

    private Transform _arrowHead;
    private Transform _arrowTail;
    private Transform _ballHolder;
    private List<Transform> _tailParts;
    private List<Transform> _balls;
    private List<Vector3> positionHistory = new List<Vector3>();

    private int currentCheckpointIndex = 0;
    private bool reachedEnd = false;
    private bool checkpointsUpdated = false;

    // ── Dissolve state ────────────────────────────────────────────────────────
    private bool _isDissolvingTail = false;
    private float _dissolveTimer = 0f;
    private int _nextTailToDisable = 0;
    private int _disabledTailCount = 0;
    private int _activatedBallCount = 0;
    private Vector3 _lastCheckpointWorldPos;

    // Virtual head keeps moving past the checkpoint so the tail follows it in
    private Vector3 _virtualHeadPos;
    private Vector3 _dissolveDirection;
    // ─────────────────────────────────────────────────────────────────────────

    public int ArrowLength
    {
        get => _arrowLength;
        set
        {
            if (_arrowLength == value) return;
            _arrowLength = value;
            OnArrowLengthChanged();
        }
    }

    public float ArrowThickness
    {
        get => _arrowThickness;
        set
        {
            if (_arrowThickness == value) return;
            _arrowThickness = value;
            OnArrowLengthChanged();
        }
    }

    private void OnValidate()
    {
        if (_arrowHead != null && _arrowTail != null)
            OnArrowLengthChanged();
    }

    private void Awake()
    {
        _arrowHead = transform.GetChild(0);
        _arrowTail = transform.GetChild(1);
        _ballHolder = transform.GetChild(2);

        _tailParts = new List<Transform>();
        _balls = new List<Transform>();

        foreach (Transform item in _arrowTail)
            _tailParts.Add(item);

        foreach (Transform item in _ballHolder)
        {
            _balls.Add(item);
            item.gameObject.SetActive(false); // all balls hidden at start
        }
    }

    private void Start()
    {
        OnArrowLengthChanged();
        positionHistory.Add(_arrowHead.position);
    }

    private void OnArrowLengthChanged()
    {
        var arrowLength = Mathf.Abs(_arrowLength);
        _arrowHead.localScale = new Vector3(_arrowThickness * 100, 100, _arrowThickness * 100);

        //transform.localEulerAngles = _arrowLength > 0 ? new Vector3(0f, 0f, 0f) : new Vector3(0f, 0f, 180f);

        if (arrowLength > _tailParts.Count)
        {
            for (int i = _tailParts.Count + 1; i <= arrowLength; i++)
            {
                GameObject g = Instantiate(_tailParts[0].gameObject, _arrowTail);
                g.name = $"TailPart ({i})";
                _tailParts.Add(g.transform);
            }
        }

        if (arrowLength / _tailPartsPerBall > _balls.Count)
        {
            for (int i = _balls.Count + 1; i <= arrowLength / _tailPartsPerBall; i++)
            {
                GameObject g = Instantiate(_balls[0].gameObject, _ballHolder);
                g.name = $"Ball ({i})";
                _balls.Add(g.transform);
            }
        }

        for (int i = 0; i < _tailParts.Count; i++)
        {
            var part = _tailParts[i];
            if (i < arrowLength)
            {
                part.gameObject.SetActive(true);
                part.localPosition = new Vector3(0f, i * 0.02f, 0f);
            }
            else
            {
                part.gameObject.SetActive(false);
            }
        }

        foreach (var item in _tailParts)
        {
            item.localScale = new Vector3(_arrowThickness * .1f, 0.01f, _arrowThickness * .1f);
            positionHistory.Add(item.position);
        }
        foreach (var item in _balls)
        {
            item.localScale = Vector3.one * _arrowThickness * 50f;
        }
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    void Update()
    {
        if (_isDissolvingTail)
        {
            AdvanceVirtualHead(); // keeps pulling tail parts toward the checkpoint
            UpdateTail();         // snake motion continues on the advancing path
            DissolveTail();       // disables front parts and spawns balls
            return;
        }

        if (reachedEnd) return;

        MoveHead();
        RecordHeadPath();
        UpdateTail();
    }

    // ─── Head Movement ────────────────────────────────────────────────────────

    void MoveHead()
    {
        /*if (currentCheckpointIndex >= checkpoints.Count)
        {
            _lastCheckpointWorldPos = checkpoints[checkpoints.Count - 1].position;

            // Direction the arrow was travelling when it arrived — used to push
            // the virtual head past the checkpoint so the tail follows it in.
            if (checkpoints.Count >= 2)
                _dissolveDirection = (checkpoints[checkpoints.Count - 1].position
                                    - checkpoints[checkpoints.Count - 2].position).normalized;
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

        Vector3 target = checkpoints[currentCheckpointIndex].position;
        Vector3 dir = target - _arrowHead.position;

        _arrowHead.position = Vector3.MoveTowards(
            _arrowHead.position, target, moveSpeed * Time.deltaTime);

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
            Quaternion offset = Quaternion.Euler(headRotationOffset);
            _arrowHead.rotation = Quaternion.Slerp(
                _arrowHead.rotation, lookRot * offset, rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(_arrowHead.position, target) < 0.05f)
            currentCheckpointIndex++;*/

        var pos = _arrowHead.position;
        bool outOfBounds = pos.x < _xMin || pos.x > _xMax || pos.y < _yMin || pos.y > _yMax;

        if (outOfBounds && !checkpointsUpdated)
        {
            if(pos.x < _xMin)
            {
                //go towards checkpoint 4 then 5 then 8
            }
            if (pos.x > _xMax)
            {
                //go towards checkpoint 6 then 5 then 8
            }
            if (pos.y < _yMin)
            {
                //go towards checkpoint 5 then 8
            }
            if (pos.y > _yMax)
            {
                if (Vector3.Distance(pos, checkpoints[0].position) >= Vector3.Distance(pos, checkpoints[2].position))
                {
                    //go towards checkpoint 2 then 4 then 5 then 8
                }
                else
                {
                    //go towards checkpoint 0 then 6 then 5 then 8
                }
            }
            checkpointsUpdated = true;
        }
        else
        {
            var y = _arrowHead.localPosition.y;
            y += moveSpeed * Time.deltaTime;
            _arrowHead.localPosition = new Vector3(0, y, 0);
            return;
        }

        if (currentCheckpointIndex >= checkpoints.Count)
        {
            _lastCheckpointWorldPos = checkpoints[checkpoints.Count - 1].position;

            // Direction the arrow was travelling when it arrived — used to push
            // the virtual head past the checkpoint so the tail follows it in.
            if (checkpoints.Count >= 2)
                _dissolveDirection = (checkpoints[checkpoints.Count - 1].position
                                    - checkpoints[checkpoints.Count - 2].position).normalized;
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

        Vector3 target = checkpoints[currentCheckpointIndex].position;
        Vector3 dir = target - _arrowHead.position;

        _arrowHead.position = Vector3.MoveTowards(
            _arrowHead.position, target, moveSpeed * Time.deltaTime);

        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
            Quaternion offset = Quaternion.Euler(headRotationOffset);
            _arrowHead.rotation = Quaternion.Slerp(
                _arrowHead.rotation, lookRot * offset, rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(_arrowHead.position, target) < 0.05f)
            currentCheckpointIndex++;
    }

    // ─── Path Recording ───────────────────────────────────────────────────────

    void RecordHeadPath()
    {
        Vector3 last = positionHistory[positionHistory.Count - 1];
        if (Vector3.Distance(_arrowHead.position, last) >= pathRecordInterval)
            positionHistory.Add(_arrowHead.position);
    }

    // ─── Virtual Head (used during dissolve) ─────────────────────────────────

    /// <summary>
    /// Moves an invisible point past the last checkpoint at the same speed
    /// as the arrow. Appending its positions to positionHistory means
    /// UpdateTail() will naturally pull every tail part forward toward the
    /// checkpoint, exactly as if the head were still leading them.
    /// </summary>
    void AdvanceVirtualHead()
    {
        _virtualHeadPos += _dissolveDirection * moveSpeed * Time.deltaTime;

        Vector3 last = positionHistory[positionHistory.Count - 1];
        if (Vector3.Distance(_virtualHeadPos, last) >= pathRecordInterval)
            positionHistory.Add(_virtualHeadPos);
    }

    // ─── Tail Snake Update ────────────────────────────────────────────────────

    void UpdateTail()
    {
        if (positionHistory.Count < 2) return;

        for (int i = 0; i < _tailParts.Count; i++)
        {
            if (!_tailParts[i].gameObject.activeSelf) continue;

            float distBehind = (i + 1) * _tailPartSpacing;
            float distAhead = i * _tailPartSpacing;

            Vector3 partPos = SamplePathAtDistance(distBehind);
            Vector3 partFront = SamplePathAtDistance(distAhead);

            _tailParts[i].position = partPos;

            Vector3 dir = partFront - partPos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                _tailParts[i].rotation = Quaternion.LookRotation(dir.normalized)
                                       * Quaternion.Euler(90f, 0f, 0f);
            }
        }
    }

    // ─── Tail Dissolve ────────────────────────────────────────────────────────

    void DissolveTail()
    {
        float timePerPart = _tailPartSpacing / moveSpeed;
        _dissolveTimer += Time.deltaTime;

        while (_dissolveTimer >= timePerPart)
        {
            _dissolveTimer -= timePerPart;

            // Skip parts that are already inactive
            while (_nextTailToDisable < _tailParts.Count
                   && !_tailParts[_nextTailToDisable].gameObject.activeSelf)
            {
                _nextTailToDisable++;
            }

            if (_nextTailToDisable >= _tailParts.Count)
            {
                _isDissolvingTail = false;
                return;
            }

            _tailParts[_nextTailToDisable].gameObject.SetActive(false);
            _nextTailToDisable++;
            _disabledTailCount++;

            if (_disabledTailCount % _tailPartsPerBall == 0)
                ActivateNextBall();
        }
    }

    void ActivateNextBall()
    {
        if (_activatedBallCount >= _balls.Count)
        {
            Debug.LogWarning($"Arrow: Tried to activate ball #{_activatedBallCount + 1} " +
                             $"but only {_balls.Count} balls exist in _ballHolder.");
            return;
        }

        Transform ball = _balls[_activatedBallCount];
        ball.position = _lastCheckpointWorldPos;
        ball.gameObject.SetActive(true);

        _activatedBallCount++;
    }

    // ─── Path Sampling ────────────────────────────────────────────────────────

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

    // ─── Gizmos ───────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (checkpoints == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < checkpoints.Count - 1; i++)
        {
            if (checkpoints[i] != null && checkpoints[i + 1] != null)
                Gizmos.DrawLine(checkpoints[i].position, checkpoints[i + 1].position);
        }

        Gizmos.color = Color.cyan;
        foreach (var cp in checkpoints)
            if (cp != null) Gizmos.DrawWireSphere(cp.position, 0.2f);

        if (!Application.isPlaying) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < positionHistory.Count - 1; i++)
            Gizmos.DrawLine(positionHistory[i], positionHistory[i + 1]);
    }
}