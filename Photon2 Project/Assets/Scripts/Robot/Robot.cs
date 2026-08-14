using UnityEngine;

[DisallowMultipleComponent]
public class Robot : MonoBehaviour
{
    [Header("팔/다리 프리팹")]
    [SerializeField] private GameObject armPrefab;
    [SerializeField] private GameObject legPrefab;

    private Rigidbody2D bodyRigidbody;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        bodyRigidbody = GetComponent<Rigidbody2D>();

        GameObject leftLeg = AttachLimb(legPrefab, "PelvisLeft");
        SetMouseControl<Leg>(leftLeg);

        GameObject rightLeg = AttachLimb(legPrefab, "PelvisRight");
        //SetMouseControl<Leg>(rightLeg);

        GameObject leftArm = AttachLimb(armPrefab, "ShoulderLeft");
        leftArm.transform.localScale = new Vector3(-1, 1, 1);   // 좌우 반전
        GameObject rightArm = AttachLimb(armPrefab, "ShoulderRight");
        rightArm.transform.localScale = new Vector3(1, 1, 1);
    }

    private GameObject AttachLimb(GameObject limbPrefab, string attachmentPointName)
    {
        if (limbPrefab == null)
        {
            Debug.LogError($"{attachmentPointName}에 연결할 팔다리 프리팹이 없습니다.", this);
            return null;
        }

        Transform attachmentPoint = transform.Find(attachmentPointName);

        if (attachmentPoint == null)
        {
            Debug.LogError($"Robot 아래에서 {attachmentPointName} 연결 위치를 찾지 못했습니다.", this);
            return null;
        }

        HingeJoint2D joint = limbPrefab.GetComponentInChildren<HingeJoint2D>();
        joint.connectedBody = bodyRigidbody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = attachmentPoint.localPosition;

        return Instantiate(limbPrefab, attachmentPoint, false);
    }

    private void SetMouseControl<T>(GameObject limb) where T : Leg
    {
        if (limb == null)
            return;

        T limbComponent = limb.GetComponent<T>();
        if (limbComponent == null)
        {
            Debug.LogError($"{limb.name}에서 {typeof(T).Name} 스크립트를 찾지 못했습니다.", limb);
            return;
        }

        limbComponent.SetMouseControl();
    }
}
