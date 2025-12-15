using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

//using UnityEditor.ShaderKeywordFilter;
using UnityEngine;

public class LegManager : MonoBehaviour
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

    [Header("MousePos")]
    //MousePos getMousePos;
    public Transform mousePos;

    [Header("Foot")]
    public bool isGround;
    public bool isObstacle;
   
    RaycastHit2D raycastHit;
    PhotonView photonView;

    [Header("LineLenderer")]
    [SerializeField] Vector3[] lenderVec;
    void Start()
    {
        lenderVec = new Vector3[] { pelvis.transform.position, knee.transform.position, foot.transform.position };
        this.GetComponent<LineRenderer>().positionCount = lenderVec.Length;
        photonView = GetComponent<PhotonView>();
        //getMousePos = mousePos.GetComponent<MousePos>();
        max_dis = 25f;   
    }

    // Update is called once per frame
    void Update()
    {
        
        if(photonView.IsMine == false) return;
        //this.mousePos.position = getMousePos.mousePos;
        //占쏙옙占쏙옙 占쏙옙占쏙옙占� 특占쏙옙占신몌옙 占싱삼옙 占쏙옙占쏘나占쏙옙 占십듸옙占쏙옙
        //占쏙옙占쏙옙占쏙옙 占쌩곤옙 占쏙옙占쏙옙占� 占신몌옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙
        //占쏙옙占쏙옙 占쏙옙腑占쏙옙占� 占쏙옙占쏙옙 占쏙옙占쏙옙
        //占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙치
        Vector2 pkdis = pelvis.transform.position - foot.transform.position;
        pf_dis = Vector2.SqrMagnitude(pkdis);
        pf_center.position = (pelvis.transform.position + foot.transform.position)/2;

        //占쏙옙占쏢무몌옙占쏙옙 占쏙옙腑占� 占쏙옙占쏙옙 占쏙옙치占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙치占쏙옙화
        //float dis = Mathf.Sqrt((max_dis - pf_dis)); 
        float dis = (max_dis - pf_dis) / 5; //占쏙옙占쏙옙占실곤옙 占신몌옙占쏙옙 占쏙옙占싱곤옙 5占싱기에 占싱뤄옙占쏙옙 占쏙옙.占쏙옙 占싣니띰옙 占쏙옙占쏙옙占싱댐옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙 占쏙옙占쌔억옙占쏙옙
        knee.transform.localPosition = new Vector2((dis / 2f), 0);

        knee_dis = max_dis - pf_dis;
        //占쏙옙占쏙옙占쏙옙占싫되듸옙占쏙옙
        if (knee.transform.localPosition.x < 0)
        {
            knee.transform.position = pf_center.position;
        }
        //占쏙옙占쏢무몌옙占쏙옙 占쏙옙占쏙옙 처占쌕븝옙占쏙옙占쏙옙
        Vector2 pf_centerDir = pf_center.position - foot.transform.position;
        float dir = Mathf.Atan2(pf_centerDir.y, pf_centerDir.x) * Mathf.Rad2Deg + 270f;
        pf_center.rotation = Quaternion.Euler(new Vector3(0,0,dir));
        //占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쌘쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙載� 占쏙옙占쏙옙占실뤄옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 X
        //knee.transform.rotation = pf_center.transform.rotation;

        //占쏙옙占쏙옙 占쏙옙占쎌스占쏙옙 占식다븝옙占쏙옙占쏙옙
        FootGrounded();
        FootLookMouse();
        FootGroundedFromFoot();


        //lineRanderer ( 占쌈쏙옙 )
        //this.GetComponent<LineRenderer>().SetPosition(0, pelvis.transform.position);
        //this.GetComponent<LineRenderer>().SetPosition(1, knee.transform.position);
        //this.GetComponent<LineRenderer>().SetPosition(2, foot.transform.position);
        lenderVec = new Vector3[] { pelvis.transform.position, knee.transform.position, foot.transform.position };
        //DrawLineLender();
        FootLookElbow();
    }
    private void LateUpdate()
    {

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
    void FootLookMouse()
    {
 
        
        //占쏙옙占쎌스占쏙옙 占쏙옙占쏙옙占� 占신몌옙
        Vector2 pm_dir = pelvis.transform.position - mousePos.position;
        float pmdis = Vector2.SqrMagnitude(pm_dir);
        //占쏙옙占쏙옙 占쏙옙占쎌스 占쏙옙占쏢가깍옙 占쏙옙 占쌕라보깍옙 maxdis占쏙옙 占쏙옙占쏙옙占싹깍옙.

        //占쌩곤옙占쏙옙 占쏙옙占� Grounded占쏙옙 占쌕뀐옙占� 占쏙옙占쏙옙.
        if (isObstacle == true) //占쏙옙占십울옙 占쏙옙占쏙옙占� 占쏙옙占쏙옙 占쏙옙占쏢가댐옙 占쏙옙황占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占십는댐옙. 占쏙옙占쏙옙占심쏙옙트占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占� 占쏙옙 占싹댐옙.
        {
            foot.transform.position = raycastHit.point;
        }
        else
        {

            if (pmdis < max_dis) //占쏙옙占쏢가깍옙
            {
                //foot.GetComponent<Rigidbody2D>().MovePosition(mousePos.position);//rigidbody占쏙옙 占싱듸옙占쏙옙占싼쇽옙 占썸돌占쌔븝옙占쏙옙
                foot.transform.position = mousePos.position;
            }
            else //占쌕라보깍옙 占쏙옙占쏙옙 占쌕라보는깍옙占쏙옙, 占쏙옙占쏙옙占� 占쌩쏙옙占쏙옙占쏙옙 占쌕라보곤옙 占싹뤄옙占쏙옙?
            {
                //Vector2 fm_dir = foot.transform.position - mousePos.position;
                //float dir = Mathf.Atan2(fm_dir.y, fm_dir.x) * Mathf.Rad2Deg;
                //foot.transform.rotation = Quaternion.Euler(new Vector3(0, 0, dir));
                foot.transform.position = mousePos.position;
                Vector2 fp_dir = foot.transform.position - pelvis.transform.position;
                //占쏙옙駙占쏙옙占� 占쏙옙占쏘나占쏙옙 占십듸옙占쏙옙.
                Vector2 clampedPosition = pelvis.transform.position + (Vector3)(fp_dir.normalized * 5); //*占쏙옙 占싹몌옙 占쏙옙占싹깍옙占쏙옙
                foot.transform.position = clampedPosition;
                //foot.GetComponent<Rigidbody2D>().MovePosition(clampedPosition);
            }
        }

    }
    //占쌩울옙占쏙옙 占쏙옙占�
    void FootGroundedFromFoot()
    {
        if (Physics2D.Raycast(foot.transform.position, (mousePos.position - foot.transform.position).normalized, 0.5f, LayerMask.GetMask("Ground")))
        {
            this.isGround = true;
        }
        else
        {
            this.isGround = false;
        }
    }

    //占쏙옙駙占쏙옙占� 占쏙옙占�
    void FootGrounded()
    {
        raycastHit = Physics2D.Raycast(pelvis.transform.position, (mousePos.transform.position - pelvis.transform.position).normalized, 5f, LayerMask.GetMask("Ground"));
        if (raycastHit)
        {
            //占싸드럽占쏙옙 占쏙옙占쏙옙占싱뤄옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占� 占쏙옙占쏙옙占쏙옙 占쏙옙駙占쏙옙占� 占쌩삼옙占쏙옙 占쏙옙占쏙옙占심쏙옙트 hit占쏙옙 占쌈뱄옙占쏙옙占쏙옙?
            isObstacle = true;
        }
        else
        {
            isObstacle = false;
        }
    }
    void FootLookElbow()
    {
        Vector2 direction = (knee.transform.position - foot.transform.position).normalized;
        foot.transform.up = direction;

        //Vector2 direction2 = (pelvis.transform.position - knee.transform.position).normalized;
        //knee.transform.up = direction;

        Vector2 worldDirection = (pelvis.transform.position - knee.transform.position).normalized;

        // 부모 기준의 로컬 방향으로 변환
        Vector2 localDirection = pf_center.InverseTransformDirection(worldDirection);

        // 자식의 로컬 up이 타겟을 향하게 함
        knee.transform.localRotation = Quaternion.FromToRotation(Vector3.up, localDirection);
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(foot.transform.position, (mousePos.position - foot.transform.position).normalized * 0.5f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(pelvis.transform.position, (mousePos.transform.position - pelvis.transform.position).normalized * 5f);
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
