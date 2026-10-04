# Be ODYSSEUS — Claude Code 작업 규칙
## 프로젝트 환경

- Unity **6000.6.3f1** (Unity 6)
- 렌더 파이프라인: **Built-in** (URP/HDRP 아님)
- 입력: **Input System Package (New)만** 쓴다 (Input System 1.20.0).
  AR Foundation의 카메라 추적(`TrackedPoseDriver`)이 Input System을 필요로 하고, "Both"는 Android 빌드에서 지원되지 않는다.
  - **구버전 `UnityEngine.Input`(`Input.gyro`, `Input.acceleration`, `Input.GetKey` 등)은 쓰지 않는다.** 실행 중 예외가 난다.
  - 센서는 `UnityEngine.InputSystem`의 `Accelerometer`, `Gyroscope`, `AttitudeSensor`, `GravitySensor`로 읽는다.
    센서는 기본으로 꺼져 있으니 `InputSystem.EnableDevice(...)`로 켜야 한다.
  - 에디터 테스트 입력은 `Keyboard.current`, `Mouse.current`를 쓴다.
- AR: **AR Foundation 6.6.2 + Google ARCore 6.6.2**. 빌드 대상은 **Android** 하나다.
  - 그래픽 API는 **OpenGLES3만** 쓴다(Vulkan을 앞에 두면 ARCore가 최소 API 29를 요구한다).
  - IL2CPP + ARM64, 최소 API 26, 화면은 **Landscape Left 고정**(폰을 활에 가로로 거치).
- Asset Serialization: **Force Text**
- 코드 편집기: VS Code (`com.unity.ide.visualstudio`)
- Unity MCP: `com.unity.ai.assistant`의 MCP 서버(`unity-mcp`)로 에디터와 연결된다.

## 폴더 구조

<!-- 프로젝트가 자라면 채운다 -->
- `Assets/Scripts/` — 런타임 코드
- `Assets/Editor/` — 에디터 전용 코드 (빌드에 포함되지 않음)

## 절대 건드리지 말 것

- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `obj/` — Unity가 자동 생성한다.
- `.meta` 파일 — 직접 만들거나 고치거나 지우지 않는다.
- `.csproj`, `.sln`, `.slnx` — Unity가 다시 만든다.

## Unity 작업 규칙

- **파일 이름 변경·이동·삭제는 Unity 안에서** 한다. 밖에서 옮기면 `.meta`와 짝이 끊어져 참조가 깨진다.
  필요하면 사람에게 요청하거나 MCP로 처리한다.
- **씬(`.unity`)·프리팹(`.prefab`)·`.asset` 파일을 텍스트로 직접 편집하지 않는다.**
  대신 MCP 도구를 쓰거나, 오브젝트를 만들어 주는 에디터 스크립트(`[MenuItem]`)를 작성한다.
- 새 `.cs` 파일은 만들어도 되지만, 클래스 이름과 파일 이름을 반드시 같게 한다(MonoBehaviour/ScriptableObject).
- `UnityEditor` 네임스페이스는 `Editor/` 폴더 안이나 `#if UNITY_EDITOR` 안에서만 쓴다.
- 코드를 고친 뒤에는 콘솔 에러를 확인한다. MCP가 안 될 때는
  `%LOCALAPPDATA%\Unity\Editor\Editor.log`를 읽는다.

## 코딩 규칙

- 클래스·메서드·프로퍼티는 PascalCase, private 필드는 `_camelCase`.
- 인스펙터에 노출할 값은 `public` 필드 대신 `[SerializeField] private`를 쓴다.
- `Update()`에서 `GetComponent`/`Find` 계열을 매 프레임 호출하지 않는다. `Awake()`에서 캐시한다.

## Git

- 큰 작업을 시작하기 전에 커밋해 둔다.
- `.gitignore`는 Unity 공식 템플릿을 쓴다.
