using UnityEngine;

[DisallowMultipleComponent]
public class Leg : MonoBehaviour
{
    [SerializeField] private Rigidbody2D lowerLeg;
    [SerializeField] private Transform foot;
    [SerializeField] private TargetJoint2D mouseTargetJoint;

    [Header("발이 마우스를 따라가는 힘")]
    [SerializeField, Min(0f)] private float maxForce = 1000f;
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.7f;  // 흔들림 억제
    [SerializeField, Min(0f)] private float frequency = 5f;             // 목표를 따라가는 강도
    [SerializeField, Min(0f)] private float reachMargin = 0.1f;         // 다리가 완전히 펴지지 않도록 남겨둘 거리

    // 마우스
    private Camera worldCamera;
    private bool followsMouse = false;
    private float maxLegReach;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (lowerLeg == null)
            lowerLeg = transform.Find("Lower")?.GetComponent<Rigidbody2D>();
        
        if (foot == null)
            foot = lowerLeg?.transform.Find("Foot");

        if (lowerLeg == null || foot == null)
        {
            Debug.LogError("다리에서 종아리 또는 발 참조를 찾지 못했습니다.", this);
            return;
        }

        // 골반부터 발까지의 거리를 다리가 닿을 수 있는 최대 길이로 사용합니다.
        maxLegReach = Vector2.Distance(transform.position, foot.position);

        if (mouseTargetJoint == null)
        {
            mouseTargetJoint = lowerLeg.GetComponent<TargetJoint2D>();
            if (mouseTargetJoint == null)
                mouseTargetJoint = lowerLeg.gameObject.AddComponent<TargetJoint2D>();
        
            mouseTargetJoint.enabled = followsMouse;
        }

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    public void SetMouseControl()
    {
        if (TryPrepareMouseTargetJoint() == false)
        {
            followsMouse = false;
            return;
        }

        followsMouse = true;
        mouseTargetJoint.enabled = true;
        UpdateMouseTarget();
    }

    private bool TryPrepareMouseTargetJoint()
    {
        if (lowerLeg == null || foot == null || mouseTargetJoint == null)
        {
            Debug.LogError("다리에서 종아리/발/마우스 타겟 조인트 참조를 찾지 못했습니다.", this);
            return false;
        }

        // 힘이 아랫다리의 중심이 아니라 발에 작용하도록 설정합니다.
        // 무릎(기준)으로부터 발이 얼마나 떨어진 곳에 있는가 => 무릎을 잡고 발을 끌어당기는 느낌
        mouseTargetJoint.anchor = lowerLeg.transform.InverseTransformPoint(foot.position);
        mouseTargetJoint.autoConfigureTarget = false;
        mouseTargetJoint.maxForce = maxForce;
        mouseTargetJoint.dampingRatio = dampingRatio;
        mouseTargetJoint.frequency = frequency;
        mouseTargetJoint.target = foot.position;    // 초기화

        return true;
    }

    private void Update()
    {
        if (followsMouse)
        {
            UpdateMouseTarget();
        }
    }

    /// <summary>
    /// 마우스 화면 좌표 -> 게임 월드 좌표.
    /// 마우스를 따라 발 이동 위치를 업데이트합니다.
    /// </summary>
    private void UpdateMouseTarget()
    {
        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(worldCamera.transform.position.z - lowerLeg.transform.position.z);

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        Vector2 targetPosition = new Vector2(worldPosition.x, worldPosition.y);
        mouseTargetJoint.target = ClampTargetToReach(targetPosition);
    }

    /// <summary>
    /// 다리가 실제로 닿을 수 있는 범위 안으로 제한합니다.
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <returns>제한된 목표 위치</returns>
    private Vector2 ClampTargetToReach(Vector2 targetPosition)
    {
        Vector2 pelvisPosition = transform.position;
        Vector2 pelvisToTarget = targetPosition - pelvisPosition;

        // 다리가 완전히 펴지기 전에 멈춰 몸통을 계속 잡아당기는 힘을 줄입니다.
        float allowedReach = Mathf.Max(0f, maxLegReach - reachMargin);
        return pelvisPosition + Vector2.ClampMagnitude(pelvisToTarget, allowedReach);
    }
}
