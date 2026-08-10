using Fusion;
using UnityEngine;

public class BodyController : NetworkBehaviour
{
    [Header("Objects")]
    public LegManager LeftLeg;
    public LegManager RightLeg;
    public ArmManager LeftArm;
    public ArmManager RightArm;

    [Header("Limb Anchors")]
    public GameObject sholderL;
    public GameObject sholderR;
    public GameObject pelvisL;
    public GameObject pelvisR;

    [Header("Ground State")]
    [SerializeField] bool leftFootGrounded;
    [SerializeField] bool rightFootGrounded;
    [SerializeField] bool isFootsGrounded;

    [Header("Movement")]
    [SerializeField] float speed = 5f;

    Vector3 spawnPosition;
    Rigidbody2D body;

    public override void Spawned()
    {
        spawnPosition = transform.position;
        body = GetComponent<Rigidbody2D>();

        if (Object.HasStateAuthority)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.HasStateAuthority == false) return;
        if (body == null || LeftLeg == null || RightLeg == null || LeftArm == null || RightArm == null) return;

        // 테스트용 초기 위치 복귀
        if (Input.GetKeyDown(KeyCode.R))
        {
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        // 모든 입력과 물리를 호스트에서 다리 → 골반 → 팔 순서로 계산한다.
        SimulateLeg(LeftLeg);
        SimulateLeg(RightLeg);

        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;
        isFootsGrounded = leftFootGrounded || rightFootGrounded;

        if (isFootsGrounded == false)
        {
            body.gravityScale = 1f;
            SimulateArms();
            return;
        }

        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;

        Vector2 pelvisPull = Vector2.zero;
        int pullingLegCount = 0;

        if (LeftLeg.PelvisPull.sqrMagnitude > 0.0001f)
        {
            pelvisPull += LeftLeg.PelvisPull;
            pullingLegCount++;
        }

        if (RightLeg.PelvisPull.sqrMagnitude > 0.0001f)
        {
            pelvisPull += RightLeg.PelvisPull;
            pullingLegCount++;
        }

        if (pullingLegCount > 0)
        {
            // 두 다리가 동시에 당길 때는 한쪽이 다른 쪽을 덮지 않도록 평균을 사용한다.
            pelvisPull /= pullingLegCount;
            Vector2 movement = Vector2.ClampMagnitude(pelvisPull, speed * Runner.DeltaTime);
            body.MovePosition(body.position + movement);
        }

        SimulateArms();
    }

    void SimulateLeg(LegManager leg)
    {
        if (Runner.TryGetInputForPlayer(leg.Object.InputAuthority, out NetworkInputData input))
        {
            leg.SimulateHost(input.MouseWorldPos);
        }
    }

    void SimulateArms()
    {
        SimulateArm(LeftArm);
        SimulateArm(RightArm);
    }

    void SimulateArm(ArmManager arm)
    {
        if (Runner.TryGetInputForPlayer(arm.Object.InputAuthority, out NetworkInputData input))
        {
            arm.SimulateHost(input.MouseWorldPos);
        }
    }
}
