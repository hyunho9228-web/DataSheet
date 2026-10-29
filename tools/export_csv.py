"""구글 시트에서 받은 xlsx를 탭별 CSV로 내보낸다.

사용법:
    1) 구글 시트 > 파일 > 다운로드 > Microsoft Excel(.xlsx)
    2) python tools/export_csv.py 받은파일.xlsx
       -> csv/<탭이름>.csv 로 덮어씀.
       받은 xlsx는 source/인귀_데이터테이블.xlsx 로 덮어써서 같이 커밋한다(스냅샷).

시트 규칙(README 참고): 1~5행은 설명(규칙·기능·역할·의도·비고), 6행 = 열 이름,
7행부터 데이터. A열이 빈 첫 행에서 읽기를 멈춤(그 아래 계산·JSON 예시는 데이터가 아님).
5행 비고(B5)에 적힌 열(예: "Name, Note")은 기획용이라 CSV에서 뺌.
CSV에는 한글이 들어가면 안 됨(인코딩 문제 예방). 한글은 비고 열이나 String 탭에만 둔다.
내보내면서 검사하는 것: A열 ID 중복, 빈 열 이름, 비고에 적혔는데 6행에 없는 열,
내보낼 칸의 한글.
"""
import csv
import re
import sys
from pathlib import Path

from openpyxl import load_workbook

HEADER_ROW = 6
NOTE_ROW = 5
# 설명·계산용 탭은 내보내지 않음
SKIP_SHEETS = {"README", "Reference", "StatCurve", "Balance"}

HANGUL = re.compile("[\u1100-\u11ff\u3130-\u318f\uac00-\ud7a3]")

OUT_DIR = Path(__file__).resolve().parent.parent / "csv"


def cell_text(v):
    if v is None:
        return ""
    if isinstance(v, float) and v.is_integer():
        return str(int(v))
    return str(v)


def note_columns(ws):
    """5행 비고에 적힌, 코드가 읽지 않는 열 이름들."""
    label, value = (cell_text(v) for v in next(ws.iter_rows(
        min_row=NOTE_ROW, max_row=NOTE_ROW, max_col=2, values_only=True)))
    if label != "비고":
        return []
    return [c.strip() for c in value.split(",") if c.strip()]


def export_sheet(ws):
    rows = list(ws.iter_rows(min_row=HEADER_ROW, values_only=True))
    if not rows:
        return None, ["데이터 없음"]
    header = [cell_text(v) for v in rows[0]]
    while header and header[-1] == "":
        header.pop()
    width = len(header)
    errors = []
    if "" in header:
        errors.append(f"6행에 빈 열 이름이 있음 (열 {header.index('') + 1})")

    drop = note_columns(ws)
    for c in drop:
        if c not in header:
            errors.append(f"5행 비고의 열 {c}이(가) 6행에 없음")
        elif header.index(c) == 0:
            errors.append(f"5행 비고에 ID 열({c})이 있음")
    keep = [j for j, h in enumerate(header) if h not in drop]
    for j in keep:
        if HANGUL.search(header[j]):
            errors.append(f"6행 열 이름에 한글: {header[j]}")

    data, seen = [], {}
    for i, r in enumerate(rows[1:], start=HEADER_ROW + 1):
        vals = [cell_text(v) for v in r[:width]]
        vals += [""] * (width - len(vals))
        rid = vals[0]
        if rid == "":
            break
        if rid in seen:
            errors.append(f"{i}행: ID {rid} 중복 ({seen[rid]}행과 같음)")
        else:
            seen[rid] = i
        for j in keep:
            if HANGUL.search(vals[j]):
                errors.append(f"{i}행 {header[j]} 열에 한글: {vals[j]}")
        data.append([vals[j] for j in keep])
    return [[header[j] for j in keep]] + data, errors


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    wb = load_workbook(sys.argv[1], data_only=True, read_only=True)
    OUT_DIR.mkdir(exist_ok=True)
    failed = False
    for ws in wb.worksheets:
        if ws.title in SKIP_SHEETS:
            continue
        table, errors = export_sheet(ws)
        for e in errors:
            print(f"[오류] {ws.title}: {e}")
        if errors:
            failed = True
            continue
        path = OUT_DIR / f"{ws.title}.csv"
        with open(path, "w", encoding="utf-8", newline="") as f:
            csv.writer(f, lineterminator="\n").writerows(table)
        print(f"{ws.title}: {len(table) - 1}행 -> {path.relative_to(OUT_DIR.parent)}")
    if failed:
        print("오류가 있는 탭은 내보내지 않았습니다. 시트를 고친 뒤 다시 실행하세요.")
        sys.exit(1)


if __name__ == "__main__":
    main()
