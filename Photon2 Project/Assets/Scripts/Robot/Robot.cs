using UnityEngine;

[DisallowMultipleComponent]
public class Robot : MonoBehaviour
{
    [Header("팔/다리 프리팹")]
    [SerializeField] private GameObject armPrefab;
    [SerializeField] private GameObject legPrefab;

    private Rigidbody2D rigidbody;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        rigidbody = GetComponent<Rigidbody2D>();

        AttachLimb(armPrefab, "ShoulderLeft");
        AttachLimb(armPrefab, "ShoulderRight");
        armPrefab.transform.localScale = new Vector3(-1, 1, 1); // 좌우 반전

        AttachLimb(legPrefab, "PelvisLeft");
        AttachLimb(legPrefab, "PelvisRight");
    }

    private void AttachLimb(GameObject limbPrefab, string attachmentPointName)
    {
        if (limbPrefab == null)
        {
            Debug.LogError($"{attachmentPointName}에 연결할 팔다리 프리팹이 없습니다.", this);
            return;
        }

        Transform attachmentPoint = transform.Find(attachmentPointName);

        if (attachmentPoint == null)
        {
            Debug.LogError($"Robot 아래에서 {attachmentPointName} 연결 위치를 찾지 못했습니다.", this);
            return;
        }

        HingeJoint2D joint = limbPrefab.GetComponentInChildren<HingeJoint2D>();
        joint.connectedBody = rigidbody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = attachmentPoint.position;

        GameObject limb = Instantiate(limbPrefab, attachmentPoint, false);
    }
}
