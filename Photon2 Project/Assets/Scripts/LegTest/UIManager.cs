using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Unity.Burst.Intrinsics;
using Unity.VisualScripting;
using UnityEngine;

public class UIManager : MonoBehaviourPunCallbacks
{
    //public Camera mainCam;
    public GameObject[] canvases;
    public GameObject gameObject4;  // GameObject(4)를 참조

    public GameObject NetworkRobot;
    public Camera main_Cam;
    public bool isrobot;

    // 파츠 오브젝트들
    public GameObject leftArm;  // 왼쪽 팔
    public GameObject rightArm; // 오른쪽 팔
    public GameObject leftLeg;  // 왼쪽 다리
    public GameObject rightLeg; // 오른쪽 다리

    public GameObject mousePosLA;  // 왼쪽 팔
    public GameObject mousePosRA; // 오른쪽 팔
    public GameObject mousePosLL;  // 왼쪽 다리
    public GameObject mousePosRL; // 오른쪽 다리

    private GameObject[] allParts; // 4개의 파츠를 배열로 관리
    private GameObject[] allMousePos;
    private PhotonView PV; // PhotonView 변수 추가
    private Transform pelvis;
    bool isStarted;

    //로봇 몸을 모두에게 보여주는 코드는 어디있죠?
    void Start()
    {
        isStarted = false;
        Debug.Log(this.gameObject.name);
        PV = GetComponent<PhotonView>(); // PhotonView 할당
        //yield return new WaitUntil(() => PhotonNetwork.IsConnectedAndReady);



    }
    public void Update()
    {
        if (isStarted)
        {
            PhotonView pho;
            PhotonView pho2;
            for (int i = 0; i < allMousePos.Length; i++)
            {
                pho = allMousePos[i].GetPhotonView();
                if (pho.IsMine == true)
                {
                    Debug.Log(allMousePos[i].gameObject.name + "내거임");
                }
                else if (pho.IsMine != true)
                {
                    Debug.Log(allMousePos[i].gameObject.name + "내거아님");
                }

                pho2 = allMousePos[i].GetPhotonView();
                if (pho.IsMine == true)
                {
                    Debug.Log(allParts[i].gameObject.name + "내거임");
                }
                else if (pho.IsMine != true)
                {
                    Debug.Log(allParts[i].gameObject.name + "내거아님");
                }
            }
        }
    }
    public void ShowCanvas(int index)
    {
        if (canvases == null || canvases.Length == 0) return;
        if (index < 0 || index >= canvases.Length) return;

        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].SetActive(i == index);  // 현재 인덱스만 활성화
        }
    }

    // 플레이 버튼 클릭 시 호출되는 메서드
    public void OnPlayButtonPressed()
    {
        
        if (PhotonNetwork.IsMasterClient)
        {
            if (isrobot == false)
            {

                NetworkRobot = PhotonNetwork.Instantiate("Robot", new Vector3(-11, 4, 0), Quaternion.identity);
                pelvis = NetworkRobot.transform.Find("MainPelvis");
                pelvis.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                PhotonView robotview = NetworkRobot.GetPhotonView();
                //PV.RPC("robotAct", RpcTarget.AllBuffered, robotview.name);

                mousePosLA = PhotonNetwork.Instantiate("MousePosLA", Vector3.zero, Quaternion.identity);
                mousePosRA = PhotonNetwork.Instantiate("MousePosRA", Vector3.zero, Quaternion.identity);
                mousePosLL = PhotonNetwork.Instantiate("MousePosLL", Vector3.zero, Quaternion.identity);
                mousePosRL = PhotonNetwork.Instantiate("MousePosRL", Vector3.zero, Quaternion.identity);


                //이미 생성을 해버렸기 때문에 혼자 할때는 아래 소유권 이전이 의미가 없음
                leftArm = PhotonNetwork.Instantiate("Left Arm IK", Vector3.zero, Quaternion.identity);
                rightArm = PhotonNetwork.Instantiate("Right Arm IK", Vector3.zero, Quaternion.identity);
                leftLeg = PhotonNetwork.Instantiate("Left Leg IK", Vector3.zero, Quaternion.identity);
                rightLeg = PhotonNetwork.Instantiate("Right Leg IK", Vector3.zero, Quaternion.identity);

                leftArm.transform.SetParent(pelvis);
                rightArm.transform.SetParent(pelvis);
                leftLeg.transform.SetParent(pelvis);
                rightLeg.transform.SetParent(pelvis);

                leftArm.transform.localPosition = new Vector3(-1.5f, 4.2f, 0);
                rightArm.transform.localPosition = new Vector3(1.5f, 4.2f, 0);
                leftLeg.transform.localPosition = new Vector3(-1.1f, 2f, 0);
                rightLeg.transform.localPosition = new Vector3(1.1f, 2f, 0);

                leftArm.GetComponent<ArmManager>().sholder = pelvis.Find("Sholder L").gameObject;
                rightArm.GetComponent<ArmManager>().sholder = pelvis.Find("Sholder R").gameObject;
                leftLeg.GetComponent<LegManager>().pelvis = pelvis.Find("pelvisL").gameObject;
                rightLeg.GetComponent<LegManager>().pelvis = pelvis.Find("pelvisR").gameObject;

                leftArm.GetComponent<ArmManager>().mousePos = mousePosLA.transform;
                rightArm.GetComponent<ArmManager>().mousePos = mousePosRA.transform;
                leftLeg.GetComponent<LegManager>().mousePos = mousePosLL.transform;
                rightLeg.GetComponent<LegManager>().mousePos = mousePosRL.transform;

                NetworkRobot.transform.Find("MainPelvis").GetComponent<BodyController>().LeftLeg = leftLeg.GetComponent<LegManager>();
                NetworkRobot.transform.Find("MainPelvis").GetComponent<BodyController>().RightLeg = rightLeg.GetComponent<LegManager>(); ;

                
                isrobot = true;
                //leftArm = NetworkRobot.transform.Find("Left Arm IK").gameObject;
                //rightArm = NetworkRobot.transform.Find("Right Arm IK").gameObject;
                //leftLeg = NetworkRobot.transform.Find("Left Leg IK").gameObject;
                //rightLeg = NetworkRobot.transform.Find("Right Leg IK").gameObject;


            }
        }
        if (PhotonNetwork.IsMasterClient) // 방장 확인
        {
            if (gameObject4 != null)
            {
                PV.RPC("ChangeScreenAndDisableRoomCanvas", RpcTarget.All);

                int playerCount = PhotonNetwork.PlayerList.Length;
                allParts = new GameObject[] { leftLeg, rightLeg ,leftArm, rightArm};
                allMousePos = new GameObject[] { mousePosLL, mousePosRL ,mousePosLA, mousePosRA };
                main_Cam.transform.SetParent(pelvis.transform);
                main_Cam.transform.localPosition = new Vector3(0,0,-10); 
                //List<GameObject> activeParts = new List<GameObject>();
                //List<GameObject> activePos = new List<GameObject>();
                //GameObject[] activeParts = new GameObject[playerCount];
                //GameObject[] activePos = new GameObject[playerCount];
                NetworkRobot.transform.Find("MainPelvis").GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic; ;
                for (int i = 0; i < playerCount; i++)
                {
                    //int r = Random.Range(0, allParts.Length);
                    GameObject selectedPart = allParts[i];
                    GameObject selectedMousePos = allMousePos[i];

                    
                    // 이미 선택된 파츠를 중복할당하지 않도록 방지
                    //while (System.Array.Exists(activeParts, part => part == selectedPart))
                    //{
                    //    int a = Random.Range(0, allParts.Length);
                    //    selectedPart = allParts[a];
                    //    selectedMousePos = allMousePos[a];
                    //}

                    selectedPart.SetActive(true);

                    //activeParts[i] = selectedPart;
                    //activePos[i] = selectedMousePos;

                    PhotonView photonView = selectedPart.GetPhotonView();
                    PhotonView photonView2 = selectedMousePos.GetPhotonView();

                    if (photonView != null)
                    {
                        photonView.TransferOwnership(PhotonNetwork.MasterClient);
                        //photonView.TransferOwnership(PhotonNetwork.PlayerList[i].ActorNumber); // 소유권 할당
                        photonView2.TransferOwnership(PhotonNetwork.PlayerList[i].ActorNumber);
                        // 소유권이 제대로 이전되었는지 확인
                        //if (!photonView.IsMine)  // 소유권 이전 실패시
                        //{
                        //    photonView.RequestOwnership(); // 소유권을 다시 요청
                        //    photonView2.RequestOwnership();
                        //}
                    }

                    // 파츠 활성화 전파
                    PV.RPC("ActivatePartRPC", RpcTarget.AllBuffered, selectedPart.name);
                    PV.RPC("ActivatePartRPC", RpcTarget.AllBuffered, selectedMousePos.name);
                }

                Debug.Log("파츠 활성화 및 플레이어 할당 완료!");
            }
            else
            {
                Debug.LogError("GameObject(4)가 할당되지 않았습니다. 인스펙터에서 할당해 주세요.");
            }
        }
        else
        {
            Debug.LogWarning("방장만 이 버튼을 누를 수 있습니다.");
        }
        isStarted = true;
    }

    [PunRPC]
    void ChangeScreenAndDisableRoomCanvas()
    {
        gameObject4.SetActive(true);  // 모든 플레이어의 화면을 변경
        main_Cam.gameObject.SetActive(true);
        GameObject roomCanvas = GameObject.Find("RoomCanvas(3)");
        if (roomCanvas != null)
        {
            roomCanvas.SetActive(false);
        }
        else
        {
            Debug.LogError("RoomCanvas(3)를 찾을 수 없습니다.");
        }
    }

 

    // 각 플레이어에게 랜덤으로 파츠 할당
    // 방장이 각 플레이어에게 파츠를 할당할 때
    public void AssignRandomPartToPlayer()
    {
        allParts = new GameObject[] { leftArm, rightArm, leftLeg, rightLeg };
        int playerCount = PhotonNetwork.PlayerList.Length;

        if (playerCount == 1)
        {
            GameObject selectedPart = allParts[Random.Range(0, allParts.Length)];
            selectedPart.SetActive(true);

            PhotonView photonView = selectedPart.GetComponent<PhotonView>();
            if (photonView != null)
            {
                photonView.RequestOwnership(); // 소유권을 요청
            }
        }
        else
        {
            GameObject[] activeParts = new GameObject[playerCount];

            for (int i = 0; i < playerCount; i++)
            {
                GameObject selectedPart = allParts[Random.Range(0, allParts.Length)];

                while (System.Array.Exists(activeParts, part => part == selectedPart))
                {
                    selectedPart = allParts[Random.Range(0, allParts.Length)];
                }

                selectedPart.SetActive(true);
                activeParts[i] = selectedPart;

                PhotonView photonView = selectedPart.GetComponent<PhotonView>();
                if (photonView != null)
                {
                    photonView.TransferOwnership(PhotonNetwork.PlayerList[i].ActorNumber); // 소유권을 이전

                    // 소유권이 제대로 이전되었는지 확인
                    if (!photonView.IsMine)  // 소유권 이전 실패시
                    {
                        photonView.RequestOwnership(); // 소유권을 다시 요청
                    }
                }
            }
        }
    }


    // 파츠 활성화 전파
    [PunRPC]
    void ActivatePartRPC(string partName)
    {
        GameObject part = GameObject.Find(partName);
        if (part != null) part.SetActive(true);
    }

    
}
