using Fusion;
using UnityEngine;

public class BodyController : NetworkBehaviour, IAfterTick
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
    [SerializeField] float speed = 5f;  // 지지 다리가 골반에 요청한 이동을 적용할 최대 속도
    [SerializeField] float movementResponse = 10f; // 목표 수평 속도에 도달하는 반응 속도
    [SerializeField] float maxMovementAcceleration = 25f; // 걷기 입력이 몸통에 가할 수 있는 최대 수평 가속도
    [SerializeField] float movementDeadZone = 0.05f; // 작은 마우스 떨림을 이동으로 바꾸지 않는 입력 거리
    [SerializeField, Range(0f, 1f)] float minLegExtensionToMove = 0.85f;    // 걷기 이동에 사용할 수 있는 최소 다리 펴짐 비율
    [SerializeField] float minFootBelowPelvis = 0.5f;   // 수평으로 벌어진 다리가 몸통을 움직이지 못하게 하는 최소 높이 차이

    [Header("Ground Support")]
    [SerializeField] float supportBlendSpeed = 8f; // 발 접촉이 몸통 지지력으로 이어지는 속도
    [SerializeField] float supportVelocityResponse = 10f; // 지지 중 목표 수직 속도를 따라가는 반응 속도
    [SerializeField] float maxSupportAcceleration = 30f; // 착지와 기립 때 몸통에 가할 수 있는 최대 수직 가속도

    [Header("Standing")]
    [SerializeField] float standSpeed = 3f; // 양발이 함께 바닥을 누를 때 골반이 상승하는 최대 속도
    [SerializeField] float minStandPressure = 0.15f;    // 기립 입력으로 인정할 각 다리의 최소 아래쪽 압력
    [SerializeField, Range(0f, 1f)] float targetStandingExtension = 0.92f;  // 완전히 펴지는 것을 피하기 위한 기립 완료 다리 길이 비율

    Vector3 spawnPosition;
    Rigidbody2D body;

    // 재시뮬레이션에서도 같은 지지력 변화가 나오도록 틱 상태로 동기화한다.
    [Networked] float SupportBlend { get; set; }

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
            SupportBlend = 0f;
        }

        // 모든 입력과 물리를 호스트에서 다리 → 골반 → 팔 순서로 계산한다.
        SimulateLeg(LeftLeg);
        SimulateLeg(RightLeg);

        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;
        isFootsGrounded = leftFootGrounded || rightFootGrounded;

        // 중력은 항상 유지하고 발이 닿고 떨어질 때 지지력만 부드럽게 변화시킨다.
        body.gravityScale = 1f;
        SupportBlend = Mathf.MoveTowards(SupportBlend, isFootsGrounded ? 1f : 0f, supportBlendSpeed * Runner.DeltaTime);

        Vector2 pelvisPull = Vector2.zero;
        int pullingLegCount = 0;

        if (LeftLeg.TryGetSupportedPelvisPull(minLegExtensionToMove, minFootBelowPelvis, out Vector2 leftPelvisPull))
        {
            pelvisPull += leftPelvisPull;
            pullingLegCount++;
        }

        if (RightLeg.TryGetSupportedPelvisPull(minLegExtensionToMove, minFootBelowPelvis, out Vector2 rightPelvisPull))
        {
            pelvisPull += rightPelvisPull;
            pullingLegCount++;
        }

        // 두 다리가 동시에 당길 때는 한쪽이 다른 쪽을 덮지 않도록 평균을 사용한다.
        if (pullingLegCount > 0) pelvisPull /= pullingLegCount;

        float desiredHorizontalVelocity = Mathf.Abs(pelvisPull.x) >= movementDeadZone ? Mathf.Clamp(pelvisPull.x, -1f, 1f) * speed : 0f;
        float desiredVerticalVelocity = TryGetStandingVelocity(out float standingVelocity) ? standingVelocity : 0f;
        ApplyContinuousBodyPhysics(desiredHorizontalVelocity, desiredVerticalVelocity);

        SimulateArms();
    }

    /// <summary>
    /// Rigidbody2D 이동이 반영된 뒤 다리 루트와 관절을 새 골반 위치에 맞춰 다시 확정합니다.
    /// </summary>
    public void AfterTick()
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (LeftLeg == null || RightLeg == null) return;

        LeftLeg.RefreshHostPoseAfterPelvisMove();
        RightLeg.RefreshHostPoseAfterPelvisMove();
    }

    /// <summary>
    /// 양발이 모두 고정되고 두 플레이어가 충분히 바닥을 누를 때 사용할 상승 속도를 계산합니다.
    /// 두 다리 중 압력이 약하거나 상승 여유가 작은 쪽을 기준으로 속도를 제한합니다.
    /// </summary>
    bool TryGetStandingVelocity(out float standingVelocity)
    {
        standingVelocity = 0f;
        if (!LeftLeg.TryGetStandingSupport(targetStandingExtension, minStandPressure, out float leftPressure, out float leftRiseDistance)) return false;
        if (!RightLeg.TryGetStandingSupport(targetStandingExtension, minStandPressure, out float rightPressure, out float rightRiseDistance)) return false;

        // 한 명만 강하게 눌러도 일어서지 않도록 두 입력 중 작은 값을 사용한다.
        float sharedPressure = Mathf.Min(leftPressure, rightPressure);
        float availableRiseDistance = Mathf.Min(leftRiseDistance, rightRiseDistance);
        standingVelocity = Mathf.Min(availableRiseDistance / Runner.DeltaTime, standSpeed * Mathf.Clamp01(sharedPressure));
        return standingVelocity > 0f;
    }

    /// <summary>
    /// 중력을 끄거나 위치를 순간 이동하지 않고, 발의 지지력과 목표 속도를 연속적인 힘으로 적용합니다.
    /// </summary>
    void ApplyContinuousBodyPhysics(float desiredHorizontalVelocity, float desiredVerticalVelocity)
    {
        if (SupportBlend <= 0f) return;

        float horizontalAcceleration = Mathf.Clamp((desiredHorizontalVelocity - body.linearVelocity.x) * movementResponse, -maxMovementAcceleration, maxMovementAcceleration);
        float verticalAcceleration = Mathf.Clamp((desiredVerticalVelocity - body.linearVelocity.y) * supportVelocityResponse, -maxSupportAcceleration, maxSupportAcceleration);
        float gravityCompensation = -Physics2D.gravity.y * body.gravityScale;
        Vector2 supportAcceleration = new Vector2(horizontalAcceleration, gravityCompensation + verticalAcceleration) * SupportBlend;

        body.AddForce(supportAcceleration * body.mass, ForceMode2D.Force);
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
