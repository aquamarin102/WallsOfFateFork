using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsPushingHash = Animator.StringToHash("IsPushing");
    private static readonly int PushSpeedHash = Animator.StringToHash("PushSpeed");
    private static readonly int PickupFloorHash = Animator.StringToHash("PickupFloor");
    private static readonly int PickupBodyHash = Animator.StringToHash("PickupBody");
    private static readonly int OpenChestHash = Animator.StringToHash("OpenChest");

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void ApplyLocomotion(float normalizedSpeed, bool isPushing, float pushSpeed)
    {
        animator.SetFloat(SpeedHash, normalizedSpeed);
        animator.SetBool(IsPushingHash, isPushing);
        animator.SetFloat(PushSpeedHash, pushSpeed);
    }

    public void PlayPickupFloor() => animator.SetTrigger(PickupFloorHash);

    public void PlayPickupBody() => animator.SetTrigger(PickupBodyHash);

    public void PlayOpenChest() => animator.SetTrigger(OpenChestHash);
}
