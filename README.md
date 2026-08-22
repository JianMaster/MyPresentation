# MyPresentation

Unity 角色扮演式演讲训练项目。运行时从 VoiceAnalyzer 的 UDP `5005` 端口接收 delivery 根对象，完成逐句任务、NPC 正向反馈与最终四维评价。

## 训练流程

1. 启动 VoiceAnalyzer，并在 Voice 控制台完成语气/音量个人基准校准。
2. 运行 Unity 场景。Unity 不处理也不显示校准；收到 Voice 校准后的首个 UDP 包即显示第一句台词。
3. 逐句朗读台词；系统根据 `speech_detected` 开始计时，读完按 Enter 结算。
4. 语气、速度、音量和目标 NPC 视线各占 25%。无效或过短的分析会要求重试，不会记为零分。
5. 全部台词结束后显示综合评价；运行日志写入 `Application.persistentDataPath/Sessions`。

UDP 不再使用 `enabled/analyzers` 外层封装。根对象包含 delivery 九个字段，以及逐句流程必需的 `timestamp`、`sequence_id`、`speech_detected` 和 `feature_window_seconds`。

评分阈值和研究版本信息集中在 `Assets/Resources/Scoring/DefaultScoringProfile.asset`。Unity 不再朗读校准文本；话速以冻结的 `300 CPM` 为 normal 基准，slow/fast 仍使用配置中的比例。日志只保存 delivery 与必要元数据，不保存音频或 ASR 文本。

## Unity 控制流

运行时只有 `PresentationTaskController` 可以推进训练状态：

```text
等待 Voice 数据
  → 显示当前台词并等待发声
  → 检测到发声后记录本句
  → Enter 结算
      ├─ 数据无效：重试当前句
      ├─ 还有台词：进入下一句
      └─ 全部完成：生成总结报告
```

各组件职责保持单一：

- `AnalysisUdpReceiver`：接收并校验 UDP 数据，不决定训练状态。
- `PresentationTaskController`：唯一的流程控制器，处理状态、输入、逐句结算和会话结束。
- `AudienceFeedbackController`：选择目标听众并判断点头、视线和鼓掌反馈。
- `AudienceView`：执行角色、视线射线和描边等场景表现。
- `PerformanceEvaluator`：只计算分数，不访问场景或 UI。
- `UIManager`：只显示主控制器提供的文字。
- `SessionLogWriter`：只写入研究日志。

场景中的 `UIRoot` 明确挂载 `PresentationTaskController`，其依赖全部通过 Inspector 引用，不使用运行时自动挂载或全场景查找。
