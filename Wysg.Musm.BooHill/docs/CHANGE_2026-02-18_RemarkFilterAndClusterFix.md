# 비고 텍스트 필터 추가 및 단지 필터 수정

**Date:** 2026-02-18

## Summary

1. 메인 창 필터에 **비고** 텍스트 검색 필터를 추가했습니다.
2. 새로 생성된 DB에서 **단지** 필터가 작동하지 않는 문제를 수정했습니다.

## 변경 1: 비고 텍스트 필터

### 문제
매물의 비고(remark)에 포함된 키워드(예: "급매")로 검색할 수 없었습니다.

### 해결
- 필터 UI에 "비고" `TextBox`를 추가했습니다.
- 텍스트를 입력하고 적용하면 해당 텍스트가 비고에 포함된 `item`이 하나라도
  있는 `house`만 표시됩니다.
- SQL에서 `EXISTS` 서브쿼리와 `LIKE` 연산자로 부분 문자열 일치를 수행합니다.

## 변경 2: 단지 필터 수정

### 문제
새로 생성된 DB(빈 DB)에서 한꺼번에 가져오기로 매물을 추가하면 `house` 테이블에
`cluster_id`가 설정되지만 `cluster` 테이블에 해당 레코드가 없어 단지 콤보박스에
항목이 표시되지 않았습니다.

### 해결
`EnsureClustersFromHousesAsync` 메서드를 추가하여 앱 초기화 시 `house` 테이블의
`cluster_id` 중 `cluster` 테이블에 없는 값을 자동으로 생성합니다.

## Changes

### `Models/FilterOptions.cs`
- `RemarkText` 속성 추가

### `MainWindow.xaml`
- 필터 그리드 10번 열에 `RemarkTextBox` 추가 (레이블: "비고", 플레이스홀더: "검색어")

### `MainWindow.xaml.cs`
- `_remarkTextBox` 필드, `WireUpControls`, `ReadFiltersFromUi`, `ClearFilters_Click` 연동

### `BooHillRepository.cs`
- `GetHousesAsync`: `RemarkText` 필터 시 `EXISTS (SELECT 1 FROM item ... remark LIKE ...)` WHERE 절 추가
- `EnsureClustersFromHousesAsync`: `house`에 존재하지만 `cluster`에 없는 `cluster_id` 레코드를 `INSERT OR IGNORE`
- `CreateAsync`: `EnsureClustersFromHousesAsync` 호출 추가
