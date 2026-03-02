# 비고 없는 매물 자동 합치기 (v 1.0.2)

**Date:** 2026-03-01

## Summary

한꺼번에 가져오기(Bulk Import)에서 비고(remark)가 없는 매물도
동일한 동·호수·평의 기존 매물과 자동으로 합칠 수 있도록 변경했습니다.

기존에는 `price|office|remark` 키가 정확히 일치해야만 기존 매물로 인식했기
때문에, 비고가 비어 있는 매물은 같은 부동산·같은 가격의 기존 매물이 있어도
중복으로 분류되지 않았습니다.

버전을 `v 1.0.1` → `v 1.0.2`로 올렸습니다.

## Changes

### `BulkImportWindow.xaml.cs`

- **`HasSharedItem`**: 파싱된 매물의 비고가 비어 있으면 `price|office`만으로
  기존 매물과 비교하는 보조 키(remarkless key) 매칭을 추가.
- **`RemarklessItemKey(BulkParsedItem)`** / **`RemarklessItemKey(ItemRecord)`**:
  비고를 제외한 `price|office` 키를 생성하는 헬퍼 메서드 추가.

### `Package.appxmanifest`

- `Version` 1.0.1.0 → 1.0.2.0

## Files changed

| File | Change |
|------|--------|
| `BulkImportWindow.xaml.cs` | remarkless 매칭 로직 추가 |
| `Package.appxmanifest` | 빌드 번호 증가 |
| `docs/CHANGE_2026-03-01_RemarklessMerge.md` | 이 문서 |
