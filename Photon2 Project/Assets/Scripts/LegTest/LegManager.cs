using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class LegManager : NetworkBehaviour
{
    // Start is called before the first frame update
    [Header("Objects")]
    public GameObject pelvis;
    [SerializeField] GameObject knee;
    public GameObject foot;

    [Header("Virtual Knee")]
    [SerializeField] Transform pf_center;

    [Header("index")]
    public float pf_dis;
    [SerializeField] float knee_dis;
    public float max_dis;
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
        //this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;
        max_dis = 25f;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!GetInput(out NetworkInputData data)) return;

        mouseWorldPos = data.MouseWorldPos;

        //드로 목표 위치가 특정거리 이상 벗어나지 않도록
        //드는 발과 의 거리를 통해 관절 위치 조정
        //뒤꿈치 처짐 계산
        //호스트만 실제로 발/무릎 목표 위치를 계산해서 옮긴다
        FootGrounded();
        FootLookMouse();
        FootGroundedFromFoot();

        //foot는 NetworkTransform이 붙어있어서, 매 렌더 프레임(Update)에서 회전을 바꾸면
        //다음 프레임에 마지막 틱 상태로 되돌려진다. 그래서 회전은 여기서 확정해야 한다.
        Vector2 direction = (knee.transform.position - foot.transform.position).normalized;
        foot.transform.up = direction;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 pkdis = ((Vector2)pelvis.transform.position - (Vector2)foot.transform.position);
        pf_dis = Vector2.SqrMagnitude(pkdis);
        pf_center.position = (pelvis.transform.position + foot.transform.position)/2;

        //무릎이 최대거리에 가까워질수록 관절 위치를 안쪽으로 당김
        float dis = (max_dis - pf_dis) / 5;
        knee.transform.localPosition = new Vector2((dis / 2f), 0);

        knee_dis = max_dis - pf_dis;
        //반대편으로 넘어가지 않도록
        if (knee.transform.localPosition.x < 0)
        {
            knee.transform.position = pf_center.position;
        }

        //골반-발 중심 방향으로 회전
        Vector2 pf_centerDir = pf_center.position - foot.transform.position;
        float dir = Mathf.Atan2(pf_centerDir.y, pf_centerDir.x) * Mathf.Rad2Deg + 270f;
        pf_center.rotation = Quaternion.Euler(new Vector3(0, 0, dir));

        lenderVec = new Vector3[] { pelvis.transform.position, knee.transform.position, foot.transform.position };
        //this.GetComponent<LineRenderer>().SetPositions(lenderVec);
        FootLookElbow();
    }
    void FootLookMouse()
    {
        //마우스와의 거리
        Vector2 pm_dir = (Vector2)pelvis.transform.position - mouseWorldPos;
        float pmdis = Vector2.SqrMagnitude(pm_dir);

        //장애물 위에 있으면 접지된 지점을 따라가고, 가까우면 마우스를 그대로 따라가서 무릎이 접히고,
        //멀면 고정 길이로 clamp되어 다리가 펴진다.
        Vector2 targetPos;
        if (isObstacle == true)
        {
            targetPos = raycastHit.point;
        }
        else if (pmdis < max_dis) //따라가기 (무릎이 접힘)
        {
            targetPos = mouseWorldPos;
        }
        else //최대거리 이상이면 방향만 따라가고 거리는 clamp (다리가 펴짐)
        {
            Vector2 fp_dir = mouseWorldPos - (Vector2)pelvis.transform.position;
            targetPos = (Vector2)pelvis.transform.position + fp_dir.normalized * 5f;
        }

        //목표 위치로 즉시 스냅하지 않고, 초당 footSpeed만큼만 이동시켜 무릎 굽힘이 갑자기 튀지 않게 한다.
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
        raycastHit = Physics2D.Raycast(pelvis.transform.position, (mouseWorldPos - (Vector2)pelvis.transform.position).normalized, 5f, LayerMask.GetMask("Ground"));
        if (raycastHit)
        {
            isObstacle = true;
        }
        else
        {
            isObstacle = false;
        }
    }
    void FootLookElbow()
    {
        Vector2 worldDirection = (pelvis.transform.position - knee.transform.position).normalized;

        // 부모 기준의 로컬 방향으로 변환
        Vector2 localDirection = pf_center.InverseTransformDirection(worldDirection);

        // 자식의 로컬 up이 타겟을 향하게 함
        knee.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);
    }
    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawRay(foot.transform.position, (mouseWorldPos - (Vector2)foot.transform.position).normalized * 0.5f);

        //Gizmos.color = Color.cyan;
        //Gizmos.DrawRay(pelvis.transform.position, (mouseWorldPos - (Vector2)pelvis.transform.position).normalized * 5f);
    }
}
