using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

public class PlayerBoxGrabber : MonoBehaviour
{
    #region Inspector
    [Header("Grab Settings")]
    [SerializeField] private float grabRange = 2.0f;
    [SerializeField] private KeyCode grabKey = KeyCode.E;

    [Header("Click-to-Grab Settings")]
    [SerializeField] private float clickRayLength = 100f;
    [Tooltip("Mask for objects considered as boxes on raycast")] public LayerMask boxMask = ~0; // by default everything
    [Tooltip("Если true, игрок автоматически подбежит к выбранной коробке и схватит её")] [SerializeField]
    private bool autoRunToClickedBox = true;

    [Header("Box Movement Settings")]
    [SerializeField] private float maxSpeed = 5f;
    #endregion

    // ─────────── runtime ───────────
    public Transform attachedBox { get; private set; }

    private int originalBoxLayer;
    private Vector3 localGrabOffset = Vector3.zero;
    private float currentSpeed = 0f;

    private Rigidbody attachedRb;
    private bool wasKinematic;

    private enum GrabAxis { ForwardBack, LeftRight }
    private GrabAxis grabAxis = GrabAxis.ForwardBack;

    private NavMeshAgent agent;
    private Coroutine moveToCoroutine;
    // ────────────────────────────────

    private void Awake() => agent = GetComponent<NavMeshAgent>();

    private void Update()
    {
        /*========== HOTKEYS ==========*/
        if (Input.GetKeyDown(grabKey)) ToggleGrabWithE();
        if (Input.GetMouseButtonDown(0) && !IsPointerOverUi()) HandleMouseClick();

        if (attachedBox == null) return; // no box grabbed ⇒ skip move logic

        /*====== MOVE BOX WHILE HOLDING ======*/
        float vertical = Input.GetAxis("Vertical");
        float horizontal = Input.GetAxis("Horizontal");

        float moveInput = (grabAxis == GrabAxis.LeftRight) ? horizontal : vertical;
        currentSpeed = Mathf.Abs(moveInput) > 0.1f ? moveInput * maxSpeed : 0f;

        Vector3 moveDir = (grabAxis == GrabAxis.LeftRight) ? attachedBox.right : attachedBox.forward;
        attachedBox.position += moveDir * currentSpeed * Time.deltaTime;

        transform.position = attachedBox.TransformPoint(localGrabOffset);

        Vector3 lookDir = attachedBox.position - transform.position; lookDir.y = 0f;
        if (lookDir != Vector3.zero) transform.rotation = Quaternion.LookRotation(lookDir);
    }

    /*========== PUBLIC API ==========*/
    public bool IsHoldingBox => attachedBox != null;

    public void ToggleGrab(Transform box)
    {
        if (attachedBox == box) DetachBox();
        else AttachBox(box);
    }

    /*========== E KEY ==========*/
    private void ToggleGrabWithE()
    {
        if (attachedBox == null) TryAttachNearestBox(); else DetachBox();
    }

    /*========== MOUSE CLICK ==========*/
    private void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickRayLength, boxMask)) return;
        if (!hit.collider.CompareTag("Box")) return;

        Transform clickedBox = hit.collider.transform;

        float distance = Vector3.Distance(transform.position, clickedBox.position);

        if (attachedBox == null)
        {
            // 1) если уже в радиусе – схватываем сразу
            if (distance <= grabRange + 0.1f)
            {
                AttachBox(clickedBox);
                return;
            }

            // 2) иначе бежим, если можно
            if (autoRunToClickedBox && agent && agent.isOnNavMesh)
            {
                if (moveToCoroutine != null) StopCoroutine(moveToCoroutine);
                moveToCoroutine = StartCoroutine(MoveToBoxAndGrab(clickedBox));
            }
            else
            {
                // 3) нет NavMeshAgent / авто-бег выключен – просто телепортируемся
                //Debug.LogWarning("Player not on NavMesh – хватание без подбега.");
                transform.position = clickedBox.position - (clickedBox.forward * grabRange * 0.5f);
                AttachBox(clickedBox);
            }
        }
        else if (attachedBox == clickedBox)
        {
            DetachBox();
        }
    }

    private IEnumerator MoveToBoxAndGrab(Transform target)
    {
        if (!agent || !agent.isOnNavMesh)
        {
            AttachBox(target); yield break;
        }

        agent.isStopped = false;
        agent.SetDestination(target.position);

        // ►  ЖДЁМ, пока действительно не подойдём
        while (true)
        {
            // если путь построен и осталось пройти больше grabRange
            if (!agent.pathPending &&
                agent.remainingDistance <= grabRange + 0.8f)
                break;                      // достаточно близко

            yield return null;              // ждём следующий кадр
        }

        agent.ResetPath();

        // контрольная проверка
        if (Vector3.Distance(transform.position, target.position) <= grabRange + 0.8f)
            AttachBox(target);
        //else Debug.Log("[BoxGrabber] Too far to grab — " + Vector3.Distance(transform.position, target.position));
    }

    /*========== FIND & ATTACH ==========*/
    private void TryAttachNearestBox()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position, grabRange, boxMask);
        foreach (Collider col in cols)
        {
            if (!col.CompareTag("Box")) continue;
            AttachBox(col.transform); return;
        }
        //Debug.Log("[BoxGrabber] Box in grab range not found.");
    }

    private void AttachBox(Transform box)
    {
        if (!box) return; if (attachedBox == box) return;

        attachedBox = box; originalBoxLayer = attachedBox.gameObject.layer;
        attachedBox.gameObject.layer = LayerMask.NameToLayer("MovableBox");

        localGrabOffset = attachedBox.InverseTransformPoint(transform.position);
        grabAxis = Mathf.Abs(localGrabOffset.x) > Mathf.Abs(localGrabOffset.z) ? GrabAxis.LeftRight : GrabAxis.ForwardBack;

        currentSpeed = 0f; transform.SetParent(attachedBox);

        attachedRb = attachedBox.GetComponent<Rigidbody>();
        if (attachedRb)
        {
            wasKinematic = attachedRb.isKinematic; attachedRb.isKinematic = false;
        }

        //Debug.Log($"[BoxGrabber] Grabbed {attachedBox.name} ({grabAxis})");
    }

    private void DetachBox()
    {
        if (!attachedBox) return;

        if (attachedRb) attachedRb.isKinematic = wasKinematic;

        attachedBox.gameObject.layer = originalBoxLayer; transform.SetParent(null);
        //Debug.Log($"[BoxGrabber] Released {attachedBox.name}");

        attachedBox = null; currentSpeed = 0f;
    }

    /*========== GIZMOS ==========*/
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, grabRange);
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
