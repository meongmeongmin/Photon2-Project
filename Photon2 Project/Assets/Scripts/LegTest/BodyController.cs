using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

public class BodyController : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] GameObject body;
    public LegManager LeftLeg;
    public LegManager RightLeg;
    [SerializeField] GameObject pelvisL;
    [SerializeField] GameObject pelvisR;

    [Header("isGrounded")]
    [SerializeField] bool leftFootGrounded;
    [SerializeField] bool rightFootGrounded;
    [SerializeField] bool isFootsGrounded;
    [Header("Index")]
    [SerializeField] float speed;
    [SerializeField] float radius;
    [Header("PelvisChildPos")]
    [SerializeField] Vector3 pelvisL_ChildPos;
    [SerializeField] Vector3 pelvisR_ChildPos;
    // Start is called before the first frame update
    private void Awake()
    {
        
    }
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;

        isFootsGrounded = (leftFootGrounded || rightFootGrounded);
        if(isFootsGrounded == true) //발이 하나라도 닿아 있을때
        {
            this.GetComponent<Rigidbody2D>().gravityScale = 0;
            this.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            Vector3 newPosition = this.transform.position; // C의 새로운 위치를 계산

            // 키 입력 등을 통해 C의 이동 방향을 결정
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");

            if (x !=0 || y != 0)
            {
                newPosition += new Vector3(x, y, 0) * speed * Time.deltaTime;

                // 합집합 영역 내에 있는지 확인 이 안에 있을때만 이동 가능 하지만 부드럽게 움직이려면?
                if (IsInUnion(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius))
                {
                    //움직임을 제어하는것이 아니라 제어 한 후에 위치를 넣어준다.
                    this.transform.position = newPosition; // 유효한 위치로 C를 이동
                }
                else
                {
                    Vector3 clampedPosition = ClampToBoundary(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius);
                    this.transform.position = clampedPosition;
                }
            }
        }

        if (isFootsGrounded == false) //발이 하나도 닿지 않았을때
        {
            this.GetComponent<Rigidbody2D>().gravityScale = 1;
        }

        //SetLegParents(0,LeftLeg.transform, LeftLeg.transform.Find("pf_Center").Find("knee"), LeftLeg.transform.transform.Find("foot"));
        //SetLegParents(3,RightLeg.transform, RightLeg.transform.Find("pf_Center").Find("knee"), LeftLeg.transform.transform.Find("foot"));
        //SetLegParents(pelvisL.transform, pelvisL.transform.Find("knee"), transform.Find("Hand"));
        //SetLegParents(pelvisL.transform, pelvisL.transform.Find("knee"), transform.Find("Hand"));

    }

    bool IsInUnion(Vector3 point, Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
    {
        bool inCircleA = (point.x - centerA.x) * (point.x - centerA.x) + (point.y - centerA.y) * (point.y - centerA.y) <= radiusA * radiusA;
        bool inCircleB = (point.x - centerB.x) * (point.x - centerB.x) + (point.y - centerB.y) * (point.y - centerB.y) <= radiusB * radiusB;

        return inCircleA && inCircleB; // 두 원 중 하나에 포함되는지 확인
    }
    Vector3 ClampToBoundary(Vector3 cPosition, Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
    {
        //// A와 B의 반경 안쪽 경계로 위치를 제한
        Vector3 dirA = cPosition - centerA;
        Vector3 dirB = cPosition - centerB;


        if (dirA.sqrMagnitude > radiusA * radiusA && !(dirB.sqrMagnitude > radiusB * radiusB))
        {
            cPosition = centerA + dirA.normalized * radiusA;
        }


        if (dirB.sqrMagnitude > radiusB * radiusB && !(dirA.sqrMagnitude > radiusA * radiusA))
        {
            cPosition = centerB + dirB.normalized * radiusB;
        }

        if (dirA.sqrMagnitude > radiusA * radiusA && dirB.sqrMagnitude > radiusB * radiusB)
        {
            cPosition = ((centerA + dirA.normalized * radiusA) + (centerB + dirB.normalized * radiusB)) / 2;
        }

        return cPosition;
    }
    
    void SetLegParents(int index,Transform start, Transform midle, Transform target) //index는 0부터 3의 배수로 작성하라
    {
        this.GetComponent<LineRenderer>().SetPosition(index, start.transform.position);
        this.GetComponent<LineRenderer>().SetPosition(index + 1, midle.transform.position);
        this.GetComponent<LineRenderer>().SetPosition(index + 2, target.transform.position);
        //this.GetComponent<LineRenderer>().SetPosition(index + 3, new Vector3(float.NaN, float.NaN, float.NaN));

    }

    //void OnDrawGizmos()
    //{
    //    // 원을 시각적으로 표시
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(LeftLeg.foot.transform.position, radius);
    //    Gizmos.color = Color.blue;
    //    Gizmos.DrawWireSphere(RightLeg.foot.transform.position, radius);
    //}
    //그냥 호리젠탈로 키 받고
    //ground 체크해서
    //하나라도 닿아 있으면 상하좌우 움직이기
    //두개 모두 닿아 있으면 둘다 최대거리 계산해서 움직이기.

    //땅에 닿아 있을때도 골반이 어떻게 움직일 것인지 정교하게 시스템기획을 해야할듯

    //float x = Input.GetAxisRaw("Horizontal");
    //float y = Input.GetAxisRaw("Vertical");
    //if(leftFootGrounded == true && rightFootGrounded == true)
    //{
    //    MovingPelvis(x, y);
    //}
    //else if(leftFootGrounded == true && rightFootGrounded == false)
    //{
    //    MovingPelvis(x, y);
    //}
    //else if (leftFootGrounded == false && rightFootGrounded == true)
    //{
    //    MovingPelvis(x,y);
    //}

    //if (leftFootGrounded == false && rightFootGrounded == false)
    //{
    //    this.GetComponent<Rigidbody2D>().gravityScale = 1;
    //}
    //else
    //{
    //    this.GetComponent<Rigidbody2D>().gravityScale = 0;
    //}
    //void MovingPelvis(float x,float y)
    //{
    //    this.transform.position = this.transform.position + new Vector3(x, y, 0) * Time.deltaTime * speed;
    //}
}
