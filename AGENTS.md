# AGENTS.md — Tap4D

Руководство для агентских сессий. Стиль кода — в `StyleGuide.md` (обязателен),
дизайн — в `Plan.md` (v1, планарный рендерер) и `PlanRadial.md` (рейкастер).

## Что это

Библиотека-рендерер 4D-сцен (.NET 10, C#, ComputeSharp 3.2). Виртуальный экран
трёхмерный: X/Y идут на реальный экран, W (ана-ката) — в цвет (красный→синий,
попиксельно по W точки попадания). Несколько точек на один пиксель — чередование
по кадрам. Зелёный канал зарезервирован под 3D-текстуры.

## Структура

- `Tap4D/` — библиотека (net10.0-windows, AllowUnsafeBlocks).
  - `Maths/` — `Rotation4` (повороты в плоскостях), `Perspective`, `CameraSpace`,
    `TetrahedronClipper`.
  - `Geometry/` — `Tetrahedron`, `Segment4` (цилиндр с мировым радиусом),
    `Tesseract` (генератор: тетраэдры + рёбра), `GeometryTransform`.
  - `Scene/` — `Scene4` (Tetrahedra видимые / Occluders заслоняющие / Segments),
    `Camera4` (Position, ориентация, Focal=2, Near=0.02), `AnaKataPalette`.
  - `Rendering/` — `IRenderer` (общий контракт: `Render(Scene4, Camera4) → byte[] BGRA`,
    `FrameIndex` инкрементируется рендерером после реального рендера, `MinW/MaxW` —
    у рендерера, не у палитры). Планарные `CpuRenderer`/`GpuRenderer` (+`RasterShader`).
  - `Rendering/Raycast/` — рейкастер: `VoxelScreen` (сферический экран, 64 среза W),
    `RayTetrahedronIntersection` (Крамер 4×4), `RaySegmentIntersection`,
    `AngularBounds` (отсев по угловым диапазонам X/Y/W), `CpuRaycastRenderer`,
    `GpuRaycastRenderer` (два прохода: `RaycastVoxelShader` → воксельные буферы
    depths/coverage, `CompositeShader` → чередование срезов).
- `Tap4D.Tests/` — xUnit; `Snapshots/RenderSnapshotTests` пишет PNG в `snapshots/`
  (в репозитории игнорируются, перегенерируются прогоном тестов).
- `Tap4D.Viewer/` — WPF: `MainWindow`, `FpsCameraController`, `RawMouseInput`
  (WM_INPUT), `CursorConfiner`, `DemoScene` (тессеракт, 3 режима).

## Ключевые инварианты (не ломать)

- **Паритет CPU/GPU**: обе версии рендерера обязаны давать одинаковую картинку;
  правки логики дублируются в шейдеры дословно. Тесты сравнивают.
- **Солвер луч–сегмент после W-анизотропии работает с НЕединичным направлением**:
  все формулы ближайшего подхода делят на `uu = dot(u,u)`. `t` при этом —
  физический параметр луча (не меняется масштабированием W).
- **Толстые лучи**: к радиусу сегмента добавляется след воксела
  `t·footprintK`, `footprintK = (pixelSize + ½·sliceWidth·wScale)/focal` — иначе
  рёбра пропадают из-за редкой дискретизации по W.
- **Цвет — по W точки попадания** (`hitW = depth·sliceW/focal`), не по W среза.
- Заслоняющие тетраэдры участвуют в глубине, но не рисуются; список кандидатов
  обрезается на первой заслонке.
- Чередование: срезы с попаданиями в порядке W, кадр N показывает (N mod count)-й.

## Сборка и проверка

```bash
dotnet build Tap4D.slnx   # решение в формате .slnx, не .sln
dotnet test Tap4D.slnx    # 79+ тестов; snapshot-тесты пишут PNG в snapshots/
```

Снапшоты можно и нужно смотреть глазами (ReadMediaFile) после правок рендерера —
пользователь это приветствует. Дымовая проверка вьювера: запустить в фоне,
прочитать заголовок окна (`Get-Process | ? MainWindowTitle -like 'Tap4D*'`),
завершить.

## Подводные камни

- ComputeSharp: локальные массивы в шейдере не поддерживаются; primary-constructor
  параметры шейдера доступны в теле напрямую (поле не нужно).
- Параллелизация тестов отключена (`AssemblyInfo.cs`) — гонка в ComputeSharp
  ConditionalWeakTable.
- WScript.Shell SendKeys на машине пользователя ненадёжен — не симулировать ввод
  так; есть честный тест `MainWindowInputTests` (HwndSource + RaiseEvent, STA).
- Оптимизация «пропуск первого прохода при неизменной сцене» удалена осознанно
  (ломала живые сцены; в реальных сценах всё движется). Не возвращать без спроса.
- Не использовать субагентов и AskUserQuestion — пользователь просил вопросы
  задавать просто в чат.
