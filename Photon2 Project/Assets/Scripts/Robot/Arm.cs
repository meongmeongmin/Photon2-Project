using UnityEngine;

public class Arm : Limb
{
    protected override void Init()
    {
        _type = LimbType.Arm;
        base.Init();
    }
}
