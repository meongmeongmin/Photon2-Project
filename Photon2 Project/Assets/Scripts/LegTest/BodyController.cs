using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class BodyController : NetworkBehaviour
{
    [Header("Objects")]
    //[SerializeField] GameObject body;
    public LegManager LeftLeg;
    public LegManager RightLeg;

    [Header("Limb Anchors (Robot 프리팹에서 직접 연결, 이름으로 찾지 않음)")]
    public GameObject sholderL;
    public GameObject sholderR;
    public GameObject pelvisL;
    public GameObject pelvisR;

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

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return; //공유 몸통 물리는 호스트만 시뮬레이션한다

        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;

        isFootsGrounded = (leftFootGrounded || rightFootGrounded);
        if(isFootsGrounded == true) //둘 중 하나라도 접지된 상태
        {
            this.GetComponent<Rigidbody2D>().gravityScale = 0;
            this.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            Vector3 newPosition = this.transform.position; // C의 다음 위치를 계산

            // 키 입력에 따라 C가 이동할 방향을 결정
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");

            if (x !=0 || y != 0)
            {
                newPosition += new Vector3(x, y, 0) * speed * Runner.DeltaTime;

                // 계산된 위치가 원 안에 있는지 확인 후 안에 있으면 이동, 밖에 있으면 경계로 클램프
                if (IsInUnion(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius))
                {
                    this.transform.position = newPosition;
                }
                else
                {
                    Vector3 clampedPosition = ClampToBoundary(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius);
                    this.transform.position = clampedPosition;
                }
            }
        }

        if (isFootsGrounded == false) //둘 다 접지되지 않았으면
        {
            this.GetComponent<Rigidbody2D>().gravityScale = 1;
        }
    }

    bool IsInUnion(Vector3 point, Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
    {
        bool inCircleA = (point.x - centerA.x) * (point.x - centerA.x) + (point.y - centerA.y) * (point.y - centerA.y) <= radiusA * radiusA;
        bool inCircleB = (point.x - centerB.x) * (point.x - centerB.x) + (point.y - centerB.y) * (point.y - centerB.y) <= radiusB * radiusB;

        return inCircleA && inCircleB; // 둘 중 하나에 포함되는지 확인
    }
    Vector3 ClampToBoundary(Vector3 cPosition, Vector3 centerA, float radiusA, Vector3 centerB, float radiusB)
    {
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
}
