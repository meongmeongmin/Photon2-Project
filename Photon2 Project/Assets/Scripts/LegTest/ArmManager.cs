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
    [SerializeField] float forearmLength = 2.5f;  // 팔꿈치 - 손
    [SerializeField] int bendDirection = 1;       // 팔꿈치가 반대로 굽으면 -1로 바꿀 것

    [Header("index")]
    [SerializeField] float handSpeed = 15f; //초당 손이 이동할 수 있는 최대 거리

    Vector2 mouseWorldPos;
    [Header("LineLenderer")]
    Vector3[] lenderVec;
    void Start()
    {
        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };
        //this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!GetInput(out NetworkInputData data)) return;

        mouseWorldPos = data.MouseWorldPos;

        //호스트만 실제로 손/팔꿈치 목표 위치를 계산해서 옮긴다
        HandLookMouse();
        SolveTwoBoneIK();

        //hand는 NetworkTransform이 붙어있어서, 매 렌더 프레임(Update)에서 회전을 바꾸면
        //다음 프레임에 마지막 틱 상태로 되돌려진다. 그래서 회전은 여기서 확정해야 한다.
        Vector2 direction = (elbow.transform.position - hand.transform.position).normalized;
        hand.transform.up = direction;
    }

    //코사인 법칙을 이용한 2-bone IK: 어깨-팔꿈치-손 삼각형에서 팔꿈치 위치를 구한다
    void SolveTwoBoneIK()
    {
        Vector2 sholderPos = sholder.transform.position;
        Vector2 handPos = hand.transform.position;

        Vector2 toHand = handPos - sholderPos;
        float maxReach = upperArmLength + forearmLength;
        float minReach = Mathf.Abs(upperArmLength - forearmLength) + 0.01f;
        float d = Mathf.Clamp(toHand.magnitude, minReach, maxReach - 0.01f);

        float cosAngle = (upperArmLength * upperArmLength + d * d - forearmLength * forearmLength) / (2f * upperArmLength * d);
        cosAngle = Mathf.Clamp(cosAngle, -1f, 1f);
        float angle = Mathf.Acos(cosAngle);

        float baseAngle = Mathf.Atan2(toHand.y, toHand.x);
        float elbowAngle = baseAngle + angle * bendDirection;

        Vector2 elbowPos = sholderPos + new Vector2(Mathf.Cos(elbowAngle), Mathf.Sin(elbowAngle)) * upperArmLength;
        elbow.transform.position = elbowPos;

        //위팔 스프라이트가 어깨 방향을 향하도록 회전
        Vector2 upperArmDir = (sholderPos - elbowPos).normalized;
        elbow.transform.up = upperArmDir;
    }

    void Update()
    {
        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };
        //this.GetComponent<LineRenderer>().SetPositions(lenderVec);
    }

    void HandLookMouse()
    {
        //도달 가능한 최대 거리(위팔+아래팔) 안으로 클램프
        float maxReach = upperArmLength + forearmLength;
        Vector2 sh_dir = mouseWorldPos - (Vector2)sholder.transform.position;
        Vector2 targetPos = (sh_dir.magnitude > maxReach)
            ? (Vector2)sholder.transform.position + sh_dir.normalized * maxReach
            : mouseWorldPos;

        //목표 위치로 즉시 스냅하지 않고, 초당 handSpeed만큼만 이동시켜 갑자기 튀지 않게 한다.
        hand.transform.position = Vector2.MoveTowards(hand.transform.position, targetPos, handSpeed * Runner.DeltaTime);
    }

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
}
