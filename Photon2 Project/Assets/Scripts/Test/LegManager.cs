using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 상태 권한을 가진 호스트에서 발 목표 이동, 접지·고정 상태, 2-Bone IK를 계산합니다.
/// 이 클래스는 몸통을 직접 이동하지 않고, 고정된 발의 접촉점과 플레이어가 발을 미는 입력을 BodyController에 제공합니다.
/// </summary>
public class LegManager : NetworkBehaviour
{
    [Header("Objects")]
    public GameObject pelvis;
    [SerializeField] GameObject knee;
    public GameObject foot;

    [Header("Bone Lengths (2-Bone IK)")]
    [SerializeField] float thighLength = 2.5f; // 골반에서 무릎까지의 고정 길이
    [SerializeField] float shinLength = 2.52f; // 무릎에서 발까지의 고정 길이
    [SerializeField] int bendDirection = 1; // 무릎이 굽는 방향이며 반대쪽으로 굽으면 -1로 설정한다

    [SerializeField] float footSpeed = 15f; // 마우스를 향해 발이 이동할 수 있는 초당 최대 월드 거리

    [Header("Foot")]
    [SerializeField, Min(0.01f)] float footCollisionRadius = 0.25f; // 절차적 발을 대표하는 가상 충돌 원의 반지름. 발 스프라이트가 바닥 안으로 들어가지 않도록 이동 검사와 접지 판정에 함께 사용한다
    [SerializeField, Min(0f)] float groundSkin = 0.02f;              // 물리 오차로 발이 지면과 반복해서 겹치지 않도록 표면에서 추가로 띄우는 최소 간격
    [SerializeField] float plantedReleaseHeight = 0.5f; // 고정된 발보다 마우스를 이 높이 이상 올리면 발을 뗀다
    public bool isGround;   // 실제 발의 접지
    public bool isObstacle; // 골반과 마우스 사이에서 Ground를 찾았는지를 나타낸다.

    RaycastHit2D raycastHit;    // 골반에서 마우스 방향으로 검사한 가장 가까운 Ground 충돌 정보다.
    Vector2 mouseWorldPos;

    /// <summary>
    /// 고정된 발을 움직이려고 한 입력 중 바닥 표면과 나란한 성분입니다.
    /// BodyController는 이 입력의 반대 방향으로 제한된 지면 반력을 계산합니다.
    /// </summary>
    public Vector2 GroundPushInput { get; private set; }

    /// <summary>
    /// 발이 바닥에 고정되어 있는지를 나타냅니다.
    /// 단, 골반을 끌어 이동시키려면 다리가 충분히 펴져 있고
    /// 발이 골반보다 아래에 있어야 합니다.
    /// </summary>
    public bool IsPlanted { get; private set; }

    /// <summary>
    /// 고정된 발보다 아래에 있는 마우스와 발 사이의 수직 거리입니다.
    /// 양쪽 다리의 값이 모두 기준 이상일 때 BodyController가 협동 기립 속도를 계산합니다.
    /// </summary>
    public float StandPressure { get; private set; }

    /// <summary>
    /// 발이 처음 접지했을 때 기록하며 몸통이 이동해도 유지되는 월드 좌표입니다.
    /// </summary>
    Vector2 plantedPosition;

    // 발을 고정한 지면의 법선이다. 평지에서는 Vector2.up이며 경사면에서는 표면 방향을 따른다.
    Vector2 plantedNormal = Vector2.up;

    // 최대 길이 초과 등으로 고정이 풀린 발이 같은 Ground 안에서 즉시 다시 고정되는 현상을 막는다.
    bool waitForGroundExitBeforePlanting;

    public override void Spawned()
    {
        if (TryResolvePelvis())
            FollowPelvisAnchor();
    }

    void LateUpdate()
    {
        if (Object == null || Object.HasStateAuthority) return;
        if (TryResolvePelvis() == false) return;

        // 게스트는 IK를 다시 계산하지 않고 사지 루트만 보간된 몸통 골반 앵커에 붙인다.
        FollowPelvisAnchor();
    }

    /// <summary>
    /// 네트워크 스폰 순서 때문에 비어 있을 수 있는 골반 참조를 현재 로봇의 좌우 앵커에서 찾습니다.
    /// </summary>
    bool TryResolvePelvis()
    {
        if (pelvis != null) return true;

        var body = FindFirstObjectByType<BodyController>();
        if (body == null) return false;

        bool isLeftLeg = gameObject.name.StartsWith("Left", System.StringComparison.Ordinal);
        pelvis = isLeftLeg ? body.pelvisL : body.pelvisR;
        return pelvis != null;
    }

