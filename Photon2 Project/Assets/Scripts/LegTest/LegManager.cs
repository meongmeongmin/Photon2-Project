using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LegManager : NetworkBehaviour
{
    [Header("Objects")]
    public GameObject pelvis;
    [SerializeField] GameObject knee;
    public GameObject foot;

    [Header("Bone Lengths (2-Bone IK)")]
    [SerializeField] float thighLength = 2.5f; // 골반 - 무릎
    [SerializeField] float shinLength = 2.52f;  // 무릎 - 발
    [SerializeField] int bendDirection = 1;    // 무릎이 반대로 굽으면 -1로 바꿀 것

    [SerializeField] float footSpeed = 15f; //초당 발이 이동할 수 있는 최대 거리

    [Header("Foot")]
    float groundCheckRadius = 0.15f;
    // 고정된 발보다 이 높이 이상 마우스를 올리면 발 고정을 해제한다.
    [SerializeField] float plantedReleaseHeight = 0.5f;
    public bool isGround;
    public bool isObstacle;

    RaycastHit2D raycastHit;
    Vector2 mouseWorldPos;

    /// <summary>
    /// 발이 목표 위치까지 도달하지 못했을 때 골반에 요청하는 월드 좌표 기준 이동량입니다.
    /// 물리적인 힘이 아니라 마우스 목표 위치와 발의 도달 가능 위치 사이의 차이를 나타냅니다.
    /// </summary>
    public Vector2 PelvisPull { get; private set; }

    /// <summary>
    /// 발이 바닥의 한 지점에 고정되어 몸통을 지지할 수 있는 상태인지 나타냅니다.
    /// </summary>
    public bool IsPlanted { get; private set; }

    /// <summary>
    /// 플레이어가 고정된 발보다 아래로 마우스를 내려 바닥을 누르는 입력의 크기입니다.
    /// </summary>
    public float StandPressure { get; private set; }

    // 발이 처음 접지되어 고정된 월드 좌표
    Vector2 plantedPosition;

    public override void Spawned()
    {
        if (TryResolvePelvis())
        {
            FollowPelvisAnchor();
        }
    }

    void LateUpdate()
    {
        if (Object == null || Object.HasStateAuthority) return;
        if (TryResolvePelvis() == false) return;

        // 게스트의 사지 루트를 보간된 몸통 골반에 고정한다.
        FollowPelvisAnchor();
    }

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

        // 이미 고정된 발은 그 자리를 유지하고, 아니면 마우스를 따라 이동한 뒤 접지를 시도한다.
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

    public bool TryGetSupportedPelvisPull(float minExtensionRatio, float minVerticalDrop, out Vector2 supportedPull)
    {
        supportedPull = Vector2.zero;
        if (!isGround || PelvisPull.sqrMagnitude <= 0.0001f) return false;

        Vector2 pelvisToFoot = (Vector2)foot.transform.position - (Vector2)pelvis.transform.position;
        float maxReach = thighLength + shinLength;
        float extensionRatio = pelvisToFoot.magnitude / maxReach;
        float verticalDrop = pelvis.transform.position.y - foot.transform.position.y;

        // 수평으로 벌어진 다리나 충분히 펴지지 않은 다리는 몸통을 지지하지 못한다.
        if (extensionRatio < minExtensionRatio) return false;
        if (verticalDrop < minVerticalDrop) return false;

        // 임계점을 넘는 순간 골반이 갑자기 끌리지 않도록 지지력을 0에서 1까지 서서히 키운다.
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
        if (!IsPlanted || StandPressure < minPressure) return false;

        float targetReach = (thighLength + shinLength) * targetExtensionRatio;
        float horizontalDistance = Mathf.Abs(pelvis.transform.position.x - plantedPosition.x);
        if (horizontalDistance >= targetReach) return false;

        // 피타고라스 정리로 목표 다리 길이에서 가능한 골반의 수직 높이를 구한다.
        float targetVerticalDistance = Mathf.Sqrt(targetReach * targetReach - horizontalDistance * horizontalDistance);
        float currentVerticalDistance = pelvis.transform.position.y - plantedPosition.y;

        pressure = StandPressure;
        riseDistance = Mathf.Max(0f, targetVerticalDistance - currentVerticalDistance);
        return riseDistance > 0.001f;
    }

    /// <summary>
    /// 고정된 발을 접지 지점에 유지하고 플레이어 입력을 기립 압력과 골반 이동 요청으로 분리합니다.
    /// </summary>
    bool TryMaintainPlantedFoot()
    {
        if (!IsPlanted) return false;

        // 마우스를 위로 올리거나 바닥이 사라지면 발 고정을 해제한다.
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

        // 아래쪽 입력은 기립 압력, 좌우 및 위쪽 입력은 골반 이동 요청으로 사용한다.
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
        if (!isGround) return;

        IsPlanted = true;
        plantedPosition = foot.transform.position;
    }

    void FollowPelvisAnchor()
    {
        transform.SetPositionAndRotation(pelvis.transform.position, pelvis.transform.rotation);
    }

    //코사인 법칙을 이용한 2-bone IK: 골반-무릎-발 삼각형에서 무릎 위치를 구한다
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

        float d = Mathf.Clamp(toFoot.magnitude, minReach, maxReach - 0.01f);

        float cosAngle = (thighLength * thighLength + d * d - shinLength * shinLength) / (2f * thighLength * d);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        float angle = Mathf.Acos(cosAngle);

        float baseAngle = Mathf.Atan2(toFoot.y, toFoot.x);
        float kneeAngle = baseAngle + angle * bendDirection;

        Vector2 kneePos = pelvisPos + new Vector2(Mathf.Cos(kneeAngle), Mathf.Sin(kneeAngle)) * thighLength;
        knee.transform.position = kneePos;

        //허벅지 스프라이트가 골반 방향을 향하도록 회전
        Vector2 thighDir = (pelvisPos - kneePos).normalized;
        knee.transform.up = thighDir;
    }

    void LookMouse()
    {
        Vector2 targetPos;
        if (isObstacle == true)
        {
            //장애물 위에 있으면 접지된 지점을 따라간다
            targetPos = raycastHit.point;
            PelvisPull = mouseWorldPos - targetPos;
        }
        else
        {
            //도달 가능한 최대 거리(허벅지+종아리) 안으로 클램프
            float maxReach = thighLength + shinLength;
            Vector2 fp_dir = mouseWorldPos - (Vector2)pelvis.transform.position;
            targetPos = (fp_dir.magnitude > maxReach)
                ? (Vector2)pelvis.transform.position + fp_dir.normalized * maxReach 
                : mouseWorldPos;

            // 발의 최대 도달 범위를 넘은 입력은 골반을 당기는 이동량으로 사용한다.
            PelvisPull = mouseWorldPos - targetPos;
        }

        //목표 위치로 즉시 스냅하지 않고, 초당 footSpeed만큼만 이동시켜 갑자기 튀지 않게 한다.
        foot.transform.position = Vector2.MoveTowards(foot.transform.position, targetPos, footSpeed * Runner.DeltaTime);
    }

    //바닥에 닿았는지 (발 기준)
    void FootGroundedFromFoot()
    {
        Collider2D hit = Physics2D.OverlapCircle(foot.transform.position, groundCheckRadius, LayerMask.GetMask("Ground"));
        isGround = hit != null;
    }

    //골반 기준 장애물 체크
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

        // 마우스가 다리 최대 길이보다 가까우면 마우스까지만 검사한다.
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

        //마우스(발 목표)가 도달할 수 있는 최대/최소 범위를 원으로 표시
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

    //2D 평면(XY) 위에 원을 그리는 헬퍼 (Gizmos.DrawWireSphere는 3D라서 2D 게임에선 이게 더 보기 편하다)
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
