# 한꺼번에 가져오기 일간 로그 기능 추가 (v 1.1.2)

**Date:** 2026-02-26

## Summary

한꺼번에 가져오기(Bulk Import) 시 **모든** 작업 내역을 개별 주택·매물 단위로
로컬 파일에 상세 기록하고, 별도의 **일간 로그** 창에서 날짜별로 인포그래픽
스타일(요약 카드 + 계층형 리스트)로 확인할 수 있는 기능을 추가했습니다.

버전을 `v 1.0.1` → `v 1.1.2`로 올렸습니다.

## 로그 파일

- 위치: `LocalFolder/logs/YYYYMMDD.log`
- 한 줄 형식: `[yyyy-MM-dd HH:mm:ss] ACTION | 상세 내용`
- 들여쓰기로 계층 표현: 요약(0칸) → 주택(2칸) → 매물(4칸)
- 기록되는 작업:

| Action | 설명 |
|---|---|
| **PARSE** | 시뮬레이션(파싱) 요약 (총 주택, 매물, 중복 수) |
| PARSE_HOUSE | 파싱된 새 후보 주택 (단지·동·호·평·향, 매물 수) |
| PARSE_ITEM | 파싱된 매물 (유형·가격·부동산·날짜·비고) |
| PARSE_DUP | 파싱 시 발견된 중복 주택 (대상 house_id) |
| PARSE_DUP_ITEM | 중복 주택의 매물 |
| **IMPORT_DUP** | 기존 주택에 매물 추가 요약 |
| IMPORT_DUP_HOUSE | 추가 대상 주택별 상세 |
| IMPORT_DUP_ITEM | 추가된 매물 |
| **MERGE** | 합치기 요약 (대상 주택, house_id, 매물 수) |
| MERGE_ITEM | 합쳐진 매물 |
| **INSERT_NEW** | 새 주택 삽입 요약 |
| INSERT_NEW_HOUSE | 삽입된 주택 (house_id, 단지·동·호·평·향) |
| INSERT_NEW_ITEM | 삽입된 매물 |

## 일간 로그 창

- 메인 창 상단에 **📋 일간 로그** 버튼으로 열기
- `CalendarDatePicker`로 날짜 선택, 총 줄 수 표시
- 상단 4개 요약 카드 — 각 카드 아래 주택·매물 건수 표시
  - 파싱(파랑) / 중복 가져오기(주황) / 합치기(초록) / 새 주택(보라)
- 하단 계층형 로그 리스트
  - 요약행(굵게) → 주택행(들여쓰기+약간 투명) → 매물행(더 들여쓰기+작은 폰트)
  - 행 색상·아이콘이 작업 유형을 구분

## Changes

### `ImportLogger.cs`
- **신규 메서드**: `LogParseHouse`, `LogParseDuplicate`, `LogImportDupHouse`,
  `LogInsertNewHouse` — 개별 주택 + 매물 단위로 상세 기록
- `LogMerge` 시그니처 확장: `IEnumerable<BulkParsedItem> items` 추가 → 매물 상세 기록
- `ParseLine` — 들여쓰기(Indent) 파싱 추가
- `LogEntry` — `Indent` 프로퍼티 추가

### `BulkImportWindow.xaml.cs`
- `ParseButton_Click` → 파싱 후 `LogParseHouse` / `LogParseDuplicate` 루프
- `ImportDuplicates_Click` → 주택별 `LogImportDupHouse` 호출
- `MergeSelected_Click` → `LogMerge`에 매물 리스트 전달
- `FinalizeNew_Click` → 주택별 `LogInsertNewHouse` 호출

### `DailyLogWindow.xaml`
- 요약 카드에 주택·매물 서브 텍스트 추가
- 헤더에 총 줄 수 `TextBlock` 추가
- 리스트 행: `Padding`, `Opacity`, `FontSize`, `FontWeight` 바인딩 → 계층 시각화

### `DailyLogWindow.xaml.cs`
- 13종 action 카운팅 (summary + house + item)
- `LogEntryViewModel` — `LeftPadding`, `RowOpacity`, `DetailFontSize`, `LabelWeight` 속성 추가
- 창 크기 1100×780으로 확대

### `InfoWindow.xaml`
- 버전 `v 1.0.1` → `v 1.1.2`

### `docs/CHANGE_2026-02-26_DailyImportLog.md`
- 이 문서 전면 갱신