    public void SimulateHost(Vector2 inputMouseWorldPos)
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (TryResolvePelvis() == false) return;

        FollowPelvisAnchor();
        mouseWorldPos = inputMouseWorldPos;

        // 고정된 발은 월드 위치를 유지한다. 고정되지 않았다면 경로 검사 → 발 이동 → 접지 검사 → 고정 순서로 처리한다.
        if (TryMaintainPlantedFoot() == false)
        {
            FootGrounded();
            LookMouse();
            FootGroundedFromFoot();
            TryPlantFoot();
        }

        SolveTwoBoneIK();

        Vector2 direction = (knee.transform.position - foot.transform.position).normalized;
        foot.transform.up = direction;
    }

    /// <summary>
    /// 고정된 발이 몸통을 지지할 수 있는 접촉인지 확인하고 지면 반력 계산에 필요한 값을 반환합니다.
    /// 단순히 발 주변에 Ground가 있는 것만으로는 부족하며 발이 고정되어 있고 골반보다 아래에 있어야 합니다.
    /// </summary>
    public bool TryGetGroundContact(float minVerticalDrop, out Vector2 contactPoint, out Vector2 contactNormal, out Vector2 pushInput, out float pressure)
    {
        contactPoint = Vector2.zero;
        contactNormal = Vector2.up;
        pushInput = Vector2.zero;
        pressure = 0f;
        if (IsPlanted == false || isGround == false) return false;

        float verticalDrop = pelvis.transform.position.y - foot.transform.position.y;
        if (verticalDrop < minVerticalDrop) return false;

        contactPoint = plantedPosition;
        contactNormal = plantedNormal;
        pushInput = GroundPushInput;
        pressure = StandPressure;
        return true;
    }

    /// <summary>
    /// 틱의 몸통 물리 이동이 끝난 뒤 발의 월드 위치는 유지하면서 다리 루트와 무릎을 다시 맞춥니다.
    /// </summary>
    public void RefreshHostPoseAfterPelvisMove()
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (TryResolvePelvis() == false) return;

        // 고정된 발은 기록된 접지점을, 움직이는 발은 현재 위치를 몸통 이동 전 기준점으로 보존한다.
        Vector2 preservedFootPosition = IsPlanted ? plantedPosition : foot.transform.position;
        FollowPelvisAnchor();

        float maxReach = thighLength + shinLength - 0.01f;
        Vector2 pelvisToFoot = preservedFootPosition - (Vector2)pelvis.transform.position;
        if (pelvisToFoot.magnitude > maxReach)
        {
            // 몸통 이동으로 고정된 발이 최대 길이를 벗어나면 고정을 풀고 경계로 되돌린다.
            IsPlanted = false;
            StandPressure = 0f;
            GroundPushInput = Vector2.zero;
            waitForGroundExitBeforePlanting = true;
            preservedFootPosition = (Vector2)pelvis.transform.position + pelvisToFoot.normalized * maxReach;
        }

        // 몸통 낙하나 회전으로 골반 앵커가 크게 움직인 뒤에도 발 중심이 Ground 내부에 남지 않도록 다시 표면 밖으로 보정한다.
        preservedFootPosition = ResolveFootPenetration(preservedFootPosition);
        if (IsPlanted) plantedPosition = preservedFootPosition;

        foot.transform.position = preservedFootPosition;
        SolveTwoBoneIK();

        Vector2 direction = (knee.transform.position - foot.transform.position).normalized;
        foot.transform.up = direction;
    }

    /// <summary>
    /// 고정된 발과 현재 골반 위치를 기준으로 기립에 사용할 압력과 남은 상승 거리를 계산합니다.
    /// 발의 좌우 간격이 다리의 목표 길이보다 넓으면 해당 다리로는 일어설 수 없습니다.
    /// </summary>
    public bool TryGetStandingSupport(float targetExtensionRatio, float minPressure, out float pressure, out float riseDistance)
    {
        pressure = 0f;
        riseDistance = 0f;
        if (IsPlanted == false || StandPressure < minPressure) return false;

        float targetReach = (thighLength + shinLength) * targetExtensionRatio;
        float horizontalDistance = Mathf.Abs(pelvis.transform.position.x - plantedPosition.x);
        if (horizontalDistance >= targetReach) return false;

        // 발의 수평 위치는 고정되어 있으므로 피타고라스 정리로 목표 다리 길이에서 가능한 골반 높이를 구한다.
        float targetVerticalDistance = Mathf.Sqrt(targetReach * targetReach - horizontalDistance * horizontalDistance);
        float currentVerticalDistance = pelvis.transform.position.y - plantedPosition.y;

        pressure = StandPressure;
        riseDistance = Mathf.Max(0f, targetVerticalDistance - currentVerticalDistance);
        return riseDistance > 0.001f;
    }

    /// <summary>
    /// 발을 고정한 자리에 그대로 붙여 두고, 그 뒤에도 계속 들어오는 마우스 입력을 둘로 나눠서 쓴다.
    /// 지면 안쪽으로 누르는 입력은 StandPressure로, 지면과 나란히 미는 입력은 GroundPushInput으로 분리합니다.
    /// 두 값은 직접적인 힘이 아니며 BodyController가 마찰과 균형 조건을 적용한 뒤 지면 반력으로 변환합니다.
    /// </summary>
    bool TryMaintainPlantedFoot()
    {
        if (IsPlanted == false) return false;

        // 플레이어가 발을 들거나, 접지면이 사라지거나, 몸통과 발이 최대 길이보다 멀어지면 고정을 해제한다.
        bool wantsToRelease = mouseWorldPos.y > plantedPosition.y + plantedReleaseHeight;
        bool groundStillExists = Physics2D.OverlapCircle(plantedPosition, footCollisionRadius + groundSkin * 2f, LayerMask.GetMask("Ground")) != null;
        float maxReach = thighLength + shinLength - 0.01f;
        Vector2 pelvisToPlantedFoot = plantedPosition - (Vector2)pelvis.transform.position;
        bool legOverstretched = pelvisToPlantedFoot.magnitude > maxReach;

        if (wantsToRelease || !groundStillExists || legOverstretched)
        {
            IsPlanted = false;
            StandPressure = 0f;
            GroundPushInput = Vector2.zero;
            waitForGroundExitBeforePlanting = true;

            // 고정 해제 순간에도 발이 다리 최대 길이 밖에 남지 않도록 즉시 보정한다.
            if (legOverstretched) foot.transform.position = (Vector2)pelvis.transform.position + pelvisToPlantedFoot.normalized * maxReach;
            return false;
        }

        foot.transform.position = plantedPosition;
        isGround = true;
        isObstacle = true;

        // 입력을 지면 법선과 접선으로 나눠 누르는 힘과 미는 방향을 서로 독립적으로 사용한다.
        Vector2 blockedInput = mouseWorldPos - plantedPosition;
        float normalInput = Vector2.Dot(blockedInput, plantedNormal);
        StandPressure = Mathf.Max(0f, -normalInput);
        GroundPushInput = blockedInput - plantedNormal * normalInput;
        return true;
    }

    /// <summary>
    /// 이동이 끝난 발이 바닥에 닿아 있으면 현재 위치에 고정합니다.
    /// </summary>
    void TryPlantFoot()
    {
        StandPressure = 0f;
        GroundPushInput = Vector2.zero;

        // 고정이 강제로 풀렸다면 Ground에서 완전히 빠진 한 틱을 확인한 뒤에만 다시 고정할 수 있다.
        if (waitForGroundExitBeforePlanting)
        {
            if (isGround == false) waitForGroundExitBeforePlanting = false;
            return;
        }

        if (isGround == false) return;

        IsPlanted = true;
        plantedPosition = foot.transform.position;

        // 레이로 찾은 표면 법선을 우선 사용하고, 유효하지 않으면 평지 법선을 사용한다.
        plantedNormal = isObstacle && raycastHit.collider != null ? raycastHit.normal.normalized : Vector2.up;
        if (Vector2.Dot(plantedNormal, Vector2.up) < 0f) plantedNormal = -plantedNormal;
    }

    /// <summary>
    /// 독립된 NetworkObject인 다리 루트를 몸통의 골반 앵커 위치와 회전에 맞춥니다.
    /// </summary>
    void FollowPelvisAnchor()
    {
        transform.SetPositionAndRotation(pelvis.transform.position, pelvis.transform.rotation);
    }

    /// <summary>
    /// 골반과 발 위치, 두 뼈의 고정 길이를 이용해 코사인 법칙으로 무릎 위치를 계산합니다.
    /// </summary>
    void SolveTwoBoneIK()
    {
        Vector2 pelvisPos = pelvis.transform.position;
        Vector2 footPos = foot.transform.position;

        Vector2 toFoot = footPos - pelvisPos;
        float maxReach = thighLength + shinLength;
        float minReach = Mathf.Abs(thighLength - shinLength) + 0.01f;

        // 계산 거리뿐 아니라 실제 발 위치도 최대 도달 범위 안으로 제한한다.
        if (toFoot.magnitude > maxReach - 0.01f)
        {
            footPos = pelvisPos + toFoot.normalized * (maxReach - 0.01f);
            foot.transform.position = footPos;
            toFoot = footPos - pelvisPos;
        }

        // 최소 거리 보정은 두 뼈의 길이가 비슷할 때 코사인 법칙의 분모가 0에 가까워지는 것을 막는다.
        float d = Mathf.Clamp(toFoot.magnitude, minReach, maxReach - 0.01f);

        float cosAngle = (thighLength * thighLength + d * d - shinLength * shinLength) / (2f * thighLength * d);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        float angle = Mathf.Acos(cosAngle);

        float baseAngle = Mathf.Atan2(toFoot.y, toFoot.x);
        float kneeAngle = baseAngle + angle * bendDirection;

        Vector2 kneePos = pelvisPos + new Vector2(Mathf.Cos(kneeAngle), Mathf.Sin(kneeAngle)) * thighLength;
        knee.transform.position = kneePos;

        // 허벅지 스프라이트의 피벗이 무릎에 있으므로 위쪽 축이 골반을 향하게 한다.
        Vector2 thighDir = (pelvisPos - kneePos).normalized;
        knee.transform.up = thighDir;
    }

    /// <summary>
    /// 마우스 방향으로 찾은 바닥 지점 또는 다리가 닿을 수 있는 가장 먼 지점을 발의 목표 위치로 사용합니다.
    /// 고정되지 않은 발은 몸통에 힘을 전달하지 않고 관절 자세만 변경합니다.
    /// </summary>
    void LookMouse()
    {
        Vector2 targetPos;
        if (isObstacle == true)
        {
            // 표면점에서 발 반지름만큼 법선 방향으로 띄워 발 중심을 배치한다. 중심을 표면점에 직접 두면 발의 절반이 지형 안으로 들어간다.
            targetPos = raycastHit.point + raycastHit.normal * (footCollisionRadius + groundSkin);
        }
        else
        {
            // Ground가 없다면 마우스 위치를 허벅지와 종아리의 합산 길이 안으로 제한한다.
            float maxReach = thighLength + shinLength;
            Vector2 fp_dir = mouseWorldPos - (Vector2)pelvis.transform.position;
            targetPos = (fp_dir.magnitude > maxReach)
                ? (Vector2)pelvis.transform.position + fp_dir.normalized * maxReach 
                : mouseWorldPos;

        }

        // 발의 실제 이동 구간도 CircleCast로 검사한다. 골반→마우스 검사 방향과 발의 이동 방향이 다르더라도 중간의 바닥을 통과하지 않는다.
        Vector2 currentPosition = foot.transform.position;
        Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPos, footSpeed * Runner.DeltaTime);
        Vector2 movement = nextPosition - currentPosition;

        if (movement.sqrMagnitude > 0.000001f)
        {
            RaycastHit2D movementHit = Physics2D.CircleCast(currentPosition, footCollisionRadius, movement.normalized, movement.magnitude, LayerMask.GetMask("Ground"));
            if (movementHit.collider != null) nextPosition = movementHit.centroid + movementHit.normal * groundSkin;
        }

        foot.transform.position = ResolveFootPenetration(nextPosition);
    }

    /// <summary>
    /// 이동을 마친 실제 발 위치 주변에 Ground 레이어가 있는지 검사해 isGround를 갱신합니다.
    /// </summary>
    void FootGroundedFromFoot()
    {
        Collider2D hit = Physics2D.OverlapCircle(foot.transform.position, footCollisionRadius + groundSkin * 2f, LayerMask.GetMask("Ground"));
        isGround = hit != null;
    }

    /// <summary>
    /// 이름과 달리 발의 최종 접지를 판정하지 않습니다.
    /// 골반에서 마우스 방향으로 레이를 쏴 발이 먼저 닿아야 할 Ground 표면을 찾습니다.
    /// 실제 발 크기와 이동 경로에 대한 관통 방지는 LookMouse의 CircleCast가 담당합니다.
    /// </summary>
    void FootGrounded()
    {
        float maxReach = thighLength + shinLength;
        Vector2 pelvisPos = pelvis.transform.position;
        Vector2 toMouse = mouseWorldPos - pelvisPos;

        if (toMouse.sqrMagnitude < 0.0001f)
        {
            isObstacle = false;
            return;
        }

        // 다리로 도달할 수 없는 먼 지형이 발 목표가 되지 않도록 레이 길이를 최대 다리 길이로 제한한다.
        float rayDistance = Mathf.Min(toMouse.magnitude, maxReach);
        raycastHit = Physics2D.Raycast(pelvisPos, toMouse.normalized, rayDistance, LayerMask.GetMask("Ground"));
        isObstacle = raycastHit.collider != null;
    }

    /// <summary>
    /// 이미 Ground 안에 들어간 발을 골반 쪽에서 처음 만나는 표면 바깥으로 복구합니다.
    /// 정상 이동은 CircleCast가 관통을 예방하지만, 낙하 직전 위치나 몸통 회전으로 시작점부터 겹친 경우에는 이 사후 보정이 필요합니다.
    /// </summary>
    Vector2 ResolveFootPenetration(Vector2 candidatePosition)
    {
        int groundMask = LayerMask.GetMask("Ground");
        Collider2D overlappedGround = Physics2D.OverlapCircle(candidatePosition, footCollisionRadius, groundMask);
        if (overlappedGround == null) return candidatePosition;

        Vector2 pelvisPosition = pelvis.transform.position;
        Vector2 pelvisToCandidate = candidatePosition - pelvisPosition;
        if (pelvisToCandidate.sqrMagnitude > 0.000001f)
        {
            RaycastHit2D surfaceHit = Physics2D.CircleCast(pelvisPosition, footCollisionRadius, pelvisToCandidate.normalized, pelvisToCandidate.magnitude, groundMask);
            if (surfaceHit.collider != null && surfaceHit.fraction > 0.0001f) return surfaceHit.centroid + surfaceHit.normal * groundSkin;
        }

        // 골반 앵커까지 Ground 안에 있는 특수 자세에서는 위쪽의 안전한 점에서 해당 콜라이더의 가장 가까운 표면을 찾아 복구한다.
        Vector2 recoveryProbe = candidatePosition + Vector2.up * (thighLength + shinLength + footCollisionRadius);
        Vector2 surfacePoint = overlappedGround.ClosestPoint(recoveryProbe);
        Vector2 recoveryNormal = (recoveryProbe - surfacePoint).normalized;
        if (recoveryNormal.sqrMagnitude < 0.000001f) recoveryNormal = Vector2.up;
        return surfacePoint + recoveryNormal * (footCollisionRadius + groundSkin);
    }

    #region Test
    private void OnDrawGizmos()
    {
        if (pelvis == null || knee == null || foot == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pelvis.transform.position, knee.transform.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(knee.transform.position, foot.transform.position);

        // 발 목표가 도달할 수 있는 최대·최소 범위를 원으로 표시한다.
        DrawReachCircle(pelvis.transform.position, thighLength + shinLength, Color.yellow);
        DrawReachCircle(pelvis.transform.position, Mathf.Abs(thighLength - shinLength), Color.red);

#if UNITY_EDITOR
        float thighDist = Vector3.Distance(pelvis.transform.position, knee.transform.position);
        float shinDist = Vector3.Distance(knee.transform.position, foot.transform.position);
        UnityEditor.Handles.Label((pelvis.transform.position + knee.transform.position) / 2, $"허벅지 실측: {thighDist:F2}");
        UnityEditor.Handles.Label((knee.transform.position + foot.transform.position) / 2, $"종아리 실측: {shinDist:F2}");
        UnityEditor.Handles.Label(pelvis.transform.position + Vector3.up * (thighLength + shinLength), "최대 도달 범위 (노랑)");
#endif
    }

    // 2D XY 평면에서 도달 범위를 확인하기 위한 원형 기즈모를 선분으로 그린다.
    void DrawReachCircle(Vector3 center, float radius, Color color)
    {
        if (radius <= 0f) return;
        Gizmos.color = color;
        int segments = 48;
        Vector3 prevPoint = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
    #endregion
}
