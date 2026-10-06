# Presentation

Unity 6000.3.7f1 演讲训练项目。`Main` 是唯一流程入口。

## 使用

1. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play Mode。
2. 按 **Enter**（主键盘或小键盘）开始训练。
3. VoiceAD 向 `127.0.0.1:5005` 发送分析结果；按台词朗读并看向描边的 NPC。
4. 收到本次发声的结束包后立即评分、换句，不等待 NPC 注视结束。10 句结束显示四维报告。训练中 Enter 不跳句，结束后停留在报告页；再次训练需重新进入 Play Mode。

VoiceAD 的静音结束检测决定换句时机；本项目不检查朗读内容是否完整，不包含 ASR。未收到结束包时保持当前台词，超过 Main 的等待阈值只显示提示。

## 代码和配置

所有运行代码位于 `Assets/Presentation/Scripts`：

| 模块 | 职责 |
| --- | --- |
| `Main.cs` | Enter 开始、视线检测、逐句结算和结束 |
| `Data` | 台词、目标及重音/停顿格式 |
| `Voice` | 非阻塞 UDP 接收和输入校验 |
| `Scoring` | 分段去重、时长加权、评分及一条建议 |
| `Audience` | NPC 默认看向 Screen、独立计时恢复、描边、平滑转头、点头和鼓掌 |
| `UI` | 台词、状态和报告 |
| `Player` | WASD 和鼠标视角 |
| `Logging` | 逐句 JSONL 日志 |
| `Rendering` | 沿用原项目的轮廓渲染 |

- 台词：`Assets/Presentation/Data/Speech.asset`，保留原 10 句。
- 评分：`Assets/Presentation/Data/DefaultScoringProfile.asset`，训练前调整权重、区间及鼓掌阈值。默认表达、语速、音量、视线各占 25%。
- 日志：`Application.persistentDataPath/Sessions/*.jsonl`，记录参数、每句结果及完成/中断状态；不保存音频。写入失败会在界面提示。
- 场景通过 Inspector 引用连接 Main、TrainingView、摄像机和 6 个 AudienceRole。描边使用 PC_Renderer、RoleOutline 图层 6 及现有两个 Shader，仅保留当前 Unity 6 的 RenderGraph 路径。

## 视线事件

- 每句只计算表达、语速、音量；视线仅在触发事件后计分，每次事件最多计一次。视线分数为成功次数 / 实际触发次数 × 100，训练结束时合并总分；未触发事件时不参与总分。
- NPC 的 `Default Target` 绑定场景中的 Screen，开始前及事件结束后均看向 Screen，无描边。
- Main 的 `Gaze Event Count` 是次数上限：`-1` 使用总台词数除以 3（向下取整，当前上限 3 次），`0` 关闭，正数指定上限。
- 每句开头先检查是否还有次数，有则做一次随机判断（50% 触发）。不保证用满次数，也不为补足次数强制触发。NPC 目标仍按台词指定索引或原来的轮流顺序选取。
- Main 触发时只调用 `LookAt`。NPC 的 `Look Duration` 默认为 **5 秒**，由 AudienceRole 自己计时，到时恢复 Screen 和原图层。换句、注视命中、Main 停用均不提前取消这个计时。
- 视线计分独立于当前台词，换句后仍可回应尚未结束的 NPC 注视。点头和 NPC 自行恢复保持不变，UDP 结束包仍立即换句，训练结束立即结算。

## UDP

发送 UTF-8 JSON，所需字段如下：

```json
{
  "A": 0.4,
  "D": 0.3,
  "speech_rate": 3.5,
  "loudness": 0.45,
  "utterance_started_at": 1790643600.25,
  "segment_index": 1,
  "segment_seconds": 1.2,
  "speech_ended": true
}
```

`utterance_started_at` 使用本机当前 Unix 秒数，同一次发声保持不变；示例时间需替换。A/D 范围为 [-1,1]；语速是响度峰/秒，响度不是分贝。V 和等级字段可随 VoiceAD 原协议发送，但不参与评分。

中间片段使用 `speech_ended: false`，只在发声结束时发送 true。按片段时长加权，重复编号只计一次，重复编号的结束标记仍有效。旧发声和重复结束包不会推进下一句。新发声替换未完成的旧发声；UDP 不重传或恢复乱序，下一句显示后再朗读。

## 验证

- 逻辑检查：项目根目录执行 `& ./Tools/Verification/Verify.ps1`，复用本机 Unity 程序集；需要现有 `Assembly-CSharp.csproj`，临时产物写入 `Library/PresentationVerification`。
- 完整训练：保存场景、停止 Play Mode 后，选择菜单 **Presentation > Verify UDP Training**。先检查描边材质重复创建和释放，再通过 Input System 注入 Enter、发送真实回环 UDP，并用实际摄像机射线验证视线和点头。运行 4 轮，每轮 10 句，覆盖默认上限、自定义上限、连续随机未命中不补次数和关闭事件；另验证 Main 停用后 NPC 仍会独立计时恢复 Screen。测试配置只在 Play Mode 中修改，结束后恢复场景配置。
- 验证结果位于 `ValidationResults/UDP/result.json`，各轮模拟训练日志为 `ValidationResults/UDP/session*.jsonl`。验证代码仅在编辑器运行，不进入玩家构建。模拟目标匹配数据不代表真人语音识别准确率。

替换前项目已备份至 `D:\MyProjects\Presentation_Backup_20260929_210323`，包含原代码、资源、设置、Git 和 Rewrite；`backup-manifest.csv` 提供文件哈希。
