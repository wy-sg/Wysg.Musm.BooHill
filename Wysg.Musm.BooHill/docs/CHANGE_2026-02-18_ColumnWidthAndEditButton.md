# 변경 사항: 컬럼 너비 축소 및 편집 버튼 추가

**날짜:** 2026-02-18

## 요약

1. **메인 윈도우 컬럼 너비 축소** — 동, 호수, 상태, 순위 컬럼의 너비를 줄여 레이아웃을 개선.
2. **편집 버튼 추가** — 각 행에 "편집" 버튼을 추가하여 물건 정보를 직접 수정할 수 있는 ContentDialog 제공.

## 상세 변경

### 컬럼 너비 변경 (헤더 및 데이터 템플릿)

| 컬럼   | 변경 전 | 변경 후 |
|--------|---------|---------|
| 동     | 120     | 80      |
| 호수   | 120     | 80      |
| 상태   | 120     | 80      |
| 순위   | 120     | 70      |
| 편집   | (없음)  | 60 (신규) |

### 편집 기능

- 각 물건 행에 "편집" 버튼 (`EditHouse_Click`) 추가.
- 클릭 시 `ContentDialog`가 표시되며, 다음 필드를 편집 가능:
  - 단지, 동, 호수, 평수, 향
  - 관심, 거래 완료 (체크박스)
  - 감평액, 감평액 추정, 순위, 순위 추정
  - 태그
- 저장 시 `BooHillRepository.UpsertHouseAsync`를 호출하고 목록을 새로고침.

## 수정된 파일

| 파일 | 변경 내용 |
|------|-----------|
| `Wysg.Musm.BooHill\MainWindow.xaml` | 헤더/데이터 그리드 컬럼 너비 축소, 14번째 컬럼(편집) 추가 |
| `Wysg.Musm.BooHill\MainWindow.xaml.cs` | `EditHouse_Click`, `ShowEditHouseDialogAsync` 메서드 추가, `System.Globalization` using 추가 |
| `Wysg.Musm.BooHill\docs\CHANGE_2026-02-18_ColumnWidthAndEditButton.md` | 본 문서 |
