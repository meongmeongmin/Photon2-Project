# PhotonAppSettings 설정

`PhotonAppSettings.asset`은 실제 Photon Fusion App ID를 담고 있어서 git에 커밋되지 않습니다 (`.gitignore` 참고).

## 최초 클론 후 설정 방법

1. 이 폴더의 `PhotonAppSettings.asset.template`를 복사해서 `PhotonAppSettings.asset`으로 이름을 바꾼다.
2. `AppIdFusion` 값을 Photon Dashboard(https://dashboard.photonengine.com)에서 발급받은 Fusion App ID로 교체한다.
3. Unity 에디터에서 Fusion 창(Fusion > Realtime Settings 등)을 열어 값이 제대로 반영됐는지 확인한다.

App ID는 팀 채널(예: Slack, 사내 문서)에서 공유받으세요.
