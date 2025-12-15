using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class TestCircle : MonoBehaviour
{

    public Transform A;
    public Transform B;
    public float radiusA;
    public float radiusB;
    public Transform C;

    void Update()
    {
        Vector3 newPosition = C.position; // C의 새로운 위치를 계산

        // 키 입력 등을 통해 C의 이동 방향을 결정
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        newPosition += new Vector3(x, y, 0) * 0.2f; 
        
        // 합집합 영역 내에 있는지 확인 이 안에 있을때만 이동 가능 하지만 부드럽게 움직이려면?
        if (IsInUnion(newPosition, A.position, radiusA, B.position, radiusB) && newPosition.y > A.position.y)
        {
            //float disA = Vector3.SqrMagnitude(newPosition - A.position);
            //float disB = Vector3.SqrMagnitude(newPosition - B.position);
            //disA = Mathf.Sqrt(disA);
            //disB = Mathf.Sqrt(disB);
            //움직임을 제어하는것이 아니라 제어 한 후에 위치를 넣어준다.
            //유효한 위치로 C를 이동
            Debug.Log("내부에서 이동");
            C.position = newPosition;
           
        }
        else
        {
            Debug.Log("경계선에서 이동");
            Vector3 clampedPosition = ClampToBoundary(newPosition, A.position, radiusA, B.position, radiusB);
            C.position = clampedPosition;
        }
       
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

        //if (disB >= radiusB - 0.01f)
        //{
        //    cPosition = centerB + dirB.normalized * radiusB;
        //}

        //if (disA >= radiusA - 0.01f)
        //{
        //    cPosition = centerA + dirA.normalized * radiusA;
        //}

        return cPosition;
    }
    void OnDrawGizmos()
    {
        // 원을 시각적으로 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(A.position, radiusA);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(B.position, radiusB);
    }
}
