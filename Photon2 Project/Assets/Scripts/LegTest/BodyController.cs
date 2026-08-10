using Fusion;
using UnityEngine;

/// <summary>
/// 상태 권한을 가진 호스트에서 양쪽 다리 입력, 몸통 물리, 양쪽 팔 입력을 순서대로 실행합니다.
/// 게스트는 이 계산을 실행하지 않고 호스트가 동기화한 몸통과 사지의 결과를 화면에 표시합니다.
/// </summary>
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
    // 아래 값은 각 다리의 발이 땅에 닿았는지를 한 틱 동안 모아서 보여주는 디버그용 상태다.
    [SerializeField] bool leftFootGrounded;
    [SerializeField] bool rightFootGrounded;
    [SerializeField] bool isFootsGrounded;

    [Header("Movement")]
    [SerializeField] float speed = 5f;                                  // 골반 견인 입력으로 만들 수 있는 최대 수평 속도
    [SerializeField] float movementResponse = 10f;                      // 이 값이 클수록 현재 속도가 목표 수평 속도를 더 빠르게 따라잡는다
    [SerializeField] float maxMovementAcceleration = 25f;               // 위 값으로 계산한 힘이 한 틱에 낼 수 있는 최대 수평 가속도(이보다 세게는 못 민다)
    [SerializeField] float movementDeadZone = 0.05f;                    // 이 거리보다 작은 골반 견인 입력은 마우스 떨림으로 간주한다
    [SerializeField, Range(0f, 1f)] float minLegExtensionToMove = 0.85f; // 최대 다리 길이에 대한 현재 다리 길이의 최소 비율
    [SerializeField] float minFootBelowPelvis = 0.5f;                   // 발이 골반보다 최소한 이만큼 아래에 있어야 지지 다리로 인정한다

    [Header("Ground Support")]
    [SerializeField] float supportBlendSpeed = 8f;          // 발이 땅에 닿거나 떨어졌을 때, 그 변화가 지지력(SupportBlend)에 반영되는 빠르기. 값이 클수록 빨리 반영된다
    [SerializeField] float supportVelocityResponse = 10f;   // 이 값이 클수록 현재 속도가 목표 수직 속도를 더 빠르게 따라잡는다
    [SerializeField] float maxSupportAcceleration = 30f;    // 착지 충격 완화와 기립에 사용할 최대 수직 가속도

    [Header("Standing")]
    [SerializeField] float standSpeed = 3f;                                 // 양발이 함께 바닥을 누를 때 골반이 상승하는 최대 속도
    [SerializeField] float minStandPressure = 0.15f;                        // 각 다리 플레이어가 이 값 이상 아래로 당겨야 기립 입력으로 인정한다
    [SerializeField, Range(0f, 1f)] float targetStandingExtension = 0.92f; // 무릎이 완전히 펴지기 전에 상승을 멈추는 목표 다리 길이 비율

    [Header("Arm-Driven Lean")]
    [SerializeField, Range(0f, 1f)] float minArmExtensionToLean = 0.45f; // 이 비율보다 팔을 길게 뻗었을 때부터 몸통에 영향을 준다
    [SerializeField] float maxBodyLeanAngle = 18f;                      // 양팔 입력으로 기울어질 수 있는 최대 좌우 각도
    [SerializeField] float bodyLeanResponse = 18f;                      // 이 값이 클수록 몸통이 목표 각도로 더 빠르게 기울어진다
    [SerializeField] float bodyLeanDamping = 6f;                        // 현재 회전 속도를 줄여 목표 각도 주변의 흔들림을 억제한다
    [SerializeField] float maxLeanAngularAcceleration = 540f;           // 팔 입력이 만들 수 있는 최대 각가속도(도/초²)
    [SerializeField, Range(0f, 1f)] float airborneLeanMultiplier = 0.25f; // 발이 지면을 지지하지 않을 때 남길 자세 제어 비율

    Vector3 spawnPosition;
    float spawnRotation;
    Rigidbody2D body;

    /// <summary>
    /// 발이 바닥을 딛고 있는 정도를 0~1 사이 값으로 나타낸다. 0이면 발이 땅에서 완전히 떨어진 상태,
    /// 1이면 양발이 완전히 바닥을 딛고 선 상태다. 접지 여부가 바뀔 때 이 값이 서서히 움직이면서
    /// 지지력이 갑자기 켜지거나 꺼지지 않고 부드럽게 변하게 한다.
    /// Fusion 재시뮬레이션에서도 같은 보간 결과가 나오도록 틱 상태로 저장한다.
    /// </summary>
    [Networked] float SupportBlend { get; set; }

    public override void Spawned()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.eulerAngles.z;
        body = GetComponent<Rigidbody2D>();

        // 물리는 상태 권한을 가진 호스트만 계산한다. 게스트의 몸통은 NetworkTransform 결과를 표시한다.
        if (Object.HasStateAuthority)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
            body.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
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
            body.rotation = spawnRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            SupportBlend = 0f;
        }

        // 다리 결과가 몸통 이동을 결정하고, 이동된 몸통이 팔의 어깨 위치를 결정하므로 이 순서를 지킨다.
        SimulateLeg(LeftLeg);
        SimulateLeg(RightLeg);

        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;
        isFootsGrounded = leftFootGrounded || rightFootGrounded;

        // 중력을 끄지 않는다. 대신 발이 땅에 닿았는지에 따라 중력에 맞서는 지지력을 서서히 켜고 끈다.
        body.gravityScale = 1f;
        SupportBlend = Mathf.MoveTowards(SupportBlend, isFootsGrounded ? 1f : 0f, supportBlendSpeed * Runner.DeltaTime);

        // 각 다리가 실제로 몸통을 지지할 수 있을 때만 PelvisPull을 걷기 입력에 포함한다.
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

        // 합을 사용하면 다리 수에 따라 속도가 두 배가 되므로 유효한 다리들의 평균을 사용한다.
        if (pullingLegCount > 0) pelvisPull /= pullingLegCount;

        // 걷기는 수평 속도, 양발 협동 기립은 수직 속도를 각각 요청한다.
        float desiredHorizontalVelocity = Mathf.Abs(pelvisPull.x) >= movementDeadZone ? Mathf.Clamp(pelvisPull.x, -1f, 1f) * speed : 0f;
        float desiredVerticalVelocity = TryGetStandingVelocity(out float standingVelocity) ? standingVelocity : 0f;
        ApplyContinuousBodyPhysics(desiredHorizontalVelocity, desiredVerticalVelocity);

        SimulateArms();
        ApplyArmDrivenLean();
    }

    /// <summary>
    /// Rigidbody2D가 몸통 위치를 갱신한 뒤 다리 루트와 관절을 새 골반 위치에 맞춥니다.
    /// 다리는 물리 적용 전에 계산되므로 이 후처리가 없으면 한 틱 동안 골반과 허벅지 사이가 벌어질 수 있습니다.
    /// </summary>
    public void AfterTick()
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (LeftLeg == null || RightLeg == null || LeftArm == null || RightArm == null) return;

        LeftLeg.RefreshHostPoseAfterPelvisMove();
        RightLeg.RefreshHostPoseAfterPelvisMove();
        LeftArm.RefreshHostPoseAfterBodyMove();
        RightArm.RefreshHostPoseAfterBodyMove();
    }

    /// <summary>
    /// 양발이 모두 고정되고 두 플레이어가 충분히 바닥을 누를 때 사용할 상승 속도를 계산합니다.
    /// 두 다리 중 누르는 힘이 약하거나 더 올라갈 수 있는 거리가 짧은 쪽에 맞춰 속도를 제한합니다.
    /// </summary>
    bool TryGetStandingVelocity(out float standingVelocity)
    {
        standingVelocity = 0f;
        if (!LeftLeg.TryGetStandingSupport(targetStandingExtension, minStandPressure, out float leftPressure, out float leftRiseDistance)) return false;
        if (!RightLeg.TryGetStandingSupport(targetStandingExtension, minStandPressure, out float rightPressure, out float rightRiseDistance)) return false;

        // 한 명만 강하게 눌러도 일어서지 않도록 두 입력 중 작은 값을 사용한다.
        float sharedPressure = Mathf.Min(leftPressure, rightPressure);
        float availableRiseDistance = Mathf.Min(leftRiseDistance, rightRiseDistance);

        // 남은 거리를 DeltaTime으로 나눈 속도로 제한해 이번 틱에 목표 높이를 지나치지 않게 한다.
        standingVelocity = Mathf.Min(availableRiseDistance / Runner.DeltaTime, standSpeed * Mathf.Clamp01(sharedPressure));
        return standingVelocity > 0f;
    }

    /// <summary>
    /// 중력을 끄거나 위치를 순간 이동하지 않고, 발의 지지력과 목표 속도를 연속적인 힘으로 적용합니다.
    /// </summary>
    void ApplyContinuousBodyPhysics(float desiredHorizontalVelocity, float desiredVerticalVelocity)
    {
        if (SupportBlend <= 0f) return;

        // 목표 속도와 현재 속도의 차이를 가속도로 바꾸되, 착지하거나 입력이 갑자기 바뀔 때 힘이 너무 세지지 않도록 제한한다.
        float horizontalAcceleration = Mathf.Clamp((desiredHorizontalVelocity - body.linearVelocity.x) * movementResponse, -maxMovementAcceleration, maxMovementAcceleration);
        float verticalAcceleration = Mathf.Clamp((desiredVerticalVelocity - body.linearVelocity.y) * supportVelocityResponse, -maxSupportAcceleration, maxSupportAcceleration);

        // 중력을 없애는 대신 같은 크기의 반대 가속도를 발의 지지력으로 더한다.
        float gravityCompensation = -Physics2D.gravity.y * body.gravityScale;
        Vector2 supportAcceleration = new Vector2(horizontalAcceleration, gravityCompensation + verticalAcceleration) * SupportBlend;

        // 위에서 계산한 값은 가속도이므로 질량을 곱해 Rigidbody2D에 전달할 힘으로 변환한다.
        body.AddForce(supportAcceleration * body.mass, ForceMode2D.Force);
    }

    /// <summary>
    /// 양팔이 실제로 뻗은 방향을 합산해 목표 몸통 각도를 만들고 토크로 자세를 따라갑니다.
    /// 서로 반대 방향으로 뻗은 팔은 영향을 상쇄하고 같은 방향으로 뻗은 팔은 영향을 강화합니다.
    /// </summary>
    void ApplyArmDrivenLean()
    {
        float leftLeanRequest = LeftArm.GetTorsoLeanRequest(minArmExtensionToLean);
        float rightLeanRequest = RightArm.GetTorsoLeanRequest(minArmExtensionToLean);
        float combinedLeanRequest = Mathf.Clamp(leftLeanRequest + rightLeanRequest, -1f, 1f);

        // 화면 오른쪽으로 뻗으면 시계 방향으로 기울어야 하므로 Unity의 양의 회전 방향과 부호가 반대다.
        float targetBodyAngle = -combinedLeanRequest * maxBodyLeanAngle;
        float angleError = Mathf.DeltaAngle(body.rotation, targetBodyAngle);
        float desiredAngularAcceleration = angleError * bodyLeanResponse - body.angularVelocity * bodyLeanDamping;
        desiredAngularAcceleration = Mathf.Clamp(desiredAngularAcceleration, -maxLeanAngularAcceleration, maxLeanAngularAcceleration);

        // 지면에서는 발을 축으로 자세를 적극 제어하고, 공중에서는 팔로 자세를 바꾸는 영향만 약하게 남긴다.
        float controlStrength = Mathf.Lerp(airborneLeanMultiplier, 1f, SupportBlend);
        float torque = desiredAngularAcceleration * Mathf.Deg2Rad * body.inertia * controlStrength;
        body.AddTorque(torque, ForceMode2D.Force);
    }

    /// <summary>
    /// 다리 NetworkObject의 입력 권한을 가진 플레이어가 보낸 마우스 좌표를 호스트 시뮬레이션에 전달합니다.
    /// </summary>
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

    /// <summary>
    /// 팔 NetworkObject의 입력 권한을 가진 플레이어가 보낸 마우스 좌표를 호스트 시뮬레이션에 전달합니다.
    /// </summary>
    void SimulateArm(ArmManager arm)
    {
        if (Runner.TryGetInputForPlayer(arm.Object.InputAuthority, out NetworkInputData input))
        {
            arm.SimulateHost(input.MouseWorldPos);
        }
    }
}
