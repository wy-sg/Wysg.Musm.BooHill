# 부유한 언덕 (Boo Hill)

부동산 정보 관리 애플리케이션 (부유한 언덕 프로젝트)

## 개요

부유한 언덕은 .NET 8과 WinUI 3를 기반으로 하는 Windows 데스크톱 애플리케이션으로, SQLite 데이터베이스를 통해 주택 및 매물 정보를 저장하고 관리합니다.

## 주요 기능

### 📋 주택 목록 관리
- **다중 필터 지원**: 동, 호수, 평수, 태그, 관심, 거래완료 여부로 필터링
- **정렬**: 동 번호, 가격 범위 등으로 정렬 가능
- **감평액 및 순위**: 주택별 감정평가액과 순위 정보 표시
- **가격 범위**: 매물별 최소/최대 가격 자동 계산

### 🏠 주택 상세 정보
- 주택 기본 정보 (동, 호수, 평수)
- 감정평가액 (실제값/추정값)
- 순위 (실제값/추정값)
- 관심 및 거래완료 상태 토글
- 태그 관리

### 💰 매물 관리
- 주택별 매물 정보 추가/수정/삭제
- 가격, 중개사, 날짜, 비고 정보 저장
- 오늘 추가된 매물 자동 표시

### 📥 대량 가져오기
- 텍스트 형식으로 여러 주택 및 매물 정보 일괄 입력
- 중복 검사 및 자동 병합
- 새로운 주택 자동 생성

### 🎯 클러스터 관리
- 주택을 클러스터(단지)별로 그룹화
- 클러스터별 필터링 지원

## 시스템 요구사항

- **운영 체제**: Windows 10 (버전 1809, 빌드 17763) 이상
- **프레임워크**: .NET 8.0
- **아키텍처**: x86, x64, ARM64

## 설치 및 실행

### 사전 요구사항
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 이상 (Windows App SDK 워크로드 포함)

### 빌드 방법

```powershell
# 저장소 복제
git clone https://github.com/wy-sg/Wysg.Musm.BooHill.git
cd Wysg.Musm.BooHill

# 복원 및 빌드
dotnet restore
dotnet build

# 실행
dotnet run --project Wysg.Musm.BooHill
```

### Visual Studio에서 실행
1. `Wysg.Musm.BooHill.sln` 솔루션 파일 열기
2. 빌드 구성을 Debug 또는 Release로 선택
3. 플랫폼 선택 (x64 권장)
4. F5를 눌러 디버그 모드로 실행

## 데이터베이스

애플리케이션은 로컬 SQLite 데이터베이스를 사용합니다:
- **위치**: `%LocalAppData%\Packages\<PackageId>\LocalState\realestate.sqlite`
- **초기화**: 
  - 기존 레거시 데이터베이스가 있으면 자동 복사
  - 없으면 빈 데이터베이스 자동 생성

### 데이터베이스 스키마

#### `cluster` 테이블
- 주택 클러스터(단지) 정보

#### `house` 테이블
- 주택 기본 정보 (동, 호수, 평수)
- 감정평가액 및 순위
- 관심/거래완료 플래그
- 태그

#### `item` 테이블
- 주택별 매물 정보
- 가격, 중개사, 업데이트 날짜

## 프로젝트 구조

```
Wysg.Musm.BooHill/
├── Models/
│   ├── FilterOptions.cs      # 필터 옵션 모델
│   └── HouseModels.cs         # 주택 및 매물 모델
├── BooHillRepository.cs       # 데이터베이스 액세스 계층
├── BooHillParsing.cs          # 텍스트 파싱 유틸리티
├── MainWindow.xaml/cs         # 메인 창
├── AdminWindow.xaml/cs        # 관리 창
├── BulkImportWindow.xaml/cs   # 대량 가져오기 창
├── Converters.cs              # UI 값 변환기
└── docs/                      # 변경 로그 및 문서
```

## 사용 방법

### 주택 검색 및 필터링
1. 상단 필터 섹션에서 원하는 조건 선택
   - 동/호수/평수: 다중 선택 가능
   - 태그: 관심 주제별 태그로 검색
   - 관심: 관심 항목만 표시
   - 거래: 미완료 거래만 표시
2. 감평액 또는 순위 범위 입력
3. 자동으로 목록이 업데이트됨

### 주택 정보 수정
1. 목록에서 주택 선택
2. 우클릭 또는 더블클릭하여 상세 편집 창 열기
3. 정보 수정 후 저장

### 매물 추가
1. 주택 선택
2. "매물 추가" 버튼 클릭
3. 가격, 중개사, 비고 입력

### 대량 가져오기
1. "대량 가져오기" 메뉴 선택
2. 정해진 형식으로 텍스트 입력
3. "가져오기" 버튼 클릭

## 기술 스택

- **.NET 8.0**: 최신 .NET 플랫폼
- **WinUI 3**: 모던 Windows UI 프레임워크
- **Microsoft.Data.Sqlite**: SQLite 데이터베이스 액세스
- **MVVM 패턴**: 데이터 바인딩 및 UI 분리

## 최근 변경사항

자세한 변경 내역은 `docs/` 폴더의 변경 로그를 참조하세요:
- [2026-02-15: No-DB Fallback](docs/CHANGE_2026-02-15_NoDbFallback.md)
- [2026-02-14: Tags & Multi-Select Filters](docs/CHANGE_2026-02-14_TagsAndMultiSelectFilters.md)

## 라이선스

이 프로젝트는 MIT 라이선스 하에 배포됩니다. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.

MIT License

Copyright (c) 2026 Wysg.Musm.BooHill Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## 지원 및 문의

이슈 및 개선 제안은 [GitHub Issues](https://github.com/wy-sg/Wysg.Musm.BooHill/issues)를 통해 제출해 주세요.