using UnityEngine;

public enum LimbType
{
    Arm,
    Leg
}

public abstract class Limb : MonoBehaviour
{
    protected LimbType _type;

    protected Robot _body;

    #region 어깨/골반 & 팔꿈치/무릎
    protected Rigidbody2D _upper;
    protected Rigidbody2D _lower;

    // 관절
    /// <summary>
    /// 어깨/골반 관절
    /// </summary>
    protected HingeJoint2D _rootJoint;
    /// <summary>
    /// 팔꿈치/무릎 관절
    /// </summary>
    protected HingeJoint2D _midJoint;

    // 길이
    protected float _upperLength;
    protected float _lowerLength;

    // 초기 각도
    protected float _initialRootJointAngle;
    protected float _initialMidJointAngle;
    // 목표 각도
    protected float _targetRootJointAngle;
    protected float _targetMidJointAngle;

    // 목표 위치
    protected Vector2 _midJointPosition;
    /// <summary>
    /// 팔꿈치/무릎 방향 (좌: -1, 우: 1)
    /// </summary>
    protected int _midJointDirection;

    /// <summary>
    /// 몸통에 비해서 허벅지가 몇 도 꺾여 있는가
    /// </summary>
    protected float _initialRootJointAngleFromBody;
    /// <summary>
    /// 윗팔/허벅지와 아랫팔/종아리 사이의 굽힘 각도
    /// </summary>
    protected float _initialMidJointBendAngle;
    #endregion

    #region 손/발
    /// <summary>
    /// 손/발의 중심점
    /// </summary>
    protected Transform _endEffector;
    protected Collider2D _endEffectorCollider;
    protected Vector2 _targetEndEffectorPosition;   // 목표 위치
    #endregion

    /// <summary>
    /// 어깨/골반 ~ 손/발 길이
    /// </summary>
    protected float _maxReach;

    /// <summary>
    /// 오브젝트를 뒤집으면 화면상 보이는 회전 방향과 Joint의 물리적인 각도 부호가 반대로 보일 수 있기 때문에,
    /// 좌우 반전된 팔/다리에서 화면 방향과 관절 각도 방향을 맞추는 값
    /// </summary>
    protected float _jointAngleToVisibleAngleSign = 1f;

    protected bool _followsMouse = false;
    protected Cursor _cursor;

    [Header("팔/다리를 완전히 펴지 않도록 남기는 여유")]
    [SerializeField, Range(0.8f, 0.999f)] protected float _maxReachRatio = 0.97f;

    [Header("관절 모터 조절")]
    [SerializeField, Min(0f)] protected float _rootJointMaxMotorTorque = 450f;  // 어깨/골반 관절이 낼 수 있는 최대 힘
    [SerializeField, Min(0f)] protected float _midJointMaxMotorTorque = 320f;   // 팔꿈치/무릎 관절이 낼 수 있는 최대 힘
    [SerializeField, Min(0f)] protected float _angleSpeed = 20f;                // 남은 각도에 따라 모터 속도를 얼마나 크게 만들지 결정 => 목표 각도에 가까워질수록 속도는 줄어든다
    [SerializeField, Min(0f)] protected float _maxMotorSpeed = 1080f;           // 관절이 회전할 수 있는 최대 목표 속도
    [SerializeField, Range(0f, 1f)] protected float _motorDamping = 1f;         // 현재 회전 속도를 이용해 지나치게 빠른 움직임과 떨림을 줄이는 값
    [SerializeField, Min(0f)] protected float _stopAngle = 0.35f;               // 남은 각도가 이 값보다 작으면 모터를 정지

    private void Awake()
    {
        Init();
    }

    protected virtual void Init()
    {
        _upper = transform.Find("Upper")?.GetComponent<Rigidbody2D>();
        _lower = transform.Find("Lower")?.GetComponent<Rigidbody2D>();

        _rootJoint = _upper.GetComponent<HingeJoint2D>();
        _midJoint = _lower.GetComponent<HingeJoint2D>();

        _endEffector = _type == LimbType.Leg ? _lower?.transform.Find("Foot") : _lower?.transform.Find("Hand");
        _endEffectorCollider = _endEffector.GetComponent<Collider2D>();

        _upperLength = Vector2.Distance(transform.position, _lower.position);
        _lowerLength = Vector2.Distance(_lower.position, _endEffector.position);
        _maxReach = _upperLength + _lowerLength;

        CalculateMidJointDirection(_endEffector.position);
    }

