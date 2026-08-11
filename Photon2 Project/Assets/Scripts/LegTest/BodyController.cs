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
    [SerializeField] float speed = 5f;                                  // 지면을 미는 입력으로 만들 수 있는 최대 수평 속도
    [SerializeField] float movementResponse = 10f;                      // 이 값이 클수록 현재 속도가 목표 수평 속도를 더 빠르게 따라잡는다
    [SerializeField] float maxMovementAcceleration = 25f;               // 위 값으로 계산한 힘이 한 틱에 낼 수 있는 최대 수평 가속도(이보다 세게는 못 민다)
    [SerializeField] float movementDeadZone = 0.05f;                    // 이 거리보다 작은 접선 입력은 마우스 떨림으로 간주한다
    [SerializeField] float minGroundPushPressure = 0.15f;               // 발을 이 값 이상 지면 안쪽으로 눌러야 수평 지면 반력이 생긴다
    [SerializeField, Min(0f)] float groundFriction = 0.8f;               // 수직 지지력에 비례해 허용할 최대 수평 마찰력 비율
    [SerializeField] float minFootBelowPelvis = 0.5f;                   // 발이 골반보다 최소한 이만큼 아래에 있어야 지지 다리로 인정한다

    [Header("Ground Support")]
    [SerializeField] float supportBlendSpeed = 8f;          // 발이 땅에 닿거나 떨어졌을 때, 그 변화가 지지력(SupportBlend)에 반영되는 빠르기. 값이 클수록 빨리 반영된다
    [SerializeField] float supportVelocityResponse = 10f;   // 이 값이 클수록 현재 속도가 목표 수직 속도를 더 빠르게 따라잡는다
    [SerializeField] float maxSupportAcceleration = 30f;    // 착지 충격 완화와 기립에 사용할 최대 수직 가속도
    [SerializeField, Min(0f)] float footSupportRadius = 0.45f; // 한 발이 무게중심을 안정적으로 지지할 수 있는 좌우 반경
    [SerializeField, Min(0.01f)] float balanceFalloffDistance = 1f; // 무게중심이 지지 영역을 벗어난 뒤 지지력이 0이 되는 거리
    [SerializeField, Min(0f)] float maxGroundReactionAngularAcceleration = 180f; // 발 위치가 만드는 회전 효과의 최대 각가속도. 멀리 떨어진 발이 몸통을 계속 회전시키는 현상을 막는다
    [SerializeField, Min(0f)] float groundedAngularDamping = 4f; // 접지 중 발의 회전 효과에 적용할 각속도 감쇠
    [SerializeField, Min(0f)] float maxBodyAngularVelocity = 120f; // 모든 자세 제어를 적용한 뒤 허용할 몸통의 최대 각속도

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

    struct GroundContactData
    {
        public Vector2 Point;
        public Vector2 Normal;
        public Vector2 PushInput;
        public float Pressure;
    }

    // 왼발과 오른발 접촉만 저장하므로 매 틱 할당이 생기지 않는 고정 크기 배열을 사용한다.
    readonly GroundContactData[] groundContacts = new GroundContactData[2];

    /// <summary>
    /// 발과 무게중심이 몸통을 지지할 수 있는 정도를 0~1 사이 값으로 나타냅니다.
    /// 발이 고정되어 있어도 무게중심이 지지 영역 밖으로 나가면 값이 감소합니다.
    /// 접지나 균형 상태가 바뀔 때 지지력이 갑자기 켜지거나 꺼지지 않도록 서서히 변화합니다.
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

        // Ground와 겹친 발이 아니라 실제로 고정되어 몸통 아래에 있는 발만 지면 반력 접촉으로 수집한다.
        int groundContactCount = CollectGroundContacts();
        float balanceSupport = CalculateBalanceSupport(groundContactCount, out float supportMinX, out float supportMaxX);

        // 중력을 끄지 않는다. 발 접촉과 무게중심이 유효할 때만 중력에 맞서는 지면 반력을 서서히 켠다.
        body.gravityScale = 1f;
        SupportBlend = Mathf.MoveTowards(SupportBlend, balanceSupport, supportBlendSpeed * Runner.DeltaTime);

        // 고정 발을 미는 반대 방향이 수평 지면 반력이고, 양발 협동 입력은 수직 기립 속도다.
        float desiredHorizontalVelocity = GetDesiredHorizontalVelocity(groundContactCount, supportMinX, supportMaxX);
        float desiredVerticalVelocity = TryGetStandingVelocity(out float standingVelocity) ? standingVelocity : 0f;
        ApplyGroundReactionPhysics(groundContactCount, desiredHorizontalVelocity, desiredVerticalVelocity);

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
        if (body == null || LeftLeg == null || RightLeg == null || LeftArm == null || RightArm == null) return;

        // 발 지지력과 팔 자세 제어가 동시에 큰 회전력을 만들더라도 몸통이 한 틱에 폭주하지 않도록 최종 각속도를 제한한다.
        body.angularVelocity = Mathf.Clamp(body.angularVelocity, -maxBodyAngularVelocity, maxBodyAngularVelocity);

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
    /// 현재 고정된 양발에서 지면 반력 계산에 필요한 접촉 정보를 수집합니다.
    /// </summary>
    int CollectGroundContacts()
    {
        int count = 0;
        if (LeftLeg.TryGetGroundContact(minFootBelowPelvis, out Vector2 leftPoint, out Vector2 leftNormal, out Vector2 leftPushInput, out float leftPressure))
            groundContacts[count++] = new GroundContactData { 
                Point = leftPoint, Normal = leftNormal, PushInput = leftPushInput, Pressure = leftPressure 
            };

        if (RightLeg.TryGetGroundContact(minFootBelowPelvis, out Vector2 rightPoint, out Vector2 rightNormal, out Vector2 rightPushInput, out float rightPressure)) 
            groundContacts[count++] = new GroundContactData { 
                Point = rightPoint, Normal = rightNormal, PushInput = rightPushInput, Pressure = rightPressure 
            };

        return count;
    }

    /// <summary>
    /// 고정된 발들이 만드는 수평 지지 영역과 몸통 무게중심의 거리를 이용해 현재 지지 강도를 계산합니다.
    /// </summary>
    float CalculateBalanceSupport(int contactCount, out float supportMinX, out float supportMaxX)
    {
        supportMinX = 0f;
        supportMaxX = 0f;
        if (contactCount <= 0) return 0f;

        supportMinX = groundContacts[0].Point.x - footSupportRadius;
        supportMaxX = groundContacts[0].Point.x + footSupportRadius;
        for (int i = 1; i < contactCount; i++)
        {
            supportMinX = Mathf.Min(supportMinX, groundContacts[i].Point.x - footSupportRadius);
            supportMaxX = Mathf.Max(supportMaxX, groundContacts[i].Point.x + footSupportRadius);
        }

        float centerOfMassX = body.worldCenterOfMass.x;
        float distanceOutsideSupport = centerOfMassX < supportMinX ? supportMinX - centerOfMassX : centerOfMassX > supportMaxX ? centerOfMassX - supportMaxX : 0f;
        return 1f - Mathf.Clamp01(distanceOutsideSupport / balanceFalloffDistance);
    }

    /// <summary>
    /// 고정 발을 지면과 나란히 민 입력의 반대 방향으로 목표 수평 속도를 계산합니다.
    /// 무게중심이 지지 영역 밖에 있을 때 더 멀어지는 입력은 차단하고 안쪽으로 복귀하는 입력만 허용합니다.
    /// </summary>
    float GetDesiredHorizontalVelocity(int contactCount, float supportMinX, float supportMaxX)
    {
        float desiredVelocitySum = 0f;
        int pushingFootCount = 0;

        for (int i = 0; i < contactCount; i++)
        {
            GroundContactData contact = groundContacts[i];
            if (contact.Pressure < minGroundPushPressure) continue;

            // 발을 왼쪽으로 밀면 지면은 몸을 오른쪽으로 미는 반작용을 만든다.
            float reactionInputX = -contact.PushInput.x;
            if (Mathf.Abs(reactionInputX) < movementDeadZone) continue;

            float pressureWeight = Mathf.InverseLerp(minGroundPushPressure, minGroundPushPressure + 0.5f, contact.Pressure);
            desiredVelocitySum += Mathf.Clamp(reactionInputX, -1f, 1f) * speed * pressureWeight;
            pushingFootCount++;
        }

        if (pushingFootCount <= 0) return 0f;

        float desiredVelocity = desiredVelocitySum / pushingFootCount;
        float centerOfMassX = body.worldCenterOfMass.x;
        if (centerOfMassX < supportMinX && desiredVelocity < 0f) return 0f;
        if (centerOfMassX > supportMaxX && desiredVelocity > 0f) return 0f;
        return desiredVelocity;
    }

    /// <summary>
    /// 각 발에서 계산한 지면 반력의 합은 몸통 무게중심에 적용하고, 발 위치가 만드는 회전 효과는 별도로 제한하여 적용합니다.
    /// 이 분리는 멀리 떨어진 절차적 IK 발에 중력 보상력이 적용되어 몸통이 회전 폭주하는 것을 방지합니다.
    /// </summary>
    void ApplyGroundReactionPhysics(int contactCount, float desiredHorizontalVelocity, float desiredVerticalVelocity)
    {
        if (contactCount <= 0 || SupportBlend <= 0f) return;

        // 목표 속도와 현재 속도의 차이를 가속도로 바꾸되, 착지하거나 입력이 갑자기 바뀔 때 힘이 너무 세지지 않도록 제한한다.
        float horizontalAcceleration = Mathf.Clamp((desiredHorizontalVelocity - body.linearVelocity.x) * movementResponse, -maxMovementAcceleration, maxMovementAcceleration);
        float verticalAcceleration = Mathf.Clamp((desiredVerticalVelocity - body.linearVelocity.y) * supportVelocityResponse, -maxSupportAcceleration, maxSupportAcceleration);

        // 접촉은 몸을 위로 밀 수만 있으므로 중력 보상과 수직 제어를 합친 값이 음수라면 0으로 제한한다.
        float gravityCompensation = -Physics2D.gravity.y * body.gravityScale;
        float totalNormalAcceleration = Mathf.Max(0f, gravityCompensation + verticalAcceleration) * SupportBlend;
        float totalTangentAcceleration = horizontalAcceleration * SupportBlend;
        Vector2 accumulatedContactAcceleration = Vector2.zero;
        float accumulatedGroundTorque = 0f;

        for (int i = 0; i < contactCount; i++)
        {
            GroundContactData contact = groundContacts[i];
            float loadShare = GetContactLoadShare(i, contactCount);
            float normalAcceleration = totalNormalAcceleration * loadShare;
            Vector2 tangent = new Vector2(contact.Normal.y, -contact.Normal.x);
            float requestedTangentAcceleration = Vector2.Dot(Vector2.right * totalTangentAcceleration * loadShare, tangent);
            float tangentAcceleration = Mathf.Clamp(requestedTangentAcceleration, -groundFriction * normalAcceleration, groundFriction * normalAcceleration);
            Vector2 contactAcceleration = contact.Normal * normalAcceleration + tangent * tangentAcceleration;
            Vector2 contactForce = contactAcceleration * body.mass;
            Vector2 leverArm = contact.Point - body.worldCenterOfMass;

            accumulatedContactAcceleration += contactAcceleration;
            accumulatedGroundTorque += leverArm.x * contactForce.y - leverArm.y * contactForce.x;
        }

        // 절차적 IK 발은 물리 조인트가 아니므로, 선형 지지력을 실제 접촉점에 직접 적용하면 긴 지렛팔이 비현실적인 토크를 만든다.
        body.AddForce(accumulatedContactAcceleration * body.mass, ForceMode2D.Force);
        ApplyLimitedGroundReactionTorque(accumulatedGroundTorque);
    }

    /// <summary>
    /// 발 위치로부터 계산한 물리 토크를 각가속도로 변환한 뒤 안전 범위로 제한하여 몸통에 적용합니다.
    /// 제한을 먼저 적용하고 각속도 감쇠를 더하므로, 멀리 떨어진 발의 큰 토크가 감쇠를 압도하지 않습니다.
    /// </summary>
    void ApplyLimitedGroundReactionTorque(float requestedTorque)
    {
        if (body.inertia <= Mathf.Epsilon) return;

        float requestedAngularAcceleration = requestedTorque / body.inertia * Mathf.Rad2Deg;
        float limitedAngularAcceleration = Mathf.Clamp(requestedAngularAcceleration, -maxGroundReactionAngularAcceleration, maxGroundReactionAngularAcceleration);
        float dampedAngularAcceleration = limitedAngularAcceleration - body.angularVelocity * groundedAngularDamping;
        float groundTorque = dampedAngularAcceleration * Mathf.Deg2Rad * body.inertia;
        body.AddTorque(groundTorque, ForceMode2D.Force);
    }

    /// <summary>
    /// 두 발 접지에서는 무게중심 위치에 따라 각 발이 담당할 수직 하중을 나눠 불필요한 회전력을 줄입니다.
    /// 무게중심이 두 발 바깥에 있으면 가까운 발 하나가 모든 하중을 담당해 자연스럽게 넘어지게 합니다.
    /// </summary>
    float GetContactLoadShare(int contactIndex, int contactCount)
    {
        if (contactCount == 1) return 1f;

        float firstX = groundContacts[0].Point.x;
        float secondX = groundContacts[1].Point.x;
        float distance = secondX - firstX;
        if (Mathf.Abs(distance) <= 0.0001f) return 0.5f;

        float firstShare = Mathf.Clamp01((secondX - body.worldCenterOfMass.x) / distance);
        return contactIndex == 0 ? firstShare : 1f - firstShare;
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
