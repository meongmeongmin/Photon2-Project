using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Leg : Limb
{
    protected override void Init()
    {
        _type = LimbType.Leg;
        base.Init();

        if (_lower == null || _endEffector == null)
        {
            Debug.LogError("다리에서 종아리 또는 발 참조를 찾지 못했습니다.", this);
            return;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_endEffector == null || _followsMouse == false)
            return;

        // 실행 전에는 현재 프리팹 위치로 계산하고, 실행 중에는 처음 계산한 최대 길이를 사용합니다.
        float visibleReach = Application.isPlaying && _maxReach > 0f
            ? _maxReach
            : Vector2.Distance(transform.position, _endEffector.position);

        Handles.color = new Color(0f, 1f, 0.35f, 0.08f);
        Handles.DrawSolidDisc(transform.position, Vector3.forward, visibleReach);

        Handles.color = new Color(0f, 1f, 0.35f, 0.8f);
        Handles.DrawWireDisc(transform.position, Vector3.forward, visibleReach);
    }
#endif
}
