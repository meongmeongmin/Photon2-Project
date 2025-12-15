using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class ArmManager : MonoBehaviour
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

    [Header("MousePos")]
    MousePos getMousePos;
    public Transform mousePos;
    PhotonView photonView;
    [Header("LineLenderer")]
    Vector3[] lenderVec;
    void Start()
    {
        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };

        photonView = GetComponent<PhotonView>();
        //getMousePos = mousePos.GetComponent<MousePos>();
        this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;

        max_dis = 25f;
    }

    // Update is called once per frame
    void Update()
    {

        if (photonView.IsMine == false) return;
        //this.mousePos.position = getMousePos.mousePos;
        //손이 어깨의 특정거리 이상 벗어나지 않도록
        //무릅이 손과 어깨의 거리에 따라 앞으로 접히도록
        //손이 어깨과의 각도 제한
        //가상 팔꿈치 위치
        Vector2 pkdis = sholder.transform.position - hand.transform.position;
        pf_dis = Vector2.SqrMagnitude(pkdis);
        pf_center.position = (sholder.transform.position + hand.transform.position) / 2;

        //가상팔꿈치과 어깨과 손의 위치에 따른 팔꿈치 위치변화
        float dis = (max_dis - pf_dis) / 5; //포지션과 거리의 차이가 5이기에 이렇게 함.
        elbow.transform.localPosition = new Vector2(-(dis / 2f), 0);

        knee_dis = max_dis - pf_dis;
        //역관절안되도록
        if (elbow.transform.localPosition.x > 0)
        {
            elbow.transform.position = pf_center.position;
        }
        //가상팔꿈치이 손을 처다보도록
        Vector2 pf_centerDir = pf_center.position - hand.transform.position;
        float dir = Mathf.Atan2(pf_centerDir.y, pf_centerDir.x) * Mathf.Rad2Deg + 270f;
        pf_center.rotation = Quaternion.Euler(new Vector3(0, 0, dir));
        //가상 팔꿈치의 자식으로 팔꿈치이 들어가 있으므로 각도 수정은 X
        //knee.transform.rotation = pf_center.transform.rotation;

        //손이 마우스를 쳐다보도록
        footLookMouse();
        //lineRanderer ( 임시 )
        //this.GetComponent<LineRenderer>().SetPosition(0, sholder.transform.position);
        //this.GetComponent<LineRenderer>().SetPosition(1, elbow.transform.position);
        //this.GetComponent<LineRenderer>().SetPosition(2, hand.transform.position);
        lenderVec = new Vector3[] { sholder.transform.position, elbow.transform.position, hand.transform.position };
        //DrawLineLender();
        HandLookElbow();

    }
    void DrawLineLender()
    {
        object[] serializedPoints = new object[lenderVec.Length];
        this.GetComponent<LineRenderer>().SetPositions(lenderVec);
        for (int i = 0; i < lenderVec.Length; i++)
        {
            serializedPoints[i] = lenderVec[i];

        }
        photonView.RPC("SyncLine", RpcTarget.Others, serializedPoints);
    }
    void footLookMouse()
    {
        //마우스와 어깨의 거리
        Vector2 pm_dir = sholder.transform.position - mousePos.position;
        float pmdis = Vector2.SqrMagnitude(pm_dir);
        //손이 마우스 따라가기 및 바라보기 maxdis로 제한하기.
        if (pmdis < max_dis) //따라가기
        {
            hand.transform.position = mousePos.position;
        }
        else //바라보기 손이 바라보는구나, 어깨을 중심으로 바라보게 하려면?
        {
            //Vector2 fm_dir = foot.transform.position - mousePos.position;
            //float dir = Mathf.Atan2(fm_dir.y, fm_dir.x) * Mathf.Rad2Deg;
            //foot.transform.rotation = Quaternion.Euler(new Vector3(0, 0, dir));
            hand.transform.position = mousePos.position;
            Vector2 fp_dir = hand.transform.position - sholder.transform.position;
            Vector2 clampedPosition = sholder.transform.position + (Vector3)(fp_dir.normalized * 5); //*를 하면 편하구나
            hand.transform.position = clampedPosition;
        }

    }
    void HandLookElbow()
    {
        Vector2 direction = (elbow.transform.position - hand.transform.position).normalized;
        hand.transform.up = direction;

        //Vector2 direction2 = (sholder.transform.position - elbow.transform.position).normalized;
        //elbow.transform.up = direction;
        Vector2 worldDirection = (sholder.transform.position - elbow.transform.position).normalized;

        // 부모 기준의 로컬 방향으로 변환
        Vector2 localDirection = pf_center.InverseTransformDirection(worldDirection);

        // 자식의 로컬 up이 타겟을 향하게 함
        elbow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);

    }
    [PunRPC]
    void SyncLine(object[] serializedPoints)
    {
        Vector3[] points = new Vector3[serializedPoints.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = (Vector3)serializedPoints[i];

        this.GetComponent<LineRenderer>().positionCount = points.Length;
        this.GetComponent<LineRenderer>().SetPositions(points);


    }

}
