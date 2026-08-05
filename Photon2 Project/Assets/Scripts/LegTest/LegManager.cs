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

    [Header("index")]
    public float pf_dis;
    [SerializeField] float footSpeed = 15f; //초당 발이 이동할 수 있는 최대 거리

    [Header("Foot")]
    public bool isGround;
    public bool isObstacle;

    RaycastHit2D raycastHit;
    Vector2 mouseWorldPos;

    [Header("LineLenderer")]
    [SerializeField] Vector3[] lenderVec;
    void Start()
    {
        lenderVec = new Vector3[] { pelvis.transform.position, knee.transform.position, foot.transform.position };
        this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!GetInput(out NetworkInputData data)) return;

        mouseWorldPos = data.MouseWorldPos;

        //호스트만 실제로 발/무릎 목표 위치를 계산해서 옮긴다
        FootGrounded();
        FootLookMouse();
        FootGroundedFromFoot();
        SolveTwoBoneIK(); //실제 발 위치를 기준으로 무릎 위치를 삼각형 계산으로 구한다

        //foot는 NetworkTransform이 붙어있어서, 매 렌더 프레임(Update)에서 회전을 바꾸면
        //다음 프레임에 마지막 틱 상태로 되돌려진다. 그래서 회전은 여기서 확정해야 한다.
        Vector2 direction = (knee.transform.position - foot.transform.position).normalized;
        foot.transform.up = direction;
    }

    //코사인 법칙을 이용한 2-bone IK: 골반-무릎-발 삼각형에서 무릎 위치를 구한다
    void SolveTwoBoneIK()
    {
        Vector2 pelvisPos = pelvis.transform.position;
        Vector2 footPos = foot.transform.position;

        Vector2 toFoot = footPos - pelvisPos;
        float maxReach = thighLength + shinLength;
        float minReach = Mathf.Abs(thighLength - shinLength) + 0.01f;
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

    // Update is called once per frame (순수 시각 요소만 갱신, 매 프레임 실행해도 안전)
    void Update()
    {
        pf_dis = Vector2.SqrMagnitude((Vector2)pelvis.transform.position - (Vector2)foot.transform.position);

        lenderVec = new Vector3[] { pelvis.transform.position, knee.transform.position, foot.transform.position };
        this.GetComponent<LineRenderer>().SetPositions(lenderVec);
    }

    void FootLookMouse()
    {
        Vector2 targetPos;
        if (isObstacle == true)
        {
            //장애물 위에 있으면 접지된 지점을 따라간다
            targetPos = raycastHit.point;
        }
        else
        {
            //도달 가능한 최대 거리(허벅지+종아리) 안으로 클램프
            float maxReach = thighLength + shinLength;
            Vector2 fp_dir = mouseWorldPos - (Vector2)pelvis.transform.position;
            targetPos = (fp_dir.magnitude > maxReach)
                ? (Vector2)pelvis.transform.position + fp_dir.normalized * maxReach 
                : mouseWorldPos;
        }

        //목표 위치로 즉시 스냅하지 않고, 초당 footSpeed만큼만 이동시켜 갑자기 튀지 않게 한다.
        foot.transform.position = Vector2.MoveTowards(foot.transform.position, targetPos, footSpeed * Runner.DeltaTime);
    }

    //바닥에 닿았는지 (발 기준)
    void FootGroundedFromFoot()
    {
        if (Physics2D.Raycast(foot.transform.position, (mouseWorldPos - (Vector2)foot.transform.position).normalized, 0.5f, LayerMask.GetMask("Ground")))
        {
            this.isGround = true;
        }
        else
        {
            this.isGround = false;
        }
    }

    //골반 기준 장애물 체크
    void FootGrounded()
    {
        float maxReach = thighLength + shinLength;
        raycastHit = Physics2D.Raycast(pelvis.transform.position, (mouseWorldPos - (Vector2)pelvis.transform.position).normalized, maxReach, LayerMask.GetMask("Ground"));
        if (raycastHit)
        {
            isObstacle = true;
        }
        else
        {
            isObstacle = false;
        }
    }

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
}
