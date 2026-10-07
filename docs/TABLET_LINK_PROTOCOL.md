# 폰 → 태블릿 통신 규격 (Be ODYSSEUS)

폰이 태블릿으로 보내는 메시지 형식입니다.

아래 JSON은 모두 **예시**입니다. 필드 이름과 고정값(`"v":1`, `"from":"phone"`, `"port":47800`, `type` 이름)은 그대로 보내고,
숫자·문자 값은 그때그때 달라집니다. 값의 범위는 각 장의 표에 적었습니다.

## 1. 기본

| 항목 | 값 |
|---|---|
| 방식 | UDP, 패킷 하나에 JSON 메시지 하나 (UTF-8) |
| 폰이 보내는 곳 (태블릿 포트) | **47801** |
| 폰이 답을 받는 곳 (폰 포트) | **47800** |
| 연결 코드 | 폰 화면에 뜨는 숫자 4자리 **문자열** (예: `"4821"`, `"0371"`) |

## 2. 모든 메시지 공통 필드

| 필드 | 타입 | 값 범위 | 설명 |
|---|---|---|---|
| `v` | int | `1` (고정) | 규격 버전 |
| `type` | string | 3·4장의 type 이름 | 메시지 종류 |
| `from` | string | `"phone"` (고정) | 보낸 쪽 |
| `seq` | int | 1 이상, 계속 증가 | 메시지마다 1씩 올리는 번호. 폰 앱을 다시 켜도 1로 돌아가지 않고 더 큰 번호부터 이어집니다 (중간에 몇십 번 건너뛸 수 있음) |
| `session` | string | `"00000000"` ~ `"99999999"` | 연결할 때 폰이 만드는 숫자 8자리 문자열 |

## 3. 연결

### 연결 순서

1. **폰**: "태블릿 연결"을 누르면 화면에 4자리 코드(예: `4821`)가 뜨고, `pair_offer`(코드 + `session`)를 0.5초마다 와이파이 전체에 보냅니다.
   처음에는 태블릿이 폰 주소를 모르기 때문에, 폰이 먼저 와이파이 전체에 보냅니다.
2. **태블릿**: 47801 포트로 `pair_offer`를 받습니다. 폰 주소는 **그 패킷을 보낸 IP + `port`(47800)**입니다. 사용자가 태블릿에 코드를 입력합니다.
3. **태블릿 → 폰**: "코드를 입력했다"는 신호를 폰 주소로 보내 주세요. **입력한 코드와 `pair_offer`의 `session`**을 넣어 주세요 (형식은 5장).
4. **폰**: 받은 코드가 화면의 코드와 같으면 `pair_confirm`(수락)을 보내고 연결됩니다. 이후 게임 기록을 보냅니다.

### 폰이 보내는 메시지

| type | 언제 | 보내는 곳 |
|---|---|---|
| `pair_offer` | 폰에 코드 화면이 열려 있는 동안 0.5초마다 | 브로드캐스트 `255.255.255.255:47801` |
| `pair_confirm` | 태블릿의 연결 수락 답을 받았을 때 | 태블릿 |
| `ping` | 연결된 동안 1초마다 | 태블릿 |
| `hello` | 폰 앱을 다시 켰거나 끊겼을 때, 다시 찾는 동안 1초마다 | 브로드캐스트 + 저장된 태블릿 주소 |
| `unpair` | 폰에서 연결을 해제할 때 3번 | 태블릿 |

```json
{"v":1,"type":"pair_offer","from":"phone","seq":12,"session":"40232009","code":"4821","port":47800,"device":"SM-S918N"}
{"v":1,"type":"pair_confirm","from":"phone","seq":13,"session":"40232009"}
{"v":1,"type":"ping","from":"phone","seq":14,"session":"40232009"}
{"v":1,"type":"hello","from":"phone","seq":155,"session":"40232009","port":47800,"device":"SM-S918N"}
{"v":1,"type":"unpair","from":"phone","seq":90,"session":"40232009"}
```

| 필드 | 타입 | 값 범위 | 설명 |
|---|---|---|---|
| `code` | string | `"0000"` ~ `"9999"` | 폰 화면에 뜬 연결 코드 (앞자리 0도 그대로 문자열) |
| `port` | int | `47800` (고정) | 폰이 답을 받는 포트 |
| `device` | string | 기기 모델명 (예: `"SM-S918N"`) | 화면 표시용 |

- `hello`의 `session`이 전에 연결했던 것과 같으면 같은 폰입니다 (코드 다시 입력 없이 다시 연결).
- `pair_offer`, `hello`는 브로드캐스트라서 기기에 따라 `WifiManager.MulticastLock`을 잡아야 받아집니다.
- 브로드캐스트는 `255.255.255.255`와 네트워크 브로드캐스트 주소(예: `192.168.43.255`) 두 곳으로 보내서, **같은 메시지(같은 `seq`)가 두세 번 올 수 있습니다.**

## 4. 게임 기록

