# 2026-02-27 — 일간 로그 필터 카드 + 오류 로깅

## 버전
v 1.1.3 → **v 1.1.4**

## 변경 요약

### 1. 필터 카드 토글 (일간 로그)
상단 요약 카드 (파싱, 중복 가져오기, 합치기, 새 주택)를 탭하면 해당 카테고리만 필터링하여 상세 로그에 표시합니다.

- **토글 동작**: 카드를 탭하면 해당 필터가 활성화/비활성화됩니다. 여러 카드를 동시에 활성화할 수 있습니다.
- **비활성 카드**: 필터가 하나라도 활성화되면 비활성 카드는 `opacity: 0.35`로 흐려집니다.
- **필터 없음**: 아무 카드도 활성화되지 않으면 모든 로그가 표시됩니다.
- **줄 수 표시**: 필터 활성 시 `"12줄 / 총 85줄"` 형식으로 표시됩니다.

### 2. 오류 카드 + 오류 로깅
5번째 카드 "⚠️ 오류"가 추가되었습니다.

- 가져오기 과정에서 예외 발생 시 `ImportLogger.LogError(context, message)` 로 기록됩니다.
- 로그 파일에 `ERROR | [컨텍스트] 메시지` 형식으로 저장됩니다.
- 오류 카드를 탭하면 오류 로그만 필터링됩니다.

### 3. 가져오기 안정성 개선
세 가지 가져오기 작업에 try/catch가 추가되어, 한 건의 오류가 전체 작업을 중단시키지 않습니다:
- `ImportDuplicates_Click` — 중복 가져오기 (per-house)
- `MergeSelected_Click` — 합치기
- `FinalizeNew_Click` — 새 매물 가져오기 (per-house)

## 카테고리 ↔ 액션 매핑

| 카드 | 카테고리 키 | 포함 액션 |
|------|------------|-----------|
| 파싱 | `PARSE` | PARSE, PARSE_HOUSE, PARSE_ITEM, PARSE_DUP, PARSE_DUP_ITEM |
| 중복 가져오기 | `IMPORT_DUP` | IMPORT_DUP, IMPORT_DUP_HOUSE, IMPORT_DUP_ITEM |
| 합치기 | `MERGE` | MERGE, MERGE_ITEM |
| 새 주택 | `INSERT_NEW` | INSERT_NEW, INSERT_NEW_HOUSE, INSERT_NEW_ITEM |
| 오류 | `ERROR` | ERROR |

## 수정된 파일

| 파일 | 변경 내용 |
|------|-----------|
| `DailyLogWindow.xaml` | 5열 그리드, 카드에 x:Name + Tapped 이벤트, 오류 카드 추가 |
| `DailyLogWindow.xaml.cs` | 필터 토글 로직 (_allEntries, _activeFilters, ApplyFilter, ToggleFilter, UpdateCardVisuals, GetCategory), 카드 탭 핸들러, ERROR 카운팅 |
| `ImportLogger.cs` | `LogError(string context, string message)` 메서드 추가 |
| `BulkImportWindow.xaml.cs` | ImportDuplicates_Click, MergeSelected_Click, FinalizeNew_Click에 try/catch + LogError |
| `InfoWindow.xaml` | 버전 v 1.1.3 → v 1.1.4 |
