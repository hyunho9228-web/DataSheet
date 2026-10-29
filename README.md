# DataSheet

인귀(Unity 프로젝트) 데이터 테이블 전용 저장소입니다.
데이터를 고친 커밋이 게임 프로젝트 저장소(`lkhwcf-arch/Human_Ghost-`)에 섞이지 않도록 따로 둡니다.

- 원본: [데이터 테이블 구글 시트](https://docs.google.com/spreadsheets/d/1Xn7PQsVVOY2dE_aiTLcrFxN08fgGxDH5hpaEGJBBbG8/edit)
- 게임이 읽는 것은 `csv/`의 탭별 CSV입니다.
- `source/인귀_데이터테이블.xlsx`는 시트를 통째로 받은 스냅샷입니다. CSV와 같은 시점의 시트 전체(수식·설명 탭 포함)를 남기려고 같이 커밋합니다. xlsx는 바이너리라 diff가 안 보이니 변경 확인은 CSV로 합니다.

## 로컬 폴더 구성

메인 폴더 아래에 프로젝트 폴더와 데이터 폴더를 나란히 둡니다.

```
Ingwi/                    ← 메인 폴더 (이름 자유)
├── Human_Ghost-/         ← Unity 프로젝트 저장소
└── Data/                 ← 이 저장소
```

```bash
cd Ingwi
git clone https://github.com/lkhwcf-arch/Human_Ghost-.git
git clone https://github.com/hyunho9228-web/DataSheet.git Data
```

데이터 폴더 이름은 `Data`로 통일합니다. Unity 쪽 경로(`../Data`)가 이 이름을 기준으로 합니다.

## 저장소 구조

```
Data/
├── README.md
├── source/
│   └── 인귀_데이터테이블.xlsx ← 시트 전체 스냅샷 (CSV와 같은 시점)
├── csv/                  ← 게임이 읽는 테이블 (탭 1개 = 파일 1개)
│   ├── REGISTRY.csv      ← 로더가 맨 먼저 읽음 (시트 목록·ID 구간·읽는 순서)
│   ├── UnitStat.csv
│   ├── Player.csv
│   ├── Monster.csv
│   ├── Boss.csv
│   ├── Attack.csv
│   ├── Weapon.csv
│   ├── Skill.csv
│   ├── Stage.csv
│   ├── StageLayer.csv
│   ├── State.csv
│   └── String.csv
```

CSV 추출기는 개발팀이 만들어 관리합니다. 이 저장소에는 시트와 추출 결과(CSV)만 둡니다.

README·Reference·StatCurve·Balance 탭은 설명·계산용이라 CSV로 내보내지 않습니다.

## 시트 규칙

| 항목 | 규칙 |
|---|---|
| 행 구성 | 1~5행 규칙·기능·역할·의도·비고(CSV에서 빠짐), 6행 열 이름, 7행부터 데이터 |
| 비고 열 | 5행 비고(B5)에 적은 열(예: `Name, Note`)은 기획용이라 **CSV에서 뺌**. ID 열은 비고에 넣을 수 없음 |
| 한글 | 비고 열에만. 내보내는 열의 값·열 이름은 영어·숫자만 |
| 데이터 끝 | A열이 빈 첫 행에서 끝. 그 아래 계산·JSON 예시(UnitStat·Stage)는 내보내지 않음 |
| ID | 각 탭 A열 숫자 ID. **중복 금지**. 탭별 구간은 REGISTRY의 IDRange |
| 목록 값 | 세로줄(`\|`)로 구분 (예: `5013\|5014\|5015`) |
| 플래그 | `Y` / `N` |
| Active | `N`인 행도 CSV에는 남기고, 로더가 건너뜀 |
| 자료형 | 모두 정수 |
| 비율·확률 | 만분율 (10000 = 100%) |
| 시간 | ms |
| 거리·속도 | 타일 × 100 |
| 표시 텍스트 | 직접 쓰지 않고 String 키로 참조 |

## CSV 형식

- UTF-8(BOM 없음), 줄바꿈 LF, 쉼표 구분
- 첫 줄 = 열 이름(시트 6행, 비고 열 제외), 둘째 줄부터 데이터
- **한글 금지.** 인코딩 문제를 막으려고 CSV에는 영어·숫자·기호만 둡니다(SILENT와 같은 규칙).
  한글은 5행 비고에 적은 열(Name·Note 등)에만 씁니다.
- 값 안에 쉼표가 있으면 큰따옴표로 감쌈(표준 CSV)

## 데이터 고치는 순서

1. 구글 시트를 고칩니다. (CSV를 직접 고치지 않습니다. 다음 내보내기 때 덮어써집니다.)
2. 시트에서 `파일 > 다운로드 > Microsoft Excel(.xlsx)` 하고, 받은 파일로 `source/인귀_데이터테이블.xlsx`를 덮어씁니다.
3. 개발팀 추출기로 `csv/`를 다시 만듭니다.
4. `git diff`로 바뀐 값을 확인하고 커밋합니다. 커밋 메시지에 무엇을 왜 바꿨는지 적습니다.
   예: `Monster: 허수귀 HP 35→40 (시연 난이도 조정)`

## 추출기에 바라는 것 (기획 요청, 구현은 개발팀)

- 위 시트 규칙대로 추출: 6행 열 이름, 7행부터 A열이 빈 첫 행까지, 5행 비고에 적힌 열 제외
- README·Reference·StatCurve·Balance 탭 제외
- 다음 중 하나라도 있으면 그 탭은 추출하지 않고 위치를 알려 줌: A열 ID 중복, 비고에 적힌 열이 6행에 없음, 추출할 칸이나 열 이름에 한글

## Unity 연동 (제안, 메인 개발과 확정 필요)

아래는 제안입니다. 실제 방식은 나영민 님과 정합니다.

**에디터에서는 `../Data/csv`를 바로 읽고, 빌드할 때만 `StreamingAssets`로 복사**하는 방식을 권합니다.

- 에디터: `Path.Combine(Application.dataPath, "../../Data/csv")`
  (`Application.dataPath` = `Human_Ghost-/Assets` 이므로 두 단계 위가 메인 폴더)
- 빌드: `IPreprocessBuildWithReport`에서 `../Data/csv/*.csv`를 `Assets/StreamingAssets/Data/`로 복사, 빌드 뒤 지움.
  런타임은 `Path.Combine(Application.streamingAssetsPath, "Data")`에서 읽음
- 프로젝트 저장소 `.gitignore`에 `Assets/StreamingAssets/Data/` 추가 → 데이터가 프로젝트 깃에 들어가지 않음

```csharp
static string DataDir =>
#if UNITY_EDITOR
    System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../Data/csv"));
#else
    System.IO.Path.Combine(Application.streamingAssetsPath, "Data");
#endif
```

다른 방법과 비교:

| 방법 | 장점 | 단점 |
|---|---|---|
| **../Data 읽기 + 빌드 때 복사 (권장)** | 프로젝트 깃에 데이터가 전혀 안 섞임, 시트 고치고 바로 플레이 확인 | 빌드 스크립트 하나 필요, 폴더 배치 규칙을 모두 지켜야 함 |
| git submodule | 프로젝트가 데이터 버전을 커밋으로 고정 | 데이터 바꿀 때마다 프로젝트에도 커밋 생김(목적과 반대), 서브모듈 사용법 학습 필요 |
| CSV를 StreamingAssets에 직접 복사해 커밋 | 설정 없음 | 데이터 변경이 프로젝트 깃에 섞임 |

지스타 제출 빌드(11/18)에 들어간 데이터는 이 저장소에 태그(`gstar-2026`)로 남겨 두면 나중에 같은 상태로 되돌릴 수 있습니다.
