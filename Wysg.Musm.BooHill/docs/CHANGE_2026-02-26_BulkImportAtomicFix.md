# 한꺼번에 가져오기 매물 누락 버그 수정 (v 1.1.3)

**Date:** 2026-02-26

## Summary

한꺼번에 가져오기(Bulk Import)에서 새 주택을 삽입할 때, 주택만 DB에 들어가고
매물이 누락되는 버그를 수정했습니다. 로그 생성 시점을 파싱 단계에서
실제 가져오기 단계(합치기 / 기존 매물 가져오기 / 새 매물 가져오기)로 변경했습니다.

버전을 `v 1.1.2` → `v 1.1.3`으로 올렸습니다.

## 원인 분석

두 가지 문제가 동시에 존재했습니다.

### 1. 스키마 결함 (근본 원인)

레거시 시드 DB의 `item` 테이블에 `UNIQUE(price, office, last_updated_date,
added_date, remark)` 제약조건이 걸려 있으나 **`house_id`가 빠져 있었습니다**.
서로 다른 주택에 동일한 매물 값(가격, 부동산, 날짜, 비고)이 존재하면
INSERT가 제약조건에 위배됩니다.

올바른 제약조건: `UNIQUE(house_id, price, office, last_updated_date, added_date, remark)`

### 2. 코드 비일관성

다른 모든 INSERT 메서드(`AddItemsAsync`, `ImportItemsAsync`,
`ImportHouseBatchAsync`, `UpsertItemAsync`)는 `INSERT OR IGNORE`를 사용하지만,
이전 커밋에서 `InsertHouseWithItemsAsync`만 plain `INSERT`로 변경해
제약조건 위반 시 크래시가 발생했습니다.

### 3. 트랜잭션 분리

기존 `InsertHouseWithItemsAsync`는 주택 삽입(`UpsertHouseAsync`)과 매물 삽입
(`AddItemsAsync`)을 **별도 연결·별도 트랜잭션**으로 실행했습니다.
매물 삽입이 실패해도 주택은 이미 커밋된 상태: **주택만 있고 매물이 없는 상태**.

## 수정 내용

### `BooHillRepository.cs`
- **`EnsureItemUniqueConstraintAsync`** — 신규 마이그레이션. 레거시 DB의
  잘못된 UNIQUE 제약조건(`house_id` 미포함)을 감지하고, 테이블 재생성을 통해
  `UNIQUE(house_id, price, office, last_updated_date, added_date, remark)`으로
  수정합니다.
- **`CreateBlankDatabaseAsync`** — 빈 DB 스키마에도 올바른 UNIQUE 제약조건 추가
- **`InsertHouseWithItemsAsync`** — 단일 연결 + 단일 트랜잭션으로 주택·매물을
  함께 삽입 (원자성 보장). `INSERT OR IGNORE`로 복원하여 다른 메서드와 일관성 유지.
  반환값을 `Task<(long HouseId, int ItemsAdded)>`로 변경

### `BulkImportWindow.xaml.cs`
- `ParseButton_Click` — 파싱 단계의 로그 기록 제거 (`LogParse`, `LogParseHouse`,
  `LogParseDuplicate` 호출 삭제)
- `FinalizeNew_Click` — `InsertHouseWithItemsAsync`의 튜플 반환값으로 실제
  매물 삽입 수를 사용, `LogInsertNewHouse`에 실제 매물 수 전달

### `ImportLogger.cs`
- `LogInsertNewHouse` — `int itemsAdded` 매개변수 추가. 로그에 실제/파싱
  매물 수 비율(`매물 X/Y개`)을 기록하여 진단 가능

### `InfoWindow.xaml`
- 버전 `v 1.1.2` → `v 1.1.3`

### `docs/CHANGE_2026-02-26_BulkImportAtomicFix.md`
- 이 문서 갱신
