using UnityEditor;
using UnityEngine;

[DisallowMultipleComponent]
public class Leg : Limb
{
    protected override void Init()
    {
        _type = LimbType.Leg;
        base.Init();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying == false
            || _endEffector == null
            || _followsMouse == false)
            return;

        Vector2 hipPosition = transform.position;
        Vector2 currentMidJointPosition = _lower.position;
        Vector2 currentFootPosition = _endEffector.position;
        float pointRadius = Mathf.Max(0.01f, 0.12f);

        // 두 원이 만나는 위치가 무릎이 들어갈 수 있는 두 후보입니다.
        Handles.color = new Color(0.2f, 1f, 0.35f, 0.65f);
        Handles.DrawWireDisc(hipPosition, Vector3.forward, _upperLength);

        Handles.color = new Color(1f, 0.35f, 0.15f, 0.65f);
        Handles.DrawWireDisc(_targetEndEffectorPosition, Vector3.forward, _lowerLength);

        // 현재 실제 다리는 하늘색으로 표시합니다.
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.8f);
        Gizmos.DrawLine(hipPosition, currentMidJointPosition);
        Gizmos.DrawLine(currentMidJointPosition, currentFootPosition);
        Gizmos.DrawSphere(currentMidJointPosition, pointRadius * 0.7f);

        // 계산된 예상 다리는 노란색으로 표시합니다.
        Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
        Handles.DrawLine(hipPosition, _midJointPosition, 3f);
        Handles.DrawLine(_midJointPosition, _targetEndEffectorPosition, 3f);
        Handles.DrawSolidDisc(_midJointPosition, Vector3.forward, pointRadius);

        Handles.color = new Color(1f, 0.25f, 0.2f, 1f);
        Handles.DrawSolidDisc(_targetEndEffectorPosition, Vector3.forward, pointRadius);

        Handles.Label(hipPosition + Vector2.up * pointRadius, $"골반\n허벅지: {_upperLength:F2}");
        Handles.Label(_midJointPosition + Vector2.up * pointRadius, "예상 무릎");
        Handles.Label(currentMidJointPosition + Vector2.right * pointRadius, "현재 무릎");
        Handles.Label(_targetEndEffectorPosition + Vector2.up * pointRadius, $"발 목표\n종아리: {_lowerLength:F2}");
    }
#endif
}
