using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ArmManager : NetworkBehaviour
{
    // Start is called before the first frame update
    [Header("Objects")]
    public GameObject sholder;
    [SerializeField] GameObject elbow;
    [SerializeField] GameObject hand;

    [Header("Virtual Knee")]
    [SerializeField] Transform pf_center;

    [Header("index")]
    [SerializeField] float pf_dis;
    [SerializeField] float knee_dis;
    [SerializeField] float max_dis;

    Vector2 mouseWorldPos;
    [Header("LineLenderer")]
    Vector3[] lenderVec;
    void Start()
    {
        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };

        this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;

        max_dis = 25f;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        if (!GetInput(out NetworkInputData data)) return;

        mouseWorldPos = data.MouseWorldPos;

        //호스트만 실제로 손/팔꿈치 목표 위치를 계산해서 옮긴다
        footLookMouse();

        //hand는 NetworkTransform이 붙어있어서, 매 렌더 프레임(Update)에서 회전을 바꾸면
        //다음 프레임에 마지막 틱 상태로 되돌려진다. 그래서 회전은 여기서 확정해야 한다.
        Vector2 direction = (elbow.transform.position - hand.transform.position).normalized;
        hand.transform.up = direction;
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 pkdis = sholder.transform.position - hand.transform.position;
        pf_dis = Vector2.SqrMagnitude(pkdis);
        pf_center.position = (sholder.transform.position + hand.transform.position) / 2;

        //어깨-손 중심 방향으로 팔꿈치 위치 갱신
        float dis = (max_dis - pf_dis) / 5;
        elbow.transform.localPosition = new Vector2(-(dis / 2f), 0);

        knee_dis = max_dis - pf_dis;
        //반대편으로 넘어가지 않도록
        if (elbow.transform.localPosition.x > 0)
        {
            elbow.transform.position = pf_center.position;
        }

        //어깨-손 중심 방향으로 회전
        Vector2 pf_centerDir = pf_center.position - hand.transform.position;
        float dir = Mathf.Atan2(pf_centerDir.y, pf_centerDir.x) * Mathf.Rad2Deg + 270f;
        pf_center.rotation = Quaternion.Euler(new Vector3(0, 0, dir));

        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };
        this.GetComponent<LineRenderer>().SetPositions(lenderVec);
        HandLookElbow();

    }
    void footLookMouse()
    {
        //마우스와의 거리
        Vector2 pm_dir = (Vector2)sholder.transform.position - mouseWorldPos;
        float pmdis = Vector2.SqrMagnitude(pm_dir);
        //최대거리 이상이면 따라가지 않고 방향만 clamp
        if (pmdis < max_dis) //따라가기
        {
            hand.transform.position = mouseWorldPos;
        }
        else //최대거리 이상이면 방향만 따라가고 거리는 clamp
        {
            hand.transform.position = mouseWorldPos;
            Vector2 fp_dir = hand.transform.position - sholder.transform.position;
            Vector2 clampedPosition = sholder.transform.position + (Vector3)(fp_dir.normalized * 5);
            hand.transform.position = clampedPosition;
        }

    }
    void HandLookElbow()
    {
        Vector2 worldDirection = (sholder.transform.position - elbow.transform.position).normalized;

        // 부모 기준의 로컬 방향으로 변환
        Vector2 localDirection = pf_center.InverseTransformDirection(worldDirection);

        // 자식의 로컬 up이 타겟을 향하게 함
        elbow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);

    }
}
