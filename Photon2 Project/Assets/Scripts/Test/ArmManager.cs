using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ArmManager : NetworkBehaviour
{
    [Header("Objects")]
    public GameObject sholder;
    [SerializeField] GameObject elbow;
    [SerializeField] GameObject hand;

    [Header("Bone Lengths (2-Bone IK)")]
    [SerializeField] float upperArmLength = 2.5f; // 어깨 - 팔꿈치
    [SerializeField] float forearmLength = 2.53f;  // 팔꿈치 - 손
    [SerializeField] int bendDirection = 1;       // 팔꿈치가 반대로 굽으면 -1로 바꿀 것

    [Header("Joint Movement")]
    [SerializeField] float handSpeed = 15f;             // 초당 손 목표가 이동할 수 있는 최대 거리
    [SerializeField] float shoulderRotationSpeed = 240f; // 어깨 관절이 초당 회전할 수 있는 최대 각도

    Vector2 mouseWorldPos;

    // 호스트 재시뮬레이션에서도 어깨 관절의 진행 상태가 동일하도록 틱 상태로 보관한다.
    [Networked] Vector2 HandTargetPosition { get; set; }
    [Networked] float ShoulderAngle { get; set; }
    [Networked] NetworkBool HasShoulderAngle { get; set; }

    /// <summary>
    /// 실제 손이 뻗은 방향과 길이를 기준으로 몸통을 좌우로 기울일 입력을 반환합니다.
    /// -1은 왼쪽, 1은 오른쪽이며 팔이 기준 길이보다 짧게 접혀 있으면 0을 반환합니다.
    /// </summary>
    public float GetTorsoLeanRequest(float minExtensionRatio)
    {
        if (sholder == null || hand == null) return 0f;

        Vector2 shoulderToHand = (Vector2)hand.transform.position - (Vector2)sholder.transform.position;
        float maxReach = upperArmLength + forearmLength;
        
        float extensionRatio = Mathf.Clamp01(shoulderToHand.magnitude / maxReach);
        float extensionWeight = Mathf.InverseLerp(minExtensionRatio, 1f, extensionRatio);
        
        if (extensionWeight <= 0f || shoulderToHand.sqrMagnitude <= 0.0001f) 
            return 0f;

        return shoulderToHand.normalized.x * extensionWeight;
    }

    public override void Spawned()
    {
        if (TryResolveShoulder())
        {
            FollowShoulderAnchor();

            if (Object.HasStateAuthority) 
                HandTargetPosition = hand.transform.position;
        }
    }

    void LateUpdate()
    {
        if (Object == null || Object.HasStateAuthority) return;
        if (TryResolveShoulder() == false) return;

        // 게스트의 사지 루트를 보간된 몸통 어깨에 고정한다.
        FollowShoulderAnchor();
    }

    bool TryResolveShoulder()
    {
        if (sholder != null) return true;

        var body = FindFirstObjectByType<BodyController>();
        if (body == null) return false;

        bool isLeftArm = gameObject.name.StartsWith("Left", System.StringComparison.Ordinal);
        sholder = isLeftArm ? body.sholderL : body.sholderR;
        return sholder != null;
    }

    public void SimulateHost(Vector2 inputMouseWorldPos)
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (TryResolveShoulder() == false) return;

        FollowShoulderAnchor();
        mouseWorldPos = inputMouseWorldPos;

        // 호스트만 손 목표와 어깨, 팔꿈치 관절을 순서대로 계산한다.
        if (HasShoulderAngle == false) HandTargetPosition = hand.transform.position;
        MoveHandTarget();
        SolveTwoBoneIK(true);

        Vector2 direction = (elbow.transform.position - hand.transform.position).normalized;
        hand.transform.up = direction;
    }

    /// <summary>
    /// 몸통 물리 이동과 회전이 끝난 뒤 새 어깨 위치를 기준으로 팔 연결을 다시 맞춥니다.
    /// 어깨 각도 보간은 FixedUpdateNetwork에서 이미 처리했으므로 현재 각도를 그대로 사용합니다.
    /// </summary>
    public void RefreshHostPoseAfterBodyMove()
    {
        if (Object == null || Object.HasStateAuthority == false) return;
        if (TryResolveShoulder() == false || HasShoulderAngle == false) return;

        FollowShoulderAnchor();
        SolveTwoBoneIK(false);

        Vector2 direction = (elbow.transform.position - hand.transform.position).normalized;
        hand.transform.up = direction;
    }

    void FollowShoulderAnchor()
    {
        transform.SetPositionAndRotation(sholder.transform.position, sholder.transform.rotation);
    }

    //코사인 법칙을 이용한 2-bone IK: 어깨-팔꿈치-손 삼각형에서 팔꿈치 위치를 구한다
    void SolveTwoBoneIK(bool updateShoulderAngle)
    {
        Vector2 sholderPos = sholder.transform.position;
        Vector2 handTargetPos = GetReachableHandTarget();

        Vector2 toHand = handTargetPos - sholderPos;
        float maxReach = upperArmLength + forearmLength;
        float minReach = Mathf.Abs(upperArmLength - forearmLength) + 0.01f;
        float d = Mathf.Clamp(toHand.magnitude, minReach, maxReach - 0.01f);

        float cosAngle = (upperArmLength * upperArmLength + d * d - forearmLength * forearmLength) / (2f * upperArmLength * d);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        float angle = Mathf.Acos(cosAngle);

        float baseAngle = Mathf.Atan2(toHand.y, toHand.x);
        float targetShoulderAngle = (baseAngle + angle * bendDirection) * Mathf.Rad2Deg;

        if (HasShoulderAngle == false)
        {
            ShoulderAngle = targetShoulderAngle;
            HasShoulderAngle = true;
        }
        else if (updateShoulderAngle)
        {
            ShoulderAngle = Mathf.MoveTowardsAngle(ShoulderAngle, targetShoulderAngle, shoulderRotationSpeed * Runner.DeltaTime);
        }

        float shoulderAngleRadians = ShoulderAngle * Mathf.Deg2Rad;
        Vector2 elbowPos = sholderPos + new Vector2(Mathf.Cos(shoulderAngleRadians), Mathf.Sin(shoulderAngleRadians)) * upperArmLength;
        elbow.transform.position = elbowPos;

        // 어깨가 목표 각도를 따라가는 동안에도 아래팔 길이가 변하지 않도록 손을 팔꿈치에서 다시 배치한다.
        Vector2 elbowToTarget = handTargetPos - elbowPos;
        Vector2 forearmDirection = elbowToTarget.sqrMagnitude > 0.0001f ? elbowToTarget.normalized : Vector2.down;
        hand.transform.position = elbowPos + forearmDirection * forearmLength;

        // 위팔 스프라이트가 어깨 방향을 향하도록 회전한다.
        Vector2 upperArmDir = (sholderPos - elbowPos).normalized;
        elbow.transform.up = upperArmDir;
    }

    /// <summary>
    /// 실제 손 관절과는 따로 관리하는 "목표 지점"을 마우스 쪽으로 옮긴다.
    /// 이 목표 지점은 팔이 닿을 수 있는 최대 범위 안으로 미리 제한해 둔다.
    /// </summary>
    void MoveHandTarget()
    {
        // 도달 가능한 최대 거리(위팔+아래팔) 안으로 제한한다.
        float maxReach = upperArmLength + forearmLength;
        Vector2 sh_dir = mouseWorldPos - (Vector2)sholder.transform.position;
        Vector2 targetPos = (sh_dir.magnitude > maxReach)
            ? (Vector2)sholder.transform.position + sh_dir.normalized * maxReach
            : mouseWorldPos;

        // 목표 위치로 즉시 스냅하지 않고 초당 handSpeed만큼 이동한다.
        HandTargetPosition = Vector2.MoveTowards(HandTargetPosition, targetPos, handSpeed * Runner.DeltaTime);
    }

    /// <summary>
    /// 몸통이 움직여 손 목표가 최대 도달 거리 밖으로 나간 경우 현재 틱에 사용할 위치를 보정합니다.
    /// </summary>
    Vector2 GetReachableHandTarget()
    {
        Vector2 shoulderPosition = sholder.transform.position;
        Vector2 shoulderToTarget = HandTargetPosition - shoulderPosition;
        float maxReach = upperArmLength + forearmLength - 0.01f;
        if (shoulderToTarget.magnitude <= maxReach) return HandTargetPosition;

        return shoulderPosition + shoulderToTarget.normalized * maxReach;
    }

    #region Test
    private void OnDrawGizmos()
    {
        if (sholder == null || elbow == null || hand == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(sholder.transform.position, elbow.transform.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(elbow.transform.position, hand.transform.position);

        //마우스(손 목표)가 도달할 수 있는 최대/최소 범위를 원으로 표시
        DrawReachCircle(sholder.transform.position, upperArmLength + forearmLength, Color.yellow);
        DrawReachCircle(sholder.transform.position, Mathf.Abs(upperArmLength - forearmLength), Color.red);

#if UNITY_EDITOR
        float upperArmDist = Vector3.Distance(sholder.transform.position, elbow.transform.position);
        float forearmDist = Vector3.Distance(elbow.transform.position, hand.transform.position);
        UnityEditor.Handles.Label((sholder.transform.position + elbow.transform.position) / 2, $"위팔 실측: {upperArmDist:F2}");
        UnityEditor.Handles.Label((elbow.transform.position + hand.transform.position) / 2, $"아래팔 실측: {forearmDist:F2}");
        UnityEditor.Handles.Label(sholder.transform.position + Vector3.up * (upperArmLength + forearmLength), "최대 도달 범위 (노랑)");
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
