# MyPresentation

Unity 角色扮演式演讲训练项目。运行时从 VoiceAnalyzer 的 UDP `5005` 端口接收分析结果，完成个人校准、逐句任务、NPC 正向反馈与最终四维评价。

## 训练流程

1. 启动 VoiceAnalyzer，再运行 Unity 场景。
2. 按屏幕提示朗读校准文本，完成后按 Enter。
3. 逐句朗读台词；系统检测到发声后开始计时，读完按 Enter 结算。
4. 语气、速度、音量和目标 NPC 视线各占 25%。无效或过短的分析会要求重试，不会记为零分。
5. 全部台词结束后显示综合评价；运行日志写入 `Application.persistentDataPath/Sessions`。

评分阈值和研究版本信息集中在 `Assets/Resources/Scoring/DefaultScoringProfile.asset`。日志默认不保存音频或 ASR 文本。
