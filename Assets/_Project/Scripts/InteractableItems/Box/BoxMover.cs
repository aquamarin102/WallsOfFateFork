using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoxMover : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float followSharpness = 20f;
        [SerializeField] private float maxSpeed = 4f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float lateralCorrectionMaxSpeed = 0.2f;

        [Header("Held Physics Settings")]
        [SerializeField] private float heldLinearDamping = 8f;
        [SerializeField] private float heldAngularDamping = 8f;
        [SerializeField] private float releaseVelocityDamping = 0.35f;


        private Rigidbody rb;
        private PlayerMoveController playerMoveController;
        private Transform playerTransform;
        private bool isBeingHeld;
        private Vector3 moveAxis;
        private Vector3 grabOffset;
        private Vector3 desiredPlanarVelocity;
        private Collider boxCollider;
        private Collider playerCollider;
        private bool originalUseGravity;
        private float originalLinearDamping;
        private float originalAngularDamping;
        private RigidbodyInterpolation originalInterpolation;
        private CollisionDetectionMode originalCollisionDetectionMode;
        private bool physicsStateCached;

        [Inject]
        private void Construct(PlayerMoveController playerMoveController)
        {
            this.playerMoveController = playerMoveController;
            playerTransform = playerMoveController.gameObject.transform;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            boxCollider = GetComponent<Collider>();
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (!isBeingHeld || playerTransform == null) return;

            Vector3 planarMoveAxis = GetPlanarMoveAxis();
            Vector3 targetVelocity = Vector3.zero;
            if (planarMoveAxis.sqrMagnitude > 0.0001f)
            {
                float axialSpeed = Vector3.Dot(desiredPlanarVelocity, planarMoveAxis);
                targetVelocity = planarMoveAxis * axialSpeed;
            }

            Vector3 currentVelocity = rb.linearVelocity;
            Vector3 currentPlanarVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 nextPlanarVelocity = Vector3.MoveTowards(
                currentPlanarVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);

            if (planarMoveAxis.sqrMagnitude > 0.0001f)
            {
                Vector3 axialVelocity = planarMoveAxis * Vector3.Dot(nextPlanarVelocity, planarMoveAxis);
                Vector3 lateralVelocity = nextPlanarVelocity - axialVelocity;
                lateralVelocity = Vector3.ClampMagnitude(lateralVelocity, lateralCorrectionMaxSpeed);
                nextPlanarVelocity = axialVelocity + lateralVelocity;
            }

            // Keep grounded while held and ignore upward impulses from collisions.
            float verticalVelocity = HasFrozenYPosition()
                ? 0f
                : Mathf.Min(currentVelocity.y, 0f);

            rb.linearVelocity = new Vector3(nextPlanarVelocity.x, verticalVelocity, nextPlanarVelocity.z);

            //Vector3 moveDirection = (targetPos - transform.position).normalized;
            //float distance = Vector3.Distance(transform.position, targetPos);
            //Vector3 force = moveDirection * (distance * maxForse);
            //rb.AddForce(force, ForceMode.Force);

            //if (rb.linearVelocity.magnitude > maxSpeed)
            //{
            //    rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            //}
            // Целевая позиция с сохранением исходной высоты коробки
            //rb.MovePosition(targetPos);


            // Рассчитываем силу пропорционально расстоянию

            // Применяем силу

            //Ограничиваем максимальную скорость

            //Vector3 playerDelta = playerTransform.position - transform.position;

            //float moveInput = Vector3.Dot(playerDelta.normalized, moveAxis);

            //RotateWithPlayer();
        }

        public void StartHolding()
        {
            if (isBeingHeld || playerTransform == null)
                return;

            isBeingHeld = true;
            CachePhysicsState();
            ApplyHeldPhysicsState();
            desiredPlanarVelocity = Vector3.zero;
            playerCollider = playerTransform.GetComponent<Collider>();

            Vector3 playerPos = playerTransform.position;
            Vector3 boxPos = transform.position;
            
            Vector3 playerPosXZ = new Vector3(playerPos.x, 0, playerPos.z);
            Vector3 boxPosXZ = new Vector3(boxPos.x, 0, boxPos.z);
            Vector3 playerOffsetFromBox = playerPosXZ - boxPosXZ;

            moveAxis = transform.forward;
            BoxGripPoints gripPoints = GetComponent<BoxGripPoints>();
            Transform closestPoint = null;
            if (gripPoints != null && gripPoints.points.Length == 4)
            {
                float minDist = float.MaxValue;
                foreach (Transform point in gripPoints.points)
                {
                    if (point == null) continue;
                    float dist = Vector3.Distance(playerTransform.position, point.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestPoint = point;
                    }
                }

                if (closestPoint != null)
                {
                    int index = System.Array.IndexOf(gripPoints.points, closestPoint);
                    if (index == 0 || index == 1)
                    {
                        //moveAxis = transform.forward; // ось вперёд/назад
                        moveAxis = gripPoints.points[0].position - gripPoints.points[1].position;
                    }
                    else 
                    {
                        //moveAxis = transform.right; // ось влево/вправо
                        moveAxis = gripPoints.points[2].position - gripPoints.points[3].position;
                    }
                }
            }
            //else
            //{
            //    // Запасной вариант – как раньше, по смещению
            //    Vector3 playerPosXZ = new Vector3(playerPos.x, 0, playerPos.z);
            //    Vector3 boxPosXZ = new Vector3(boxPos.x, 0, boxPos.z);
            //    grabOffset = boxPosXZ - playerPosXZ;
            //}

            Vector3 faceDirection = playerOffsetFromBox.sqrMagnitude > 0.0001f
                ? playerOffsetFromBox.normalized
                : GetPlanarMoveAxis();

            if (closestPoint != null)
            {
                Vector3 closestPointOffset = closestPoint.position - transform.position;
                closestPointOffset.y = 0f;
                if (closestPointOffset.sqrMagnitude > 0.0001f)
                    faceDirection = closestPointOffset.normalized;
            }

            playerOffsetFromBox = faceDirection * GetHeldPlayerDistance(faceDirection);
            grabOffset = -playerOffsetFromBox;

            if (playerCollider != null && boxCollider != null)
                Physics.IgnoreCollision(boxCollider, playerCollider, true);

            playerMoveController?.InBoxGrabMode(this, moveAxis.normalized, playerOffsetFromBox);

            rb.freezeRotation = true;
            rb.WakeUp();
        }

        public void StopHolding()
        {
            if (!isBeingHeld)
                return;

            isBeingHeld = false;
            desiredPlanarVelocity = Vector3.zero;

            if (playerCollider != null && boxCollider != null)
                Physics.IgnoreCollision(boxCollider, playerCollider, false);

            rb.freezeRotation = false;
            DampenReleaseVelocity();
            RestorePhysicsState();
            playerMoveController?.StopBoxGrabMode();
        }

        private void RotateWithPlayer()
        {
            Vector3 boxRotation = transform.eulerAngles;
            boxRotation.y = playerTransform.eulerAngles.y;
            rb.MoveRotation(Quaternion.Euler(boxRotation));
        }

        private bool HasFrozenYPosition()
        {
            return (rb.constraints & RigidbodyConstraints.FreezePositionY) != 0;
        }

        private Vector3 GetPlanarMoveAxis()
        {
            Vector3 planarAxis = new Vector3(moveAxis.x, 0f, moveAxis.z);
            if (planarAxis.sqrMagnitude > 0.0001f)
                return planarAxis.normalized;

            Vector3 fallbackAxis = new Vector3(transform.forward.x, 0f, transform.forward.z);
            return fallbackAxis.sqrMagnitude > 0.0001f
                ? fallbackAxis.normalized
                : Vector3.forward;
        }

        private void CachePhysicsState()
        {
            if (physicsStateCached)
                return;

            originalUseGravity = rb.useGravity;
            originalLinearDamping = rb.linearDamping;
            originalAngularDamping = rb.angularDamping;
            originalInterpolation = rb.interpolation;
            originalCollisionDetectionMode = rb.collisionDetectionMode;
            physicsStateCached = true;
        }

        private void ApplyHeldPhysicsState()
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = heldLinearDamping;
            rb.angularDamping = heldAngularDamping;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void RestorePhysicsState()
        {
            if (!physicsStateCached)
                return;

            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = originalUseGravity;
            rb.linearDamping = originalLinearDamping;
            rb.angularDamping = originalAngularDamping;
            rb.interpolation = originalInterpolation;
            rb.collisionDetectionMode = originalCollisionDetectionMode;
            physicsStateCached = false;
        }

        private void DampenReleaseVelocity()
        {
            Vector3 velocity = rb.linearVelocity;
            Vector3 planarVelocity = new Vector3(velocity.x, 0f, velocity.z) * releaseVelocityDamping;
            float verticalVelocity = HasFrozenYPosition()
                ? 0f
                : Mathf.Min(velocity.y, 0f);

            rb.linearVelocity = new Vector3(planarVelocity.x, verticalVelocity, planarVelocity.z);
            rb.angularVelocity *= releaseVelocityDamping;
        }

        private float GetHeldPlayerDistance(Vector3 faceDirection)
        {
            float playerRadius = 0.45f;
            CharacterController controller = playerMoveController != null
                ? playerMoveController.GetComponent<CharacterController>()
                : null;
            if (controller != null)
                playerRadius = controller.radius + controller.skinWidth + 0.05f;

            if (boxCollider == null)
                return playerRadius + 0.5f;

            Vector3 extents = boxCollider.bounds.extents;
            float surfaceOffset =
                Mathf.Abs(faceDirection.x) * extents.x +
                Mathf.Abs(faceDirection.z) * extents.z;

            return surfaceOffset + playerRadius;
        }

        public void SetDesiredPlanarVelocity(Vector3 velocity)
        {
            velocity.y = 0f;
            desiredPlanarVelocity = Vector3.ClampMagnitude(velocity, maxSpeed);
        }

        public Vector3 Position => rb != null ? rb.position : transform.position;
        public float CurrentPlanarSpeed => rb == null ? 0f : new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;

        public bool IsBeingHeld => isBeingHeld;
    }
}
