
=== Act 1 Story Balancer - One Sheet Setup (Instructions at top)
===

=== HOW TO USE (НАЧАЛО)
1) Открой этот файл в Google Sheets или Excel. (Если Excel - это обычный CSV с формулами, он поймет. Если Google Sheets: File / Import / Upload, Import as new sheet(s).)
2) Лист называется MAIN. Вся работа - только на ячейки с желтым/зеленым фоном.
3) Шаг 1. Нажми "Apply Step" не надо - у нас нет макросов. Вместо этого заполни желтые ячейки (дропдауны), а зеленые (формулы) сами посчитают следующее состояние.
4) Шаг 2. Каждый следующий шаг - просто скопируй строку (или диапазон) следующего состояния из зеленых ячеек в следующую строку "Step N state" - и повтори.
5) Внизу файла есть справочники "ARCHETYPES_LUT", "NODES_LUT" и "CHOICES_LUT" - ты их можешь править прямо там, и значения автоматически подтянутся вверх.
6) Если что-то пошло не так - не бойся, все пересчитывается формулами. Просто начни заново с Step 0.

=== MAIN SHEET LAYOUT (single column block, 1 run path)
Row 1: Title
Row 3: STEP 0 (start state)
   C3: Archetype (dropdown)
   E3: CharlotteTrust (auto)
   G3: PatrickPressure (auto)
   I3: NickStress (auto)
   K3: NickWarmth (auto)
   M3: Money (auto)
   O3: S1_BlefUsed (auto 0/1)
   Q3: S2_PaidPatrick (auto)
   S3: S2_ExtraWorkRequired (auto)
   U3: S2_PatrickAnswerStyle (auto)
   W3: S3_BreakingPointAnswer (auto)
   Y3: S3_CharlotteKnows (auto)
   AA3: S3_FinalVariant (auto)
   AC3: (label) Next Node (from LUT), dropdown
   AE3: (label) Current Choice, dropdown
   AG3: (label) Validation Msg (auto)
   AI3: (label) Next State preview (auto green)

Row 5: STEP 1 (apply Step 0 output to Step 1 input)
... и т.д.

=== NOTES (CSV is comma-separated, quotes around fields with commas. Вся структура MAIN + LUTs будут в одном CSV, но в одном листе, разделённые пустыми строками и помечены блоками MAIN, LUT_ARCHETYPES, LUT_NODES, LUT_CHOICES.