    /// <summary>
    /// 팔꿈치/무릎 굽힘 방향을 결정합니다.
    /// </summary>
    /// <param name="endEffectorPosition"></param>
    protected void CalculateMidJointDirection(Vector2 endEffectorPosition)
    {
        Vector2 rootJointPosition = transform.position;
        Vector2 rootToEnd = endEffectorPosition - rootJointPosition;
        Vector2 rootToMid = _lower.position - rootJointPosition;

        // 어깨/골반에서 손/발을 바라봤을 때 현재 팔꿈치/무릎이 어느 쪽에 있는지 확인합니다.
        //           + 방향
        //             ● 무릎
        //             |
        //골반 ●────────────> 발
        //             |
        //             ● 무릎
        //           - 방향
        float currentMidSide = (rootToEnd.x * rootToMid.y) - (rootToEnd.y * rootToMid.x);
        if (Mathf.Abs(currentMidSide) > 0.001f)
        {
            _midJointDirection = currentMidSide > 0f ? 1 : -1;
            return;
        }

        // 다리가 일직선이라(쫙 펴진 상태) 방향을 판단할 수 없으면 몸통이 바라보는 방향을 사용합니다.
        _midJointDirection = _body.Direction >= 0 ? 1 : -1;
    }

    public void SetInfo(Robot body, HingeJoint2D rootJoint)
    {
        _body = body;
        _rootJoint = rootJoint;
    }

    public virtual void SetMouseControl(Cursor cursor)
    {
        _cursor = cursor;
        _cursor.SetInfo(_maxReach, transform, _endEffector);

        _followsMouse = true;
        UpdateTarget();
        StartJointMotor();
    }

    /// <summary>
    /// 관절 모터 작동을 준비하고 활성화합니다.
    /// </summary>
    protected void StartJointMotor()
    {
        Vector2 rootJointDirection = _lower.position - (Vector2)transform.position;
        Vector2 midJointDirection = (Vector2)_endEffector.position - _lower.position;
        float bodyRotation = _rootJoint.connectedBody.rotation;

        // 초기 자세 설정
        {
            _initialRootJointAngle = _rootJoint.jointAngle;
            _initialMidJointAngle = _midJoint.jointAngle;
            // 몸통에 비해서 허벅지가 몇 도 꺾여 있는가
            _initialRootJointAngleFromBody = GetDirectionAngle(rootJointDirection) - bodyRotation;
            // 팔꿈치/무릎 관절이 현재 어느 방향으로 얼마나 꺾여 있는지
            _initialMidJointBendAngle = Vector2.SignedAngle(rootJointDirection, midJointDirection);
        }

        // 오브젝트를 뒤집으면 화면상 보이는 회전 방향과 Joint의 물리적인 각도 부호가 반대로 보일 수 있기 때문
        Vector3 scale = transform.lossyScale;
        _jointAngleToVisibleAngleSign = scale.x * scale.y < 0f ? -1f : 1f;

        // 활성화
        SetJointMotorEnabled(_rootJoint, true);
        SetJointMotorEnabled(_midJoint, true);
    }

    private void Update()
    {
        UpdateTarget();
    }

    private void FixedUpdate()
    {
        if (_followsMouse == false)
            return;

        CalculateTargetJointAngles();
        DriveJointMotor(_rootJoint, _targetRootJointAngle, _rootJointMaxMotorTorque);
        DriveJointMotor(_midJoint, _targetMidJointAngle, _midJointMaxMotorTorque);
    }

    /// <summary>
    /// 마우스를 따라 손/발 및 팔꿈치/무릎 위치를 업데이트합니다.
    /// </summary>
    protected void UpdateTarget()
    {
        if (_followsMouse == false || _cursor.CursorInWindow == false)
            return;

        // 목표 손/발 위치 업데이트
        Vector2 targetPosition = _cursor.UpdatePosition();
        var (limitedTargetPosition, targetDirection, targetDistance) = ClampTarget(targetPosition);
        _targetEndEffectorPosition = limitedTargetPosition;

        // 목표 팔/다리 위치 업데이트
        CalculateMidJointPosition(limitedTargetPosition, targetDirection, targetDistance);
    }

