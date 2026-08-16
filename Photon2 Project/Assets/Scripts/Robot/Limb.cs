using UnityEngine;

public enum LimbType
{
    Arm,
    Leg
}

public abstract class Limb : MonoBehaviour
{
    protected LimbType _type;

    protected Rigidbody2D _lower;
    /// <summary>
    /// 손/발의 중심점
    /// </summary>
    protected Transform _endEffector;
    protected Collider2D _endEffectorCollider;
    protected TargetJoint2D _mouseTargetJoint;

    protected Robot _body;
    /// <summary>
    /// 어깨/골반 ~ 손/발 거리를 팔/다리가 닿을 수 있는 최대 길이
    /// </summary>
    protected float _maxReach;

    protected bool _followsMouse = false;
    protected Cursor _cursor;

    [Header("손/발이 마우스를 따라가는 힘")]
    [SerializeField, Min(0f)] protected float _maxForce = 80f;
    [SerializeField, Range(0f, 1f)] protected float _dampingRatio = 1f;
    [SerializeField, Min(0f)] protected float _frequency = 15f;

    [Header("마우스 힘으로 몸 전체가 끌리는 현상 방지")]
    [SerializeField, Range(0f, 1f)] private float _bodyPullPreventionStrength = 1f;

    /// <summary>
    /// 몸통을 당기는 힘
    /// </summary>
    public float MaxForce
    {
        get => _maxForce;
        set
        {
            _maxForce = Mathf.Max(0f, value);
            _mouseTargetJoint.maxForce = _maxForce;
        }
    }
    /// <summary>
    /// 흔들림 억제
    /// </summary>
    public float DampingRatio
    {
        get => _dampingRatio;
        set
        {
            _dampingRatio = Mathf.Clamp01(value);
            _mouseTargetJoint.dampingRatio = _dampingRatio;
        }
    }
    /// <summary>
    /// 목표를 따라가는 강도
    /// </summary>
    public float Frequency
    {
        get => _frequency;
        set
        {
            _frequency = Mathf.Max(0f, value);
            _mouseTargetJoint.frequency = _frequency;
        }
    }

    private void Awake()
    {
        Init();
    }

    protected virtual void Init()
    {
        _lower = transform.Find("Lower")?.GetComponent<Rigidbody2D>();
        _endEffector = _type == LimbType.Leg ? _lower?.transform.Find("Foot") : _lower?.transform.Find("Hand");
        _endEffectorCollider = _endEffector.GetComponent<Collider2D>();

        _maxReach = Vector2.Distance(transform.position, _endEffector.position);

        _mouseTargetJoint = _lower.GetComponent<TargetJoint2D>();
        if (_mouseTargetJoint == null)
            _mouseTargetJoint = _lower.gameObject.AddComponent<TargetJoint2D>();

        _mouseTargetJoint.autoConfigureTarget = false;
        _mouseTargetJoint.enabled = _followsMouse;  // test

        MaxForce = _maxForce;
        DampingRatio = _dampingRatio;
        Frequency = _frequency;
    }

    public virtual void SetInfo(Robot body)
    {
        _body = body;
    }

    public virtual void SetMouseControl(Cursor cursor)
    {
        if (TryPrepareMouseTargetJoint() == false)
        {
            _followsMouse = false;
            return;
        }

        _cursor = cursor;
        _cursor.SetInfo(_maxReach, transform, _endEffector);

        _followsMouse = true;
        _mouseTargetJoint.enabled = true;
        UpdateMouseTarget();
    }

    private void Update()
    {
        UpdateMouseTarget();
    }

    private void FixedUpdate()
    {
        if (_followsMouse == false || _mouseTargetJoint == null || _mouseTargetJoint.enabled == false || _endEffector == null)
            return;

        CancelMouseForceOnWholeRobot();
    }

