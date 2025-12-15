using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public Text StatusText;
    public InputField roomInput;

    void Start()
    {
        Screen.SetResolution(960, 540, false); // false = 전체화면x
        PhotonNetwork.GameVersion = "1.0";  // 게임 버전 설정
    }

    void Update()
    {
        // 네트워크 상태 표시
        if (StatusText != null)
        {
            StatusText.text = PhotonNetwork.NetworkClientState.ToString();
        }
    }

    // 서버 접속후 함수
    public override void OnConnectedToMaster()
    {
        Debug.Log("서버접속완료");
    }

    public void JoinLobby() => PhotonNetwork.JoinLobby();

    public override void OnJoinedLobby()
    {
        print("로비접속완료");
    }

    public void LeaveLobby() => PhotonNetwork.LeaveLobby();

    public override void OnLeftLobby()
    {
        print("로비 나가기 완료");
    }

    // 임시 테스트용
    public void DisconnectFromServer()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            Debug.Log("서버 연결을 끊었습니다.");
        }
        else
        {
            Debug.LogWarning("이미 연결이 끊어진 상태입니다.");
        }
    }

    public void CreateRoom()
    {
        int roomCount = PhotonNetwork.CountOfRooms;
        string defaultRoomName = "Room" + (roomCount + 1);

        PhotonNetwork.CreateRoom(defaultRoomName, new RoomOptions { MaxPlayers = 5 });
        Debug.Log($"방생성완료. 방 이름: {defaultRoomName}");
    }

    // 친구와 플레이 (방 코드입력)
    public void JoinRoom()
    {
        if (roomInput != null && !string.IsNullOrEmpty(roomInput.text))
        {
            PhotonNetwork.JoinRoom(roomInput.text);
        }
        else
        {
            Debug.LogWarning("Room name is empty. Please enter a room name.");
        }
    }

    public void ExitGame()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            Debug.Log("서버 연결을 끊었습니다.");
        }

        Debug.Log("게임을 종료합니다.");
        Application.Quit();
    }

    void OnApplicationQuit()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
            Debug.Log("강제 종료 - 서버 연결을 끊었습니다.");
        }

        Debug.Log("게임이 강제 종료되었습니다.");
    }

    public void JoinRandomRoom() => PhotonNetwork.JoinRandomRoom();

    public void LeaveRoom() => PhotonNetwork.LeaveRoom();

    public override void OnCreatedRoom()
    {
        print("방만들기완료");
    }

    public override void OnJoinedRoom()
    {
        print("방참가완료");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        print($"방만들기실패: {message}");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        print($"방참가실패: {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log($"방 참가 실패: {message}");
    }

    [ContextMenu("정보")]
    void Info()
    {
        if (PhotonNetwork.InRoom)
        {
            print("현재 방 이름 : " + PhotonNetwork.CurrentRoom.Name);
            print("현재 방 인원수 : " + PhotonNetwork.CurrentRoom.PlayerCount);
            print("현재 방 최대인원수 : " + PhotonNetwork.CurrentRoom.MaxPlayers);

            string playerStr = "방에 있는 플레이어 목록 : ";
            foreach (var player in PhotonNetwork.PlayerList)
            {
                playerStr += player.NickName + ", ";
            }
            print(playerStr);
        }
        else
        {
            print("접속한 인원 수 : " + PhotonNetwork.CountOfPlayers);
            print("방 개수 : " + PhotonNetwork.CountOfRooms);
            print("모든 방에 있는 인원 수 : " + PhotonNetwork.CountOfPlayersInRooms);
            print("로비에 있는지? : " + PhotonNetwork.InLobby);
            print("연결됐는지? : " + PhotonNetwork.IsConnected);
        }
    }
}
