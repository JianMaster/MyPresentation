# MyPresentation

Unity 角色扮演式演讲训练项目。运行时从 VoiceAnalyzer 的 UDP `5005` 端口接收机器学习分析结果，完成逐句任务、NPC 正向反馈与最终四维评价。

## 训练流程

1. 启动 VoiceAnalyzer，按 Enter 后开始实时分析；新入口不再进行个人语音校准。
2. 运行 Unity 场景。Unity 收到首个 UDP 包后显示第一句台词。
3. 逐句朗读台词；系统根据 `speech_detected` 开始计时，读完按 Enter 结算。
4. 音声表达、速度、音量和目标 NPC 视线各占 25%。无效或过短的分析会要求重试，不会记为零分。
5. 全部台词结束后显示综合评价；运行日志写入 `Application.persistentDataPath/Sessions`。

UDP 使用根对象，不带 `enabled/analyzers` 外层封装。当前协议为：

```json
{
  "timestamp": 1770000000.0,
  "sequence_id": 1,
  "speech_detected": true,
  "feature_window_seconds": 2,
  "arousal": 0.42,
  "valence": -0.18,
  "speech_rate_value": 3.7,
  "speech_rate_level": "medium",
  "volume_value": 0.32,
  "volume_level": "medium"
}
```

没有检测到语音时，`arousal` 和 `valence` 可以为 `null`；Unity只把这种帧用于识别停顿，不会作为零分样本。

评分阈值和研究版本信息集中在 `Assets/Resources/Scoring/DefaultScoringProfile.asset`。音声表达使用归一化到 `[-1, 1]` 的 Arousal 与 Valence：高/低 Arousal 表示声音活跃度，高/低 Valence 表示声音被感知为偏积极或偏消极。四种目标因此是“落ち着き/活力 × 前向き/ネガティブ”；原 Dominance 维度及其全部依赖已移除。

话速不再由Unity用台词字数和时间计算，而是直接使用Voice提供的 openSMILE `loudnessPeaksPerSec`；音量使用 `loudness_sma3_amean`。两者的中等级边界与Voice当前训练配置一致：话速 `3.279–4.054`，音量 `0.253–0.381`。日志保存这四类分析值与必要元数据，不保存音频或ASR文本。

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

- `AnalysisUdpReceiver`：接收并校验 Arousal、Valence、语速和音量数据，不决定训练状态。
- `PresentationTaskController`：唯一的流程控制器，处理状态、输入、逐句结算和会话结束。
- `AudienceFeedbackController`：选择目标听众并判断点头、视线和鼓掌反馈。
- `AudienceView`：执行角色、视线射线和描边等场景表现。
- `PerformanceEvaluator`：只计算分数，不访问场景或 UI。
- `UIManager`：只显示主控制器提供的文字。
- `SessionLogWriter`：只写入研究日志。

场景中的 `UIRoot` 明确挂载 `PresentationTaskController`，其依赖全部通过 Inspector 引用，不使用运行时自动挂载或全场景查找。
