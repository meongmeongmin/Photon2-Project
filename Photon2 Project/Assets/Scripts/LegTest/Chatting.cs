using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.UI;


public class Chatting : MonoBehaviour
{
    public static Chatting Instance;

    [Header("DisconnectPanel")]
    public InputField NickNameInput;

    [Header("LobbyPanel")]
    public GameObject LobbyPanel;
    public Text WelcomeText;
    public Text LobbyInfoText;
    public Button[] CellBtn;
    public Button PreviousBtn;
    public Button NextBtn;

    [Header("RoomPanel")]
    public GameObject RoomPanel;
    public Text ListText;
    public Text RoomInfoText;
    public Text[] ChatText;
    public InputField ChatInput;

    [Header("ETC")]
    public Text StatusText;

    string localNickName;
    List<SessionInfo> myList = new List<SessionInfo>();
    int currentPage = 1, maxPage, multiple;

    void Awake()
    {
        Instance = this;
    }


    #region 방리스트 갱신
    // ◀버튼 -2 , ▶버튼 -1 , 셀 숫자
    public void MyListClick(int num)
    {
        if (num == -2) --currentPage;
        else if (num == -1) ++currentPage;
        else NetworkManager.Instance.JoinRoomByName(myList[multiple + num].Name);
        MyListRenewal();
    }

    void MyListRenewal()
    {
        // 최대페이지
        maxPage = (myList.Count % CellBtn.Length == 0) ? myList.Count / CellBtn.Length : myList.Count / CellBtn.Length + 1;

        // 이전, 다음버튼
        PreviousBtn.interactable = (currentPage <= 1) ? false : true;
        NextBtn.interactable = (currentPage >= maxPage) ? false : true;

        // 페이지에 맞는 리스트 대입
        multiple = (currentPage - 1) * CellBtn.Length;
        for (int i = 0; i < CellBtn.Length; i++)
        {
            CellBtn[i].interactable = (multiple + i < myList.Count) ? true : false;
            CellBtn[i].transform.GetChild(0).GetComponent<Text>().text = (multiple + i < myList.Count) ? myList[multiple + i].Name : "";
            CellBtn[i].transform.GetChild(1).GetComponent<Text>().text = (multiple + i < myList.Count) ? myList[multiple + i].PlayerCount + "/" + myList[multiple + i].MaxPlayers : "";
        }
    }

    // NetworkManager.OnSessionListUpdated에서 매 프레임 최신 목록을 들고 있으므로 여기서는 폴링만 한다
    void Update()
    {
        if (StatusText != null)
            StatusText.text = NetworkManager.Runner != null ? NetworkManager.Runner.State.ToString() : "Disconnected";

        if (!SessionListEquals(myList, NetworkManager.LastSessionList))
        {
            myList = new List<SessionInfo>(NetworkManager.LastSessionList);
            MyListRenewal();
        }

        if (LobbyInfoText != null && NetworkManager.Runner != null)
            LobbyInfoText.text = myList.Count + "개 방";
    }

    bool SessionListEquals(List<SessionInfo> a, List<SessionInfo> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i].Name != b[i].Name || a[i].PlayerCount != b[i].PlayerCount) return false;
        return true;
    }
    #endregion


    #region 서버연결
    void Start() => Screen.SetResolution(960, 540, false);

    public void Connect() => NetworkManager.Instance.JoinLobby();

    public void HandleJoinedLobby()
    {
        Debug.Log("서버접속완료");
        LobbyPanel.SetActive(true);
        RoomPanel.SetActive(false);
        //닉네임
        localNickName = "Player" + Random.Range(1, 100);
        WelcomeText.text = localNickName + "님 환영합니다";
        myList.Clear();
    }

    public void Disconnect() => NetworkManager.Instance.LeaveLobby();
    #endregion


    #region 방

    public string defaultRoomName;
    public async void CreateRoom()
    {
        // 1부터 99까지의 랜덤 숫자를 생성하여 방 이름을 만듭니다.
        defaultRoomName = "Room" + Random.Range(1, 100);
        var runner = NetworkManager.Instance.EnsureRunner();
        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = defaultRoomName,
            PlayerCount = 4,
        });
        if (result.Ok)
        {
            NetworkManager.Instance.SpawnSessionRpcIfHost(runner);
            HandleJoinedRoom();
        }
    }

    public void JoinRandomRoom() => NetworkManager.Instance.JoinRandomRoom();

    public void LeaveRoom() => NetworkManager.Instance.LeaveRoom();

    public void HandleJoinedRoom()
    {
        RoomPanel.SetActive(true);
        RoomRenewal();
        ChatInput.text = "";
        for (int i = 0; i < ChatText.Length; i++) ChatText[i].text = "";
    }


    public void HandlePlayerJoined(NetworkRunner runner, PlayerRef newPlayer)
    {
        RoomRenewal();
        if (newPlayer != runner.LocalPlayer)
            SessionRpc.Instance?.RPC_Chat("<color=yellow>Player" + newPlayer.PlayerId + "님이 참가하셨습니다</color>");
    }

    public void HandlePlayerLeft(NetworkRunner runner, PlayerRef otherPlayer)
    {
        RoomRenewal();
        SessionRpc.Instance?.RPC_Chat("<color=yellow>Player" + otherPlayer.PlayerId + "님이 퇴장하셨습니다</color>");
    }

    void RoomRenewal()
    {
        if (NetworkManager.Runner == null || !NetworkManager.Runner.IsRunning) return;

        ListText.text = "";
        var players = NetworkManager.Runner.ActivePlayers;
        int count = 0;
        foreach (var p in players) count++;
        int idx = 0;
        foreach (var p in players)
        {
            ListText.text += "Player" + p.PlayerId + ((++idx == count) ? "" : ", ");
        }
        RoomInfoText.text = NetworkManager.Runner.SessionInfo.Name + " / " + NetworkManager.Runner.SessionInfo.PlayerCount + "명 / " + NetworkManager.Runner.SessionInfo.MaxPlayers + "최대";
    }
    #endregion


    #region 채팅
    public void Send()
    {
        SessionRpc.Instance?.RPC_Chat(localNickName + " : " + ChatInput.text);
        ChatInput.text = "";
    }

    public void OnChatMessage(string msg)
    {
        bool isInput = false;
        for (int i = 0; i < ChatText.Length; i++)
            if (ChatText[i].text == "")
            {
                isInput = true;
                ChatText[i].text = msg;
                break;
            }
        if (!isInput) // 꽉차면 한칸씩 위로 올림
        {
            for (int i = 1; i < ChatText.Length; i++) ChatText[i - 1].text = ChatText[i].text;
            ChatText[ChatText.Length - 1].text = msg;
        }
    }
    #endregion
}
