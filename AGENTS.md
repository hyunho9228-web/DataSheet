# DataSheet 작업 규칙

인귀(Human_Ghost-) 클라이언트에 데이터를 공급하는 원본·검사·배포 도구 저장소입니다. 두 프로젝트는 같은 부모 폴더에 둡니다.

**공통 작업 규칙은 클라이언트의 `../Human_Ghost-/AGENTS.md` 한곳에서 관리합니다. 작업 전에 그 파일을 먼저 읽고 따릅니다.** 이 문서에는 DataSheet에만 해당하는 규칙을 둡니다.

## 작업 시작

- 이 저장소의 README와 관련 `Docs` 문서를 먼저 읽습니다.
- 설계·구현 현황·기획자 안내는 클라이언트 `Docs`에 기록하고 여기에 복제하지 않습니다.
- `Docs/개발자_제안사항_1109.md` 같은 기획자 전달 원본에는 구현 결과를 덧쓰지 않습니다.

## 원본 데이터

- `tables/*.xlsx`, `spawn`, `dialogue`, `stats`의 원본 데이터는 기획자 소유입니다. 값이나 탭 구조를 임의로 바꾸지 않고, 바꿔야 하면 이유와 영향을 먼저 설명하고 확인받습니다.
- 스키마(컬럼·생성자)를 바꾸면 클라이언트 `Assets/3.Script/Data`의 정의 클래스도 함께 맞춥니다.

## 배포와 도구

- `DeployData.exe --deploy`(`DeployData.cmd`)는 클라이언트 파일을 덮어씁니다. 확인받기 전에는 복사하지 않는 `--check`만 실행합니다.
- 도구 소스(`Tools/DataExporter`)를 바꾸면 회귀 검사(`dotnet run --project Tools/DataExporter.Checks`) 후 `Tools/Publish-DataExport.ps1`로 EXE를 다시 만들어 소스와 함께 공유합니다. 데이터 값만 바꿀 때는 EXE를 다시 만들지 않습니다.
- 도구는 .NET 10·최신 안정 C#, 생성 결과는 C# 9 기준으로 검사합니다.