    private void CancelMouseForceOnWholeRobot()
    {
        if (_body.Rigidbody == null || _bodyPullPreventionStrength <= 0f)
            return;

        // 직전 물리 계산에서 마우스 조인트가 팔다리에 준 힘입니다.
        Vector2 mouseForce = _mouseTargetJoint.GetReactionForce(Time.fixedDeltaTime);

        // 발을 당긴 힘과 같은 크기의 반대 힘을 골반에 줍니다.
        // 두 힘의 합이 0이므로 마우스만으로 로봇 전체가 미끄러지지 않습니다.
        // 발이 바닥을 실제로 밀어서 생기는 힘은 취소하지 않으므로 걷기 동작에는 사용할 수 있습니다.
        Vector2 oppositeForce = -mouseForce * _bodyPullPreventionStrength;
        _body.Rigidbody.AddForceAtPosition(oppositeForce, transform.position, ForceMode2D.Force);
    }

    protected bool TryPrepareMouseTargetJoint()
    {
        if (_lower == null || _endEffector == null || _mouseTargetJoint == null)
        {
            if (_type == LimbType.Leg)
                Debug.LogError("다리에서 종아리/발/마우스 타겟 조인트 참조를 찾지 못했습니다.", this);
            else
                Debug.LogError("팔에서 아랫팔/손/마우스 타겟 조인트 참조를 찾지 못했습니다.", this);
            return false;
        }

        // 힘이 아랫 팔/다리의 중심이 아니라 손/발에 작용하도록 설정합니다.
        // 팔꿈치/무릎(기준)으로부터 손/발이 얼마나 떨어진 곳에 있는가 => 팔꿈치/무릎을 잡고 손/발을 끌어당기는 느낌
        _mouseTargetJoint.anchor = _lower.transform.InverseTransformPoint(_endEffector.position);
        _mouseTargetJoint.autoConfigureTarget = false;
        _mouseTargetJoint.maxForce = MaxForce;
        _mouseTargetJoint.dampingRatio = DampingRatio;
        _mouseTargetJoint.frequency = Frequency;
        _mouseTargetJoint.target = _endEffector.position;    // 초기화

        return true;
    }

    /// <summary>
    /// 마우스를 따라 손/발 이동 위치를 업데이트합니다.
    /// </summary>
    protected void UpdateMouseTarget()
    {
        if (_followsMouse == false || _cursor.CursorInWindow == false)
            return;

        Vector2 targetPosition = _cursor.UpdatePosition();
        _mouseTargetJoint.target = ClampTargetToReach(targetPosition);
    }

    /// <summary>
    /// 팔/다리가 실제로 닿을 수 있는 범위 안으로 제한합니다.
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <returns>제한된 목표 위치</returns>
    protected Vector2 ClampTargetToReach(Vector2 targetPosition)
    {
        Vector2 limbRootPosition = transform.position;
        Vector2 limbRootToTarget = targetPosition - limbRootPosition;

        float targetDistance = limbRootToTarget.magnitude;
        Vector2 targetDirection = limbRootToTarget.normalized;

        // 마우스가 팔/다리 범위 안에 있으면 그대로 따라감
        if (targetDistance <= _maxReach)
        {
            return targetPosition;
        }

        Vector2 endEffectorPos = _endEffector.transform.position;
        float currentReach = Vector2.Distance(limbRootPosition, endEffectorPos);    // 어깨/골반 ~ 손/발 현재 길이
        float extensionThreshold = _maxReach * 0.99f;   // 거의 완전히 펴졌다고 판단할 아주 작은 여유

        // 아직 팔/다리가 덜 펴졌다면 최대 길이까지 빠르게 뻗도록 함
        float allowedReach = _maxReach * 0.97f;
        if (currentReach < extensionThreshold)
        {
            return limbRootPosition + targetDirection * allowedReach;
        }

        // 이미 거의 완전히 펴졌다면 현재 길이를 유지하면서 마우스 방향만 따라감.
        // 즉 바깥쪽으로 더 당기는 힘은 만들지 않지만 마우스가 좌우로 움직이면 손/발은 계속 따라갈 수 있음.
        float radius = Mathf.Min(currentReach, allowedReach);
        return limbRootPosition + targetDirection * radius;
    }
}
