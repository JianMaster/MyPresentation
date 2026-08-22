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