    /// <summary>
    /// 팔/다리가 실제로 닿을 수 있는 범위 안으로 제한합니다.
    /// </summary>
    /// <param name="targetPosition">목표 위치</param>
    /// <returns>제한된 목표 위치</returns>
    protected (Vector2 targetPosition, Vector2 targetDirection, float targetDistance) ClampTarget(Vector2 targetPosition)
    {
        Vector2 rootJointPosition = transform.position;  // 어깨/골반 위치
        Vector2 rootToTarget = targetPosition - rootJointPosition;

        float targetDistance = rootToTarget.magnitude;
        Vector2 targetDirection;
        if (targetDistance > Mathf.Epsilon)
            targetDirection = rootToTarget / targetDistance;    // normalized
        else
        {
            // 목표 위치와 어깨/골반 위치가 거의 같은 위치여도, 방향을 반드시 하나 만들어주기 위해
            // 현재 팔/다리가 향하고 있는 방향을 사용하자
            Vector2 currentFootDirection = (Vector2)_endEffector.position - rootJointPosition;
            targetDirection = currentFootDirection.sqrMagnitude > Mathf.Epsilon ? currentFootDirection.normalized : Vector2.down;
        }

        // 두 팔/다리 길이로 실제 계산 가능한 거리 안에 손/발 목표를 둡니다.
        float minDistance = Mathf.Abs(_upperLength - _lowerLength) + 0.001f;
        // 두 원이 한 점에서 맞닿으면 팔꿈치/무릎 방향이 불안정해지므로 완전히 펴지기 전까지만 허용합니다.
        float maxDistance = _maxReach * Mathf.Clamp(_maxReachRatio, 0.8f, 0.999f);
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

        targetPosition = rootJointPosition + (targetDirection * targetDistance);
        return (targetPosition, targetDirection, targetDistance);
    }

    /// <summary>
    /// 팔꿈치/무릎 위치를 계산합니다.
    /// </summary>
    /// <param name="targetPosition">손/발 목표 위치</param>
    protected void CalculateMidJointPosition(Vector2 targetPosition, Vector2 targetDirection, float targetDistance)
    {
        // 어깨/골반에서 손/발 방향으로 얼마나 이동한 곳에서 무릎이 갈라지는지 계산합니다.
        //                   MidJoint
        //                    ●
        //                   /|\
        //                  / | \
        //           upper /  |  \ lower
        //                /   |   \
        //      rootJoint ●---●----● EndJoint
        //                alongTarget
        float alongTarget = ((_upperLength * _upperLength) - (_lowerLength * _lowerLength) + (targetDistance * targetDistance))
            / (2f * targetDistance); // 어깨/골반 ~ 손/발 무릎 후보의 가운데 지점 길이

        //                  MidJoint
        //                   ●
        //                  /|
        //                 / |
        //          upper /  | sidewaysDistance
        //               /   |
        //    rootJoint ●----● midJointCenter
        //             alongTarget
        float sidewaysDistanceSquared = (_upperLength * _upperLength) - (alongTarget * alongTarget);
        float sidewaysDistance = Mathf.Sqrt(Mathf.Max(0f, sidewaysDistanceSquared));
        Vector2 rootJointPosition = transform.position;  // 어깨/골반 위치
        Vector2 midJointCenter = rootJointPosition + (targetDirection * alongTarget);

        // 무릎 후보 두 점이 골반-발 선에 수직인 방향이므로 90도 회전
        Vector2 sidewaysDirection = new Vector2(-targetDirection.y, targetDirection.x);

        // 팔꿈치/무릎 예상 위치 후보 두 개를 계산합니다. (좌우 대칭)
        Vector2 firstMidJointPosition = midJointCenter + (sidewaysDirection * sidewaysDistance);
        Vector2 secondMidJointPosition = midJointCenter - (sidewaysDirection * sidewaysDistance);

        // 일직선 근처에서도 반대편으로 뒤집히지 않게 합니다.
        _midJointPosition = _midJointDirection >= 0 ? firstMidJointPosition : secondMidJointPosition;
    }

