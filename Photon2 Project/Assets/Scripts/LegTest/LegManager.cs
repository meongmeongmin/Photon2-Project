using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 상태 권한을 가진 호스트에서 발 목표 이동, 접지·고정 상태, 2-Bone IK를 계산합니다.
/// 이 클래스는 몸통을 직접 이동하지 않고, 이동이 필요하면 PelvisPull과 StandPressure를 BodyController에 제공합니다.
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
    float groundCheckRadius = 0.15f;                    // 발 중심에서 이 반지름 안에 Ground가 있으면 접지된 것으로 판단한다.
    [SerializeField] float plantedReleaseHeight = 0.5f; // 고정된 발보다 마우스를 이 높이 이상 올리면 발을 뗀다
    public bool isGround;   // 실제 발의 접지
    public bool isObstacle; // 골반과 마우스 사이에서 Ground를 찾았는지를 나타낸다.

    RaycastHit2D raycastHit;    // 골반에서 마우스 방향으로 검사한 가장 가까운 Ground 충돌 정보다.
    Vector2 mouseWorldPos;

    /// <summary>
    /// 마우스를 다리가 닿을 수 있는 범위보다, 또는 고정된 발보다 더 멀리 움직였을 때
    /// 발이 따라가지 못하고 남은 거리다.
    /// 이 값 자체는 아직 힘이 아니다. BodyController가 이 다리가 몸통을 지지할 수 있는 자세인지
    /// 확인한 뒤에야 실제로 몸통을 움직이는 속도로 바뀐다.
    /// </summary>
    public Vector2 PelvisPull { get; private set; }

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
    /// 지금 다리 자세로 몸통을 당겨도 되는지 확인하고, 확인되면 자세에 맞게 줄인 PelvisPull 값을 돌려준다.
    /// 발이 땅에 닿아 있고, 다리가 충분히 펴져 있고, 발이 골반보다 아래에 있어야만 몸통을 당길 수 있다.
    /// </summary>
    public bool TryGetSupportedPelvisPull(float minExtensionRatio, float minVerticalDrop, out Vector2 supportedPull)
    {
        supportedPull = Vector2.zero;
        if (isGround == false || PelvisPull.sqrMagnitude <= 0.0001f) return false;

        Vector2 pelvisToFoot = (Vector2)foot.transform.position - (Vector2)pelvis.transform.position;
        float maxReach = thighLength + shinLength;
        float extensionRatio = pelvisToFoot.magnitude / maxReach;
        float verticalDrop = pelvis.transform.position.y - foot.transform.position.y;

        // 수평으로 벌어진 다리나 충분히 펴지지 않은 다리는 몸통을 지지하지 못한다.
        if (extensionRatio < minExtensionRatio) return false;
        if (verticalDrop < minVerticalDrop) return false;

        // 두 조건 중 더 약한 쪽 값을 쓴다. 이렇게 하면 기준을 살짝 넘겼을 때 당기는 힘이 갑자기 툭 켜지지 않고 서서히 커진다.
        float extensionSupport = Mathf.InverseLerp(minExtensionRatio, 1f, extensionRatio);
        float verticalSupport = Mathf.InverseLerp(minVerticalDrop, minVerticalDrop + 0.5f, verticalDrop);
        supportedPull = PelvisPull * Mathf.Min(extensionSupport, verticalSupport);
        return supportedPull.sqrMagnitude > 0.0001f;
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
            preservedFootPosition = (Vector2)pelvis.transform.position + pelvisToFoot.normalized * maxReach;
        }

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
    /// 마우스를 아래로 누르는 만큼은 일어서는 힘(StandPressure)으로, 옆으로 움직이는 만큼은
    /// 몸통을 당기는 힘(PelvisPull)으로 바꾼다.
    /// </summary>
    bool TryMaintainPlantedFoot()
    {
        if (IsPlanted == false) return false;

        // 플레이어가 발을 들거나, 접지면이 사라지거나, 몸통과 발이 최대 길이보다 멀어지면 고정을 해제한다.
        bool wantsToRelease = mouseWorldPos.y > plantedPosition.y + plantedReleaseHeight;
        bool groundStillExists = Physics2D.OverlapCircle(plantedPosition, groundCheckRadius, LayerMask.GetMask("Ground")) != null;
        float maxReach = thighLength + shinLength - 0.01f;
        Vector2 pelvisToPlantedFoot = plantedPosition - (Vector2)pelvis.transform.position;
        bool legOverstretched = pelvisToPlantedFoot.magnitude > maxReach;

        if (wantsToRelease || !groundStillExists || legOverstretched)
        {
            IsPlanted = false;
            StandPressure = 0f;

            // 고정 해제 순간에도 발이 다리 최대 길이 밖에 남지 않도록 즉시 보정한다.
            if (legOverstretched) foot.transform.position = (Vector2)pelvis.transform.position + pelvisToPlantedFoot.normalized * maxReach;
            return false;
        }

        foot.transform.position = plantedPosition;
        isGround = true;
        isObstacle = true;

        // 마우스를 발보다 아래로 내린 만큼은 바닥을 누르는 힘(StandPressure)으로만 쓰고, PelvisPull에는 그 아래 방향 값을 넣지 않는다.
        Vector2 blockedInput = mouseWorldPos - plantedPosition;
        StandPressure = Mathf.Max(0f, -blockedInput.y);
        PelvisPull = new Vector2(blockedInput.x, Mathf.Max(0f, blockedInput.y));
        return true;
    }

    /// <summary>
    /// 이동이 끝난 발이 바닥에 닿아 있으면 현재 위치에 고정합니다.
    /// </summary>
    void TryPlantFoot()
    {
        StandPressure = 0f;
        if (isGround == false) return;

        IsPlanted = true;
        plantedPosition = foot.transform.position;
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
    /// 마우스 방향으로 찾은 바닥 지점, 또는 다리가 닿을 수 있는 가장 먼 지점을 발의 목표 위치로 쓴다.
    /// 마우스가 그 목표보다 더 멀리 있으면, 발이 못 간 나머지 거리를 PelvisPull에 담아 몸통을 당기는 데 쓴다.
    /// </summary>
    void LookMouse()
    {
        Vector2 targetPos;
        if (isObstacle == true)
        {
            // 골반과 마우스 사이에 Ground가 있으면 표면을 뚫지 않고 첫 충돌 지점까지만 이동한다.
            targetPos = raycastHit.point;
            PelvisPull = mouseWorldPos - targetPos;
        }
        else
        {
            // Ground가 없다면 마우스 위치를 허벅지와 종아리의 합산 길이 안으로 제한한다.
            float maxReach = thighLength + shinLength;
            Vector2 fp_dir = mouseWorldPos - (Vector2)pelvis.transform.position;
            targetPos = (fp_dir.magnitude > maxReach)
                ? (Vector2)pelvis.transform.position + fp_dir.normalized * maxReach 
                : mouseWorldPos;

            // 발이 최대 도달 범위를 넘어간 만큼은 골반을 당기는 데 쓴다.
            PelvisPull = mouseWorldPos - targetPos;
        }

        // 발을 즉시 순간 이동하지 않고 footSpeed로 접근시켜 입력이 급변해도 관절이 튀지 않게 한다.
        foot.transform.position = Vector2.MoveTowards(foot.transform.position, targetPos, footSpeed * Runner.DeltaTime);
    }

    /// <summary>
    /// 이동을 마친 실제 발 위치 주변에 Ground 레이어가 있는지 검사해 isGround를 갱신합니다.
    /// </summary>
    void FootGroundedFromFoot()
    {
        Collider2D hit = Physics2D.OverlapCircle(foot.transform.position, groundCheckRadius, LayerMask.GetMask("Ground"));
        isGround = hit != null;
    }

    /// <summary>
    /// 이름과 달리 발의 최종 접지를 판정하지 않습니다.
    /// 골반에서 마우스 방향으로 레이를 쏴 발이 먼저 닿아야 할 Ground 표면을 찾습니다.
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
