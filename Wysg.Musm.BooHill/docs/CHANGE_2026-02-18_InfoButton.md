# 앱 정보 버튼 및 창 추가

**Date:** 2026-02-18

## Summary

메인 창 상단에 **ℹ 정보** 버튼을 추가했습니다. 클릭하면 앱 정보를 표시하는
작은 창이 열립니다.

## 표시 내용

- 앱 이름: 부유한 언덕-아파트
- 버전: v 1.01
- 개발자: wysg
- GitHub 링크: https://github.com/wy-sg
- YouTube 링크: https://www.youtube.com/@wy-sg
- 라이선스: MIT (한국어 요약 포함)

## Changes

### `InfoWindow.xaml`
- 새 창 XAML: `ScrollViewer` + `StackPanel` 레이아웃으로 앱 정보 표시
- `HyperlinkButton`으로 GitHub, YouTube 링크 클릭 가능

### `InfoWindow.xaml.cs`
- `InitializeComponent()` 호출 및 창 크기 460×560 고정

### `MainWindow.xaml`
- 제목 행에 `<Button Content="ℹ 정보" Click="OpenInfo_Click" />` 추가

### `MainWindow.xaml.cs`
- `OpenInfo_Click` 핸들러 추가: `InfoWindow` 인스턴스를 생성하고 `Activate()`

### `Wysg.Musm.BooHill.csproj`
- `<Page Remove="InfoWindow.xaml" />` 항목 제거 (BulkImportWindow와 동일한
  `None Update + Generator=MSBuild:Compile` 패턴 사용)