| type | 언제 |
|---|---|
| `game_start` | "게임 시작"을 눌렀을 때 |
| `stage_start` | 스테이지가 시작될 때 |
| `shot` | 활을 쏴서 판정이 나왔을 때 |
| `stage_end` | 스테이지가 끝났을 때 (클리어/실패) |
| `game_end` | 메인 화면으로 돌아갈 때 |

```json
{"v":1,"type":"game_start","from":"phone","seq":31,"session":"40232009","gameId":"20261005-124245"}

{"v":1,"type":"stage_start","from":"phone","seq":32,"session":"40232009","gameId":"20261005-124245","stage":1,"monster":"polyphemus"}

{"v":1,"type":"shot","from":"phone","seq":40,"session":"40232009",
 "gameId":"20261005-124245","stage":1,"shotIndex":2,"hit":true,"killed":true,"accuracy":0.86,"stability":0.81}

{"v":1,"type":"stage_end","from":"phone","seq":41,"session":"40232009",
 "gameId":"20261005-124245","stage":1,"cleared":true,
 "totalScore":2,"totalHits":2,"totalShots":2,"hitRate":1.0,"avgAccuracy":0.81,"avgStability":0.76}

{"v":1,"type":"game_end","from":"phone","seq":45,"session":"40232009",
 "gameId":"20261005-124245","completed":true,
 "totalScore":5,"totalHits":5,"totalShots":8,"hitRate":0.625,"avgAccuracy":0.69,"avgStability":0.74}
```

게임 규칙: 스테이지 3개, 스테이지마다 화살 최대 3발, 몬스터는 두 번 맞히면 쓰러지고 그 스테이지가 바로 끝납니다.

| 필드 | 타입 | 값 범위 | 뜻 |
|---|---|---|---|
| `gameId` | string | `"YYYYMMDD-HHMMSS"` (예: `"20261005-124245"`) | 한 판(게임 시작 ~ 메인 화면 복귀)을 구분하는 값. 게임을 시작한 날짜-시각 |
| `stage` | int | 1 ~ 3 | 스테이지 번호 |
| `monster` | string | `"polyphemus"`, `"shade"`, `"siren"` | 1·2·3스테이지 몬스터 |
| `shotIndex` | int | 1 ~ 3 | 그 스테이지에서 몇 번째 발인지 |
| `hit` | bool | `true` / `false` | 몬스터를 맞혔는지 |
| `killed` | bool | `true` / `false` | 이 발로 몬스터가 쓰러졌는지 (`hit`이 `false`면 항상 `false`, `true`면 스테이지 클리어) |
| `accuracy` | 실수 | 0.0 ~ 1.0 | 정확도. 쏜 순간 조준점이 몬스터 중심에 가까울수록 1 (빗맞혀도 가까웠으면 0보다 클 수 있음) |
| `stability` | 실수 | 0.0 ~ 1.0 | 조준 안정도. 쏘기 직전 1초 동안 덜 흔들릴수록 1 |
| `cleared` | bool | `true` / `false` | 그 스테이지에서 몬스터를 쓰러뜨렸는지 |
| `totalScore` | int | 0 ~ 6 | 누적 점수 (= 맞힌 수, 스테이지마다 최대 2) |
| `totalHits` | int | 0 ~ 6 | 누적 맞힌 수 |
| `totalShots` | int | 0 ~ 9 | 누적 쏜 수 (스테이지마다 최대 3) |
| `hitRate` | 실수 | 0.0 ~ 1.0 | 누적 명중률 (= totalHits ÷ totalShots) |
| `avgAccuracy` | 실수 | 0.0 ~ 1.0 | 누적 평균 정확도 |
| `avgStability` | 실수 | 0.0 ~ 1.0 | 누적 평균 조준 안정도 |
| `completed` | bool | `true` / `false` | 마지막 스테이지까지 마쳤으면 `true`, 중간에 나갔으면 `false` |

- `total…`, `hitRate`, `avg…`는 **게임 시작부터 지금까지 누적**입니다. `stage_end`에서는 그 스테이지까지의 누적이라, n스테이지 끝에서는 `totalHits` ≤ 2n, `totalShots` ≤ 3n 입니다.
- 쏜 발이 하나도 없으면 `hitRate`, `avgAccuracy`, `avgStability`는 `0`입니다.

- 게임 기록은 빠지면 안 돼서, 태블릿의 "받았음" 답이 올 때까지 **0.25초마다, 처음 보낸 것까지 최대 8번 보냅니다.** 다시 보낸 메시지는 `seq`가 같으니 `seq`로 중복을 걸러 주세요.
- 쏜 순간의 자세는 태블릿이 `shot`을 받은 시각 기준으로 찾으면 됩니다 (두 기기 시계는 맞추지 않음).

## 5. 태블릿에서 알려 주실 것

- "코드를 입력했다"는 신호(3장 3번)와 "받았음" 답(게임 기록에 대한 답)의 형식
  - "코드를 입력했다" 신호에는 **입력한 코드와 `pair_offer`의 `session`**을 넣어 주세요.
  - "받았음" 답에는 **받은 메시지의 `seq`**를 꼭 넣어 주세요. 폰은 그걸 보고 다시 보내기를 멈춥니다.
- 그 밖에 태블릿 → 폰으로 보내는 메시지 형식
