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

    Vector3 spawnPosition;
    Rigidbody2D body;

    public override void Spawned()
    {
        spawnPosition = transform.position; //테스트용 R키 리셋을 위해 스폰 위치를 기억해둔다
        body = GetComponent<Rigidbody2D>();

        if (Object.HasStateAuthority)
        {
            // Unity 2D physics is authoritative only on the host.
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 1f;
        }

        // Do not change proxy Rigidbody settings here. NetworkTransform's forecast
        // physics initializes and corrects the proxy body itself.
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return; //공유 몸통 물리는 호스트만 시뮬레이션한다
        if (body == null || LeftLeg == null || RightLeg == null) return;
        //(이 가드가 꺼져 있으면 클라이언트에서 LeftLeg/RightLeg가 null이라 매 틱 NullReferenceException이 발생해
        // Robot의 NetworkTransform 복제까지 함께 깨진다 - 클라이언트에는 UIManager의 host-only 배선 코드가 실행되지 않기 때문)

        //테스트용: R키를 누르면 로봇을 처음 스폰 위치로 되돌린다
        if (Input.GetKeyDown(KeyCode.R))
        {
            body.position = spawnPosition;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        leftFootGrounded = LeftLeg.isGround;
        rightFootGrounded = RightLeg.isGround;

        isFootsGrounded = (leftFootGrounded || rightFootGrounded);
        if(isFootsGrounded == true) //둘 중 하나라도 접지된 상태
        {
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            Vector2 newPosition = body.position; // C의 다음 위치를 계산

            // 키 입력에 따라 C가 이동할 방향을 결정
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");

            if (x !=0 || y != 0)
            {
                newPosition += new Vector2(x, y) * speed * Runner.DeltaTime;

                // 계산된 위치가 원 안에 있는지 확인 후 안에 있으면 이동, 밖에 있으면 경계로 클램프
                if (IsInUnion(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius))
                {
                    body.MovePosition(newPosition);
                }
                else
                {
                    Vector3 clampedPosition = ClampToBoundary(newPosition, LeftLeg.foot.transform.position, radius, RightLeg.foot.transform.position, radius);
                    body.MovePosition(clampedPosition);
                }
            }
        }
        else //둘 다 접지되지 않았으면
        {
            // Unity handles gravity, collision response, and accumulated velocity.
            body.gravityScale = 1f;
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
