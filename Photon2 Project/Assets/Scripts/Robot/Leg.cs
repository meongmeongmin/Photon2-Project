using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Leg : Limb
{
    [Header("IK 계산 결과 확인")]
    [SerializeField] private bool _showIkPreview = true;
    [SerializeField, Min(0.01f)] private float _previewPointRadius = 0.12f;

    [Header("낮은 힘으로 관절 모터 시험")]
    [SerializeField, Min(0f)] private float _testMaxMotorTorque = 50f;
    [SerializeField, Min(0f)] private float _testAngleSpeed = 3f;
    [SerializeField, Min(0f)] private float _testMaxMotorSpeed = 180f;
    [SerializeField, Range(0f, 1f)] private float _testMotorDamping = 0.5f;
    [SerializeField, Min(0f)] private float _testStopAngle = 0.75f;
    [SerializeField, Range(-1, 1)] private int _motorSpeedDirection = -1;

    private bool _motorTestActive;

    private bool _hasExpectedKneePosition;
    private Vector2 _otherKneePosition;
    private int _selectedKneeDirection;

    private float _jointAngleToVisibleAngleSign = 1f;
    private float _initialHipJointAngle;
    private float _initialKneeJointAngle;
    private float _initialThighAngleFromBody;
    private float _initialKneeBendAngle;

    protected override void Init()
    {
        _type = LimbType.Leg;
        base.Init();

        SelectInitialKneeDirection(_endEffector.position);
        CalculateExpectedKneePosition(_endEffector.position);
    }

    public override void SetMouseControl(Cursor cursor)
    {
        base.SetMouseControl(cursor);

        if (_followsMouse)
            StartJointMotorTest();
    }

    protected override void OnMouseTargetUpdated(Vector2 targetPosition)
    {
        CalculateExpectedKneePosition(targetPosition);
    }

    protected override void OnLimbFixedUpdate()
    {
        if (_followsMouse == false || _hasExpectedKneePosition == false)
            return;

        if (_motorTestActive == false)
            StartJointMotorTest();
        else if (_motorTestActive)
            StopJointMotorTest();

        CalculateTargetJointAngles();
        DriveJointMotor(_upperJoint, _targetUpperJointAngle);
        DriveJointMotor(_lowerJoint, _targetLowerJointAngle);
    }

    private void StartJointMotorTest()
    {
        // 두 제어 방식이 동시에 힘을 쓰지 않도록 기존 TargetJoint2D를 잠시 끕니다.
        _mouseTargetJoint.enabled = false;

        Vector2 thighDirection = _lower.position - (Vector2)transform.position;
        Vector2 calfDirection = (Vector2)_endEffector.position - _lower.position;
        float connectedBodyRotation = _upperJoint.connectedBody.rotation;

        _initialHipJointAngle = _upperJoint.jointAngle;
        _initialKneeJointAngle = _lowerJoint.jointAngle;
        _initialThighAngleFromBody = GetDirectionAngle(thighDirection) - connectedBodyRotation;
        _initialKneeBendAngle = Vector2.SignedAngle(thighDirection, calfDirection);

        Vector3 scale = transform.lossyScale;
        _jointAngleToVisibleAngleSign = scale.x * scale.y < 0f ? -1f : 1f;

        SetJointMotorEnabled(_upperJoint, true);
        SetJointMotorEnabled(_lowerJoint, true);
        _motorTestActive = true;
    }

    private void StopJointMotorTest()
    {
        SetJointMotorEnabled(_upperJoint, false);
        SetJointMotorEnabled(_lowerJoint, false);

        if (_mouseTargetJoint != null && _followsMouse)
        {
            _mouseTargetJoint.target = _endEffector.position;
            _mouseTargetJoint.enabled = true;
        }

        _motorTestActive = false;
    }

    /// <summary>
    /// 목표 위치를 향하도록 윗팔/허벅지 - 아랫팔/종아리 관절 각도를 계산합니다.
    /// </summary>
    private void CalculateTargetJointAngles()
    {
        Vector2 hipPosition = transform.position;
        Vector2 expectedThighDirection = _expectedLowerPosition - hipPosition;
        Vector2 expectedCalfDirection = _reachableTargetPosition - _expectedLowerPosition;
        float connectedBodyRotation = _upperJoint.connectedBody.rotation;

        float expectedThighAngleFromBody = GetDirectionAngle(expectedThighDirection) - connectedBodyRotation;
        float thighAngleChange = Mathf.DeltaAngle(_initialThighAngleFromBody, expectedThighAngleFromBody);
        _targetUpperJointAngle = _initialHipJointAngle + (thighAngleChange / _jointAngleToVisibleAngleSign);

        float expectedKneeBendAngle = Vector2.SignedAngle(expectedThighDirection, expectedCalfDirection);
        float kneeBendChange = Mathf.DeltaAngle(_initialKneeBendAngle, expectedKneeBendAngle);
        _targetLowerJointAngle = _initialKneeJointAngle + (kneeBendChange / _jointAngleToVisibleAngleSign);

        _targetUpperJointAngle = ClampToJointLimits(_upperJoint, _targetUpperJointAngle);
        _targetLowerJointAngle = ClampToJointLimits(_lowerJoint, _targetLowerJointAngle);
    }

    /// <summary>
    /// 관절 모터를 목표 각도를 향하도록 구동합니다.
    /// </summary>
    /// <param name="joint">구동할 관절</param>
    /// <param name="targetAngle">목표 각도</param>
    private void DriveJointMotor(HingeJoint2D joint, float targetAngle)
    {
        float angleError = Mathf.DeltaAngle(joint.jointAngle, targetAngle);
        float desiredJointSpeed = 0f;
        if (Mathf.Abs(angleError) > _testStopAngle)
        {
            desiredJointSpeed = (angleError * _testAngleSpeed) - (joint.jointSpeed * _testMotorDamping);
        }

        desiredJointSpeed = Mathf.Clamp(desiredJointSpeed, -_testMaxMotorSpeed, _testMaxMotorSpeed);

        int motorDirection = _motorSpeedDirection >= 0 ? 1 : -1;
        JointMotor2D motor = joint.motor;
        motor.motorSpeed = desiredJointSpeed * motorDirection;
        motor.maxMotorTorque = _testMaxMotorTorque;
        joint.motor = motor;
        joint.useMotor = true;
    }

    private void SelectInitialKneeDirection(Vector2 footTargetPosition)
    {
        Vector2 hipPosition = transform.position;
        Vector2 hipToFoot = footTargetPosition - hipPosition;
        Vector2 hipToKnee = _lower.position - hipPosition;

        // 골반에서 발을 바라봤을 때 현재 무릎이 어느 쪽에 있는지 확인합니다.
        float currentKneeSide = hipToFoot.x * hipToKnee.y - hipToFoot.y * hipToKnee.x;
        if (Mathf.Abs(currentKneeSide) > 0.001f)
        {
            _selectedKneeDirection = currentKneeSide > 0f ? 1 : -1;
            return;
        }

        // 다리가 일직선이라 방향을 판단할 수 없으면 Inspector의 기본 방향을 사용합니다.
        _selectedKneeDirection = _preferredLowerDirection >= 0 ? 1 : -1;
    }

    /// <summary>
    /// 팔꿈치/무릎 예상 위치를 계산합니다.
    /// </summary>
    /// <param name="targetPosition"></param>
    private void CalculateExpectedKneePosition(Vector2 targetPosition)
    {
        // 값이 너무 작으면 계산이 불안정해지므로 팔꿈치/무릎 위치를 계산하지 않습니다.
        if (_upperLength <= Mathf.Epsilon || _lowerLength <= Mathf.Epsilon)
        {
            _hasExpectedKneePosition = false;
            return;
        }

        Vector2 hipPosition = transform.position;
        Vector2 hipToTarget = targetPosition - hipPosition;
        float targetDistance = hipToTarget.magnitude;

        Vector2 targetDirection;
        if (targetDistance > Mathf.Epsilon)
        {
            targetDirection = hipToTarget / targetDistance; // normalized
        }
        else
        {
            // 목표 위치와 어깨/골반 위치가 거의 같은 위치여도, 방향을 반드시 하나 만들어주기 위해
            // 현재 팔/다리가 향하고 있는 방향을 사용하자
            Vector2 currentFootDirection = (Vector2)_endEffector.position - hipPosition;
            targetDirection = currentFootDirection.sqrMagnitude > Mathf.Epsilon ? currentFootDirection.normalized : Vector2.down;
        }

        // 두 다리 길이로 실제 계산 가능한 거리 안에 발 목표를 둡니다.
        float minDistance = Mathf.Abs(_upperLength - _lowerLength) + 0.001f;

        // 두 원이 한 점에서 맞닿으면 무릎 방향이 불안정해지므로 완전히 펴지기 전까지만 허용합니다.
        float maxDistance = (_upperLength + _lowerLength) * Mathf.Clamp(_maxReachRatio, 0.8f, 0.999f);
        float reachableDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        _reachableTargetPosition = hipPosition + (targetDirection * reachableDistance);

        // 골반에서 발 방향으로 얼마나 이동한 곳에서 무릎이 갈라지는지 계산합니다.

        //        Knee
        //        ●
        //       /|\
        //      / | \
        //   a /  |  \ b
        //    /   |   \
        //Hip ●---●----● Foot
        //       x
        float alongTarget = ((_upperLength * _upperLength) - (_lowerLength * _lowerLength) + (reachableDistance * reachableDistance))
            / (2f * reachableDistance); // 어깨/골반 ~ 손/발 무릎 후보의 가운데 지점

        //        Knee
        //         ●
        //        /|
        //       / |
        //upper /  | sidewaysDistance
        //     /   |
        //Hip ●----● kneeCenter
        //   alongTarget
        float sidewaysDistanceSquared = _upperLength * _upperLength - alongTarget * alongTarget;
        float sidewaysDistance = Mathf.Sqrt(Mathf.Max(0f, sidewaysDistanceSquared));
        Vector2 kneeCenter = hipPosition + (targetDirection * alongTarget);
        Vector2 sidewaysDirection = new Vector2(-targetDirection.y, targetDirection.x); // 90도 회전

        // 무릎 예상 위치 후보 두 개를 계산합니다. (좌우 대칭)
        Vector2 firstKneePosition = kneeCenter + (sidewaysDirection * sidewaysDistance);
        Vector2 secondKneePosition = kneeCenter - (sidewaysDirection * sidewaysDistance);

        // 시작할 때 선택한 굽힘 방향을 계속 사용해 일직선 근처에서도 반대편으로 뒤집히지 않게 합니다.
        bool useFirstPosition = _selectedKneeDirection >= 0;
        _expectedLowerPosition = useFirstPosition ? firstKneePosition : secondKneePosition;
        _otherKneePosition = useFirstPosition ? secondKneePosition : firstKneePosition;

        _hasExpectedKneePosition = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_showIkPreview == false
            || Application.isPlaying == false
            || _endEffector == null
            || _followsMouse == false
            || _hasExpectedKneePosition == false)
            return;

        Vector2 hipPosition = transform.position;
        Vector2 currentKneePosition = _lower.position;
        Vector2 currentFootPosition = _endEffector.position;
        float pointRadius = Mathf.Max(0.01f, _previewPointRadius);

        // 두 원이 만나는 위치가 무릎이 들어갈 수 있는 두 후보입니다.
        Handles.color = new Color(0.2f, 1f, 0.35f, 0.65f);
        Handles.DrawWireDisc(hipPosition, Vector3.forward, _upperLength);

        Handles.color = new Color(1f, 0.35f, 0.15f, 0.65f);
        Handles.DrawWireDisc(_reachableTargetPosition, Vector3.forward, _lowerLength);

        // 현재 실제 다리는 하늘색으로 표시합니다.
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.8f);
        Gizmos.DrawLine(hipPosition, currentKneePosition);
        Gizmos.DrawLine(currentKneePosition, currentFootPosition);
        Gizmos.DrawSphere(currentKneePosition, pointRadius * 0.7f);

        // 계산된 예상 다리는 노란색으로 표시합니다.
        Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
        Handles.DrawLine(hipPosition, _expectedLowerPosition, 3f);
        Handles.DrawLine(_expectedLowerPosition, _reachableTargetPosition, 3f);
        Handles.DrawSolidDisc(_expectedLowerPosition, Vector3.forward, pointRadius);

        // 선택하지 않은 반대쪽 무릎 후보는 작은 회색 점으로 표시합니다.
        Handles.color = new Color(0.65f, 0.65f, 0.65f, 0.8f);
        Handles.DrawSolidDisc(_otherKneePosition, Vector3.forward, pointRadius * 0.65f);

        Handles.color = new Color(1f, 0.25f, 0.2f, 1f);
        Handles.DrawSolidDisc(_reachableTargetPosition, Vector3.forward, pointRadius);

        Handles.Label(hipPosition + Vector2.up * pointRadius, $"골반\n허벅지: {_upperLength:F2}");
        Handles.Label(_expectedLowerPosition + Vector2.up * pointRadius, "예상 무릎");
        Handles.Label(currentKneePosition + Vector2.right * pointRadius, "현재 무릎");
        Handles.Label(_reachableTargetPosition + Vector2.up * pointRadius, $"발 목표\n종아리: {_lowerLength:F2}");
    }
#endif
}