    /// <summary>
    /// 목표 위치를 향하도록 윗팔/허벅지 - 아랫팔/종아리 관절 각도를 계산합니다.
    /// </summary>
    protected void CalculateTargetJointAngles()
    {
        Vector2 rootJointPosition = transform.position;
        Vector2 rootJointDirection = _midJointPosition - rootJointPosition;
        Vector2 midJointDirection = _targetEndEffectorPosition - _midJointPosition;
        float bodyRotation = _rootJoint.connectedBody.rotation;

        float rootJointAngleFromBody = GetDirectionAngle(rootJointDirection) - bodyRotation;
        float rootJointAngleChange = Mathf.DeltaAngle(_initialRootJointAngleFromBody, rootJointAngleFromBody);
        _targetRootJointAngle = _initialRootJointAngle + (rootJointAngleChange / _jointAngleToVisibleAngleSign);

        float midJointBendAngle = Vector2.SignedAngle(rootJointDirection, midJointDirection);
        float midJointBendChange = Mathf.DeltaAngle(_initialMidJointBendAngle, midJointBendAngle);
        _targetMidJointAngle = _initialMidJointAngle + (midJointBendChange / _jointAngleToVisibleAngleSign);

        _targetRootJointAngle = ClampToJointLimits(_rootJoint, _targetRootJointAngle);
        _targetMidJointAngle = ClampToJointLimits(_midJoint, _targetMidJointAngle);
    }

    /// <summary>
    /// 관절 모터를 목표 각도를 향하도록 구동합니다.
    /// </summary>
    /// <param name="joint">구동할 관절</param>
    /// <param name="targetAngle">목표 각도</param>
    /// <param name="maxMotorTorque">이 관절이 사용할 수 있는 최대 힘</param>
    protected void DriveJointMotor(HingeJoint2D joint, float targetAngle, float maxMotorTorque)
    {
        float angleError = Mathf.DeltaAngle(joint.jointAngle, targetAngle);
        float desiredJointSpeed = 0f;

        // 목표 각도에서 충분히 멀 때만 모터 구동
        // (angleError * _angleSpeed) => 목표에서 멀수록 빠르게 움직이고,
        // - (joint.jointSpeed * _motorDamping) => 관절이 이미 매우 빠르게 움직이고 있다면 목표 속도를 낮춘다 (관성으로 인해 목표를 지나치는 것을 방지)
        if (Mathf.Abs(angleError) > _stopAngle)
            desiredJointSpeed = (angleError * _angleSpeed) - (joint.jointSpeed * _motorDamping);

        desiredJointSpeed = Mathf.Clamp(desiredJointSpeed, -_maxMotorSpeed, _maxMotorSpeed);

        // 마우스를 옮긴 즉시 새 목표 속도를 사용합니다.
        JointMotor2D motor = joint.motor;
        motor.motorSpeed = desiredJointSpeed;
        motor.maxMotorTorque = maxMotorTorque;
        joint.motor = motor;
        joint.useMotor = true;
    }

    #region Utilities
    protected void StopJointMotor()
    {
        SetJointMotorEnabled(_rootJoint, false);
        SetJointMotorEnabled(_midJoint, false);
    }

    protected static void SetJointMotorEnabled(HingeJoint2D joint, bool enabled)
    {
        if (joint == null)
            return;

        JointMotor2D motor = joint.motor;
        motor.motorSpeed = 0f;
        joint.motor = motor;
        joint.useMotor = enabled;
    }

    /// <summary>
    /// 계산된 목표 관절 각도가 HingeJoint2D의 허용 범위를 벗어나지 않도록 제한합니다.
    /// </summary>
    /// <param name="joint">관절</param>
    /// <param name="angle">목표 관절 각도</param>
    /// <returns></returns>
    protected static float ClampToJointLimits(HingeJoint2D joint, float angle)
    {
        if (joint.useLimits == false)
            return Mathf.DeltaAngle(0f, angle);

        JointAngleLimits2D limits = joint.limits;
        return Mathf.Clamp(angle, limits.min, limits.max);
    }

    /// <summary>
    /// 벡터를 각도로 변환합니다.
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    protected static float GetDirectionAngle(Vector2 direction)
    {
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
    #endregion
}