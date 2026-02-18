# 변경 사항: 평수 파싱 수정 및 향 컬럼 추가

**날짜:** 2026-02-18

---

## 변경 1: 평수(area) 파싱 버그 수정

### 문제
대량 가져오기 시 비고(remark) 텍스트에 포함된 `40평대` 같은 문자열이 이미 올바르게 파싱된 평수(`29평`)를 덮어씌우는 버그가 있었음.

예시:
```
재건축29평 (전용22)고/12층남향
→ area = "29" (정상 파싱)

비고: "30.단독매물 입주도 가능. 올수리 되어 있음. 그랑자이40평대 신청가능"
→ area = "40" (오류: 비고 텍스트의 40평이 덮어씀)
```

### 원인
`BulkImportParser.cs`의 scan-ahead 루프에서 `AreaPattern`에 가드가 없어, 매 라인마다 매칭을 시도하여 비고 텍스트의 `40평대`가 마지막에 매칭됨.

### 해결
- 평수 파싱을 층수(floor) 정보가 있는 라인에서만 수행하도록 제한
- `AreaInfoLinePattern` 추가: `(\d+)(?:평|㎡)\s*\(` — 실제 정보 라인에서만 매칭 (전용 면적이 괄호 안에 있는 패턴)
- 기존 `AreaPattern`은 층수 라인에서만 fallback으로 사용

---

## 변경 2: 향(direction) 컬럼 추가

### 내용
각 house에 향(방향) 정보를 저장하는 `direction` 컬럼 추가.

### 지원 방향값
`남향`, `남서향`, `남동향`, `북서향`, `북동향`, `동향`, `서향`, `북향`

### DB 마이그레이션
- 기존 DB: `ALTER TABLE house ADD COLUMN direction TEXT` 자동 실행
- 기존 레코드: `direction = '남향'` 기본값 설정
- 새 DB: 스키마에 `direction TEXT` 포함

### 파싱
층수 정보 라인에서 방향 자동 추출:
```
아파트112A1㎡ (전용84A1)중/36층남서향  → direction = "남서향"
아파트99B㎡ (전용74B)중/33층남동향      → direction = "남동향"
재건축29평 (전용22)고/12층남향          → direction = "남향"
```

정규식: `\d+층((?:남서|남동|북서|북동|동|서|남|북)향)`

### UI
- `MainWindow.xaml` 목록에 "향" 컬럼 추가 (평 컬럼 옆)
- `AdminWindow` 편집 대화상자에 Direction 필드 추가

---

## 수정된 파일

| 파일 | 변경 내용 |
|------|-----------|
| `BulkImportParser.cs` | AreaInfoLinePattern 추가, area 파싱을 floor 라인으로 제한, DirectionPattern 추가, BulkParsedHouse.Direction 추가 |
| `Models/HouseModels.cs` | HouseEdit.Direction, HouseView.Direction 속성 추가 |
| `BooHillRepository.cs` | EnsureDirectionColumnAsync 마이그레이션 추가, CreateBlankDatabaseAsync 스키마 업데이트, 모든 SQL 쿼리에 direction 포함 |
| `MainWindow.xaml` | 헤더/데이터 템플릿에 "향" 컬럼 추가 |
| `AdminWindow.xaml.cs` | ShowHouseDialogAsync에 Direction 필드 추가, EditHouse_Click에 Direction 매핑 |
