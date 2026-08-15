using UnityEngine;

public class Arm : Limb
{
    protected override void Init()
    {
        _type = LimbType.Arm;
        base.Init();
        if (_lower == null || _endEffector == null)
        {
            Debug.LogError("팔에서 아랫팔 또는 손 참조를 찾지 못했습니다.", this);
            return;
        }
    }
}
