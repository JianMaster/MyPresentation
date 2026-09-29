# Presentation

Unity 角色扮演式演讲训练项目，使用 VoiceAD 的 UDP 分段结果自动完成逐句训练。

## 使用流程

1. 在 VoiceAD 目录运行 `run.ps1`。`config.py` 中 UDP 默认开启，目标为 `127.0.0.1:5005`。
2. 运行 Unity 的 `Assets/Scenes/SampleScene.unity`，立即显示第一句台词。
3. 按提示朗读并看向目标 NPC。无需按 Enter；VoiceAD 检测到发声后的连续静音后，Unity 自动结算并显示下一句。
4. 全部台词结束后显示四维报告，日志写入 `Application.persistentDataPath/Sessions`。

VoiceAD 默认在连续静音约 0.45 秒时结束一次发声。因此句中较长停顿也会结算当前台词；需要更长停顿时，在 VoiceAD 中调整 `END_SILENCE_SECONDS`。这属于声音结束检测，不检查台词是否完整朗读，不包含 ASR。

连续讲话达到约 3 秒时，VoiceAD 只提交中间片段，Unity 继续停留在当前台词，直到收到 `speech_ended: true`。最终结果在对应片段完成推理后发送，结算会包含尾段。

## 情绪与评分

- 仅用 Arousal / Dominance（A/D，范围 `[-1,1]`）评分。VoiceAD 仍输出 V，但 Unity 不读取、不记录、不评分。
- A 表示活跃程度，D 表示支配感。训练提示将高/低 D 简化表达为“自信/犹豫”，它是声学维度估计，不是人格或实际能力判断。
- 四种目标：平静自信、活跃自信、活跃犹豫、平静犹豫。原台词枚举数值顺序保持不变，现有正向台词改为自信目标。
- 表达、语速、音量、视线各占 25%。A/D 按目标方向评分，语速/音量按区间及偏离程度评分。
- 默认中速区间为 `2.0–5.0` 响度峰/秒，响度为 `0.3–0.6`，与 VoiceAD 默认配置一致；不是字数/秒或分贝。修改 VoiceAD 的区间时，也需更新 Unity 的评分资源。
- 一次发声内按各片段的分析时长加权汇总。取消固定窗口预热、最低 4 秒限制和旧样本超时评分规则，语音有效性由 VoiceAD VAD 把关。
- 视线从台词显示开始检测；不等待延迟到达的分析结果。语音连续匹配时 NPC 点头，会话高分时鼓掌。

配置在 `Assets/Resources/Scoring/DefaultScoringProfile.asset`；台词在 `Assets/Resources/Texts/Texts.asset`。

## UDP 协议

UTF-8 JSON 根对象：

```json
{
  "A": 0.4,
  "D": 0.3,
  "V": -0.1,
  "speech_rate": 3.5,
  "loudness": 0.45,
  "speech_rate_level": 0,
  "loudness_level": 0,
  "utterance_started_at": 1790643600.25,
  "segment_index": 1,
  "segment_seconds": 1.2,
  "speech_ended": true
}
```

- `utterance_started_at`：同一次发声固定不变的 Unix 秒数，使用本机时钟；用于分组并过滤台词显示前开始的旧发声。
- `segment_index`：本次发声内从 1 开始的片段编号；队列过载时可能不连续。
- `segment_seconds`：这一片段送入模型的音频时长，含保留的静音；不是纯发声时长。
- `speech_ended`：只有连续静音或文件 EOF 才为 true；最大长度切片为 false。
- 两个等级字段为整数 `-1/0/1`。

恰在最大切片处停止讲话时，结束包可能重复最后一个片段编号。Unity 只统计一次该片段，但仍处理结束标记。UDP 同一帧到达的多个包按接收顺序处理，上一句的重复或延迟包不会推进下一句。

VoiceAD 继续使用有界队列，过载或 UDP 丢包可能遗漏片段。若未收到结束包，Unity 保持当前台词；下一次发声会替换未完成的样本，不把两次发声混合评分。等待下一句显示后再开始朗读，避免下一次发声被当作显示前的旧数据。

## 代码职责

- `AnalysisUdpReceiver`：接收和校验 VoiceAD JSON，主线程按序分发。
- `PresentationTaskController`：唯一流程控制器，收集片段、结束时结算和推进。
- `PerformanceEvaluator`：A/D、语速、响度和视线评分。
- `AudienceFeedbackController` / `AudienceView`：NPC 反馈及视线检测。
- `UIManager`：台词、状态和报告显示。
- `SessionLogWriter`：保存 A/D、声学值、分段元数据和评分，不保存音频或转写文本。

场景依赖沿用 Inspector 引用，不自动挂载组件或搜索全场景。已移除旧分析端协议、V 评分以及 Enter 确认流程。

## 验证

`Assets/Tests/Editor/PerformanceEvaluatorTests.cs` 覆盖 A/D 评分、短句、加权去重、协议校验和自动推进，包括最大长度切片、重复结束包、旧数据及会话完成。
