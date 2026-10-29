# DataSheet

인귀(Unity 프로젝트) 데이터 테이블 전용 저장소입니다.
데이터를 고친 커밋이 게임 프로젝트 저장소(`lkhwcf-arch/Human_Ghost-`)에 섞이지 않도록 따로 둡니다.

- 원본: [Ingwi_DataTable 구글 시트](https://docs.google.com/spreadsheets/d/1Xn7PQsVVOY2dE_aiTLcrFxN08fgGxDH5hpaEGJBBbG8/edit)
- 기획은 구글 시트 하나에서 고칩니다(탭끼리 수식·밸런스 계산이 이어져 있어서).
- 저장소에는 그 시트를 **테이블별 xlsx 파일**(`tables/`)로 나눠 올립니다. 값만 들어 있고 수식은 없습니다.
- 각 시트 2행 기능(B2) 앞에 내보낼 형식이 적혀 있습니다: `JSON,` = UnitState(BossState·PlayerState·EliteState)·Item, `CSV,` = 나머지. 추출기는 개발팀이 만듭니다.
- 파일·폴더·탭 이름에는 한글을 쓰지 않습니다.

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
├── tables/               ← 테이블별 xlsx (파일 이름 = REGISTRY의 File 값)
│   ├── REGISTRY.xlsx     ← 로더가 맨 먼저 읽음 (시트 목록·ID 구간·읽는 순서)
│   ├── UnitStat.xlsx
│   ├── Player.xlsx
│   ├── Monster.xlsx
│   ├── UnitState.xlsx    ← 시트 3개: PlayerState·EliteState·BossState (구조 초안, 안의 상태·값은 예시·미확정)
│   ├── Item.xlsx         ← 아이템 한 시트 (무기·방어구·소모품, ItemType으로 구분)
│   ├── Skill.xlsx
│   ├── Stage.xlsx
│   ├── StageLayer.xlsx
│   ├── State.xlsx        ← 애니메이션 상태 Key 목록. Key는 프로젝트 Enum 관리형 데이터로만 씀(자동 배포). Common·Player·Monster·Boss·Required는 안 씀(비고 열)
│   └── String.xlsx
├── stats/
│   └── item/<ID>.json    ← 아이템 스탯 JSON (Item 시트 StatJson 열이 가리킴)
└── (추출 결과 CSV·JSON)   ← 개발팀 추출기가 정함 (예전 csv/ 추출본은 열이 바뀌어 지움)
```

추출기(xlsx → CSV·JSON)는 개발팀이 만들어 관리합니다.

대부분 파일 1개 = 시트 1개입니다. 플레이어·엘리트·보스의 상태(한 행 = 상태 오브젝트 하나, 같은 UnitID끼리 묶으면 유닛별 상태 배열)는 `UnitState.xlsx` 한 파일에 시트로 나눠 넣습니다. 아이템(무기·방어구·소모품)은 `Item` 시트 하나에 모으고 `ItemType`으로 구분합니다. 무기는 지금 2종입니다: 버선칼 6002(시작 무기), 쇠메 6003(완성판). 발골칼은 해체 연출에만 쓰여 아이템이 아니라서 뺐고, 6001 번호는 다시 쓰지 않습니다. 분류 값(ItemType 등)은 개발에서 enum으로 쓰는 고정 영어 키입니다. 허용 값 목록은 구글 시트 README 탭 "정해진 단어"와 각 시트 1행에 있습니다. 어느 시트가 어느 파일에 들어가는지는 REGISTRY의 `File` 열이 정합니다.

구글 시트의 README·Reference·StatCurve·Balance 탭은 기획 설명·계산용이라 저장소에 올리지 않습니다.

## 시트 규칙

| 항목 | 규칙 |
|---|---|
| 행 구성 | 1~5행 규칙·기능·역할·의도·비고(추출에서 빠짐), 6행 열 이름, 7행부터 데이터 |
| 출력 형식 | 2행 기능(B2)이 `JSON,` 또는 `CSV,`로 시작함. **JSON**: UnitState(BossState·PlayerState·EliteState), Item. **CSV**: 나머지 전부(REGISTRY, UnitStat, Player, Monster, Skill, Stage, StageLayer, State, String) |
| 비고 열 | 5행 비고(B5)에 적은 열(예: `Name, Note`)은 기획용이라 **추출에서 뺌**. ID 열은 비고에 넣을 수 없음 |
| 한글 | 비고 열에만. 내보내는 열의 값·열 이름은 영어·숫자만 |
| 데이터 끝 | A열이 빈 첫 행에서 끝. 그 아래 계산·JSON 예시(UnitStat·Stage)는 내보내지 않음 |
| ID | 각 탭 A열 숫자 ID. **중복 금지**. 탭별 구간은 REGISTRY의 IDRange |
| 목록 값 | 세로줄(`\|`)로 구분 (예: `5013\|5014\|5015`) |
| 플래그 | `Y` / `N` |
| Active | 켜고 끄는 데이터에만 둠: REGISTRY(시트 단위), Stage(스테이지 단위). `N`이면 로더가 건너뜀. 상시 쓰는 나머지 시트에는 Active 열 없음 |
| 자료형 | 모두 정수 |
| 비율·확률 | 만분율 (10000 = 100%) |
| 시간 | ms |
| 거리·속도 | 타일 × 100 |
| 표시 텍스트 | 직접 쓰지 않고 String 키로 참조 |
| 공통 열 | 같은 뜻의 값은 시트가 달라도 같은 열 이름·같은 순서. 스탯은 모든 유닛이 UnitStat 한 줄(HP, ATK, DEF, Dodge, Crit, CritMult, Poise, MoveSpeed). 몬스터 행동은 Monster·BossState·EliteState가 `IdleMin, IdleMax, PoiseRegenDelay, PoiseRegen, ExposeTime, ExecuteType, ExecuteDamage, SuperArmor`를 같은 순서로 가짐 |

## 출력 형식 (CSV·JSON)

형식은 시트 2행 기능 앞(`CSV,` / `JSON,`)이 정합니다. 둘 다 **한글 금지**입니다. 인코딩 문제를 막으려고 내보내는 값에는 영어·숫자·기호만 둡니다(SILENT와 같은 규칙). 한글은 5행 비고에 적은 열(Name·Note 등)에만 씁니다.

CSV
- UTF-8(BOM 없음), 줄바꿈 LF, 쉼표 구분
- 첫 줄 = 열 이름(시트 6행, 비고 열 제외), 둘째 줄부터 데이터
- 값 안에 쉼표가 있으면 큰따옴표로 감쌈(표준 CSV)

JSON (UnitState 세 시트, Item)
- 한 행 = 오브젝트 하나, 키 = 6행 열 이름(비고 열 제외). 상태 시트는 같은 UnitID끼리 묶어 유닛별 배열로 씀
- 정확한 모양은 개발팀 추출기가 정함

## 데이터 고치는 순서

1. 구글 시트를 고칩니다. (`tables/`·추출 결과 파일을 직접 고치지 않습니다. 다음에 다시 나눌 때 덮어써집니다.)
2. 시트를 xlsx로 받아 탭별 파일로 나눠 `tables/`를 덮어씁니다(값만, 설명·계산 탭 제외). 지금은 기획 쪽에서 Claude에게 요청해 나눕니다.
3. 개발팀 추출기로 `tables/`에서 CSV·JSON을 다시 만듭니다.
4. `git diff`로 바뀐 값을 확인하고 커밋합니다. 커밋 메시지에 무엇을 왜 바꿨는지 적습니다.
   예: `Monster: 허수귀 HP 35→40 (시연 난이도 조정)`

## 추출기에 바라는 것 (기획 요청, 구현은 개발팀)

- 위 시트 규칙대로 추출: 6행 열 이름, 7행부터 A열이 빈 첫 행까지, 5행 비고에 적힌 열 제외
- README·Reference·StatCurve·Balance 탭 제외
- 다음 중 하나라도 있으면 그 탭은 추출하지 않고 위치를 알려 줌: A열 ID 중복, 비고에 적힌 열이 6행에 없음, 추출할 칸이나 열 이름에 한글

## 아이템 스탯 JSON

Item 시트의 `StatJson` 열에 파일 이름(예: `6002.json`, 파일은 `stats/item/` 안)을 적고, 파일에는 그 아이템이 더해 주는 스탯을 배열로 넣습니다. 아이템마다 스탯 개수와 종류가 달라도 됩니다.

```json
[
  { "statType": "ATK", "value": 2 },
  { "statType": "Crit", "value": 200 }
]
```

| 키 | 값 |
|---|---|
| `statType` | enum. 플레이어 스탯 열 이름(UnitStat)과 철자·단위까지 똑같이: `HP`, `ATK`, `DEF`, `Dodge`, `Crit`, `CritMult`, `Poise`, `MoveSpeed` |
| `value` | 정수. 시트 단위 규칙 그대로(비율·확률은 만분율, 200 = 2%). 음수면 감소 |

- 스탯이 없으면 빈 배열 `[]` (기본 스탯은 UnitStat에 있음)
- 파일 이름은 아이템 ID: `stats/item/6002.json`. 한글 금지
- 지금 들어 있는 파일은 6002 버선칼, 6003 쇠메, 6101 버선, 6201 약초입니다. 값은 형식을 보여 주는 예시이며 미확정입니다
- statType을 늘릴 때는 UnitStat에 열을 먼저 만들고, 이 표와 구글 시트 README 탭 "정해진 단어"를 같이 고칩니다
- 무기 평타(타수·배율·타이밍)는 데이터로 두지 않습니다. 무기끼리의 차이는 이 스탯 JSON으로만 줍니다(Attack 테이블 삭제, 10/29)

## 변경 기록

**10/29**
- SILENT식 숫자 ID로 개편, 탭별 ID 구간, REGISTRY 시트 추가 (지운 번호는 다시 쓰지 않음)
- 구글 시트 제목 `Ingwi_DataTable`. 파일·폴더·탭 이름과 내보내는 값에 한글 금지
- 저장소를 테이블별 xlsx(`tables/`)로 나눔. 추출기는 개발팀 담당. main에 바로 올림
- State 분류 변경(Common 9개, Stun·Skill2·Skill3·Bound·PhaseChange 추가)
- 상태 시트 PlayerState·EliteState·BossState 추가 → `UnitState.xlsx` (구조 예시·미확정). UnitStat Type에 Elite
- Active 열은 REGISTRY·Stage에만 남김
- 아이템을 Item 한 시트로 합침, ItemType enum, StatJson → `stats/item/<ID>.json`
- 각 시트 2행 기능 앞에 출력 형식 `CSV,` / `JSON,` 표시
- State 시트: Key만 프로젝트 Enum 관리형 데이터로 씀(등록→자동 배포). Common·Player·Monster·Boss·Required 열은 안 쓰고 5행 비고에 넣어 추출에서 뺌. 상태 동작은 코드로 구현하고, 특수 상태만 나중에 따로 JSON. UnitState(유닛별 상태 값 JSON)와는 별개
- 무기 2종으로 정리: 버선칼 6002 = 시작 무기(START, 지스타, 공격 5001~5004, 스킬 7001), 쇠메 6003 = 완성판. 발골칼 6001은 해체 연출용이라 Item에서 빼고 `stats/item/6001.json` 삭제. Player.StartWeapon·Skill.WeaponID → 6002
- Item StatJson 값은 경로 대신 파일 이름만(`6002.json`, 개발팀 f44287f)
- Attack 테이블 삭제(평타 타수까지 데이터로 두면 기획이 너무 무거워진다는 피드백). Item AttackIDs, Monster·BossState·EliteState AttackIDs 열 삭제, 스킬 공격 값(배율·체세 피해·StateID)은 Skill로 옮김
- 몬스터 공통 열 통일: Monster·BossState·EliteState 모두 `IdleMin, IdleMax, PoiseRegenDelay, PoiseRegen, ExposeTime, ExecuteType, ExecuteDamage, SuperArmor` (보스 IdleTime → IdleMin·IdleMax, 보스 ExecuteType DAMAGE 추가)

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
