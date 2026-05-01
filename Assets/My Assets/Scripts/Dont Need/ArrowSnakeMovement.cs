using System.Collections.Generic;
using UnityEngine;

public class ArrowSnakeMovement : MonoBehaviour
{
    [Header("Arrow Parts")]
    public Transform arrowHead;          // Assign your Cone here
    public Transform arrowTail;          // Assign your Cylinder here

    [Header("Checkpoints")]
    public List<Transform> checkpoints;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 8f;

    [Header("Tail Settings")]
    [Tooltip("How long the tail stretches behind the head")]
    public float tailLength = 2f;
    [Tooltip("Distance between recorded path points — lower = smoother snake curve")]
    public float pathRecordInterval = 0.05f;

    [Header("Head Rotation Offset")]
    [Tooltip("Adjust if your cone model's tip doesn't point along Z-forward")]
    public Vector3 headRotationOffset = Vector3.zero; // e.g. (90, 0, 0) if tip points up

    // Internals
    private List<Vector3> positionHistory = new List<Vector3>();
    private int currentCheckpointIndex = 0;
    private bool reachedEnd = false;
    private float originalTailScaleX;
    private float originalTailScaleZ;

    void Start()
    {
        if (arrowHead == null || arrowTail == null)
        {
            Debug.LogError("ArrowSnakeMovement: Assign arrowHead and arrowTail in the Inspector!");
            return;
        }

        positionHistory.Add(arrowHead.position);

        // Cache original tail width so we only modify the length (Y scale)
        originalTailScaleX = arrowTail.localScale.x;
        originalTailScaleZ = arrowTail.localScale.z;
    }

    void Update()
    {
        if (reachedEnd) return;

        MoveHead();
        RecordHeadPath();
        UpdateTail();
    }

    // ─── Head ────────────────────────────────────────────────────────────────

    void MoveHead()
    {
        if (currentCheckpointIndex >= checkpoints.Count)
        {
            reachedEnd = true;
            return;
        }

        Vector3 target = checkpoints[currentCheckpointIndex].position;
        Vector3 dir = target - arrowHead.position;

        // Translate
        arrowHead.position = Vector3.MoveTowards(
            arrowHead.position, target, moveSpeed * Time.deltaTime);

        // Rotate to face movement direction
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir.normalized);
            Quaternion offset = Quaternion.Euler(headRotationOffset);
            arrowHead.rotation = Quaternion.Slerp(
                arrowHead.rotation, lookRot * offset, rotationSpeed * Time.deltaTime);
        }

        // Advance checkpoint
        if (Vector3.Distance(arrowHead.position, target) < 0.05f)
            currentCheckpointIndex++;
    }

    // ─── Path Recording ──────────────────────────────────────────────────────

    void RecordHeadPath()
    {
        Vector3 last = positionHistory[positionHistory.Count - 1];
        if (Vector3.Distance(arrowHead.position, last) >= pathRecordInterval)
            positionHistory.Add(arrowHead.position);
    }

    // ─── Tail ────────────────────────────────────────────────────────────────

    void UpdateTail()
    {
        if (positionHistory.Count < 2) return;

        // Tail front = where head base is right now
        // Tail back  = `tailLength` units behind along the recorded path
        Vector3 tailFront = arrowHead.position;
        Vector3 tailBack = SamplePathAtDistance(tailLength);

        Vector3 midpoint = (tailFront + tailBack) * 0.5f;
        Vector3 direction = tailFront - tailBack;
        float length = direction.magnitude;

        if (length < 0.001f) return;

        // Position at midpoint
        arrowTail.position = midpoint;

        // Orient: Unity's cylinder is 2 units tall along Y — rotate so Y aligns with direction
        arrowTail.rotation = Quaternion.LookRotation(direction.normalized)
                           * Quaternion.Euler(90f, 0f, 0f);

        // Scale Y so the cylinder spans exactly `length`
        // (Unity cylinder height = 2 × localScale.y, hence × 0.5f)
        arrowTail.localScale = new Vector3(
            originalTailScaleX,
            length * 0.5f,
            originalTailScaleZ);
    }

    /// <summary>
    /// Walks backwards along positionHistory and returns the world position
    /// that is exactly `distance` units behind the most recent point.
    /// </summary>
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

        // Clamp to start if path is shorter than tailLength
        return positionHistory[0];
    }

    // ─── Editor Gizmos ───────────────────────────────────────────────────────

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

        // Draw recorded path in Play mode
        if (!Application.isPlaying) return;
        Gizmos.color = Color.green;
        for (int i = 0; i < positionHistory.Count - 1; i++)
            Gizmos.DrawLine(positionHistory[i], positionHistory[i + 1]);
    }
}