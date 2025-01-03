using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Runtime.Intrinsics.Arm;
using System.Formats.Asn1;

namespace UsamisScript.EndWalker.RubicanteEx;

[ScriptType(name: "Rubicante-Ex [卢比坎特歼殛战]", territorys: [1096], guid: "a5f70ab7-b79a-468c-9ffe-3c7e5091d71d", version: "0.0.0.2", author: "Usami", note: noteStr)]

public class RubicanteEx
{
    const string noteStr =
    """
    v0.0.0.2:
    1. 我忘了，反正改了点什么东西。

    v0.0.0.1:
    很遗憾，大转盘必须Imgui。
    鸭门。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    
    const uint CLOCKWISE = 0x00020001;    // EnvControl State 代表顺时针旋转
    const uint COUNTER = 0x00200010;        // EnvControl State 代表逆时针旋转
    const uint INNER = 0x00000001;    // EnvControl Index 代表内圈魔法阵
    const uint MIDDLE = 0x00000002;    // EnvControl Index 代表中圈魔法阵
    const uint OUTER = 0x00000003;    // EnvControl Index 代表外圈魔法阵
    const uint HALF_CLEAVE = 15759; // 外围魔法阵红色半场刀 SourceDataId
    const uint FAN = 15760;     // 外围魔法阵蓝色扇形 SourceDataId
    const uint SINGLE_LINE = 542;   // EnvControl Param 内圈魔法阵是单线
    const uint V_SHAPE = 543;       // EnvControl Param 内圈魔法阵是V字
    const uint DOUBLE_LINE = 544;   // EnvControl Param 内圈魔法阵是双线
    const uint NEKO = 545;       // EnvControl Param 中圈魔法阵是猫猫
    const uint EIGHTPOS = 546;   // EnvControl Param 外圈魔法阵是八方

    List<IBattleChara?> InnerCircleId = [null, null, null];
    List<bool> isCaptured = [false, false, false];
    List<int> InnCircleFaceDir = [0, 0, 0];  // 内魔法阵面向
    List<uint> InnCircleType = [0, 0, 0];   // 内圈魔法阵类型
    List<int> RotateCircleDir = [0, 0, 0];  // 大转盘旋转方向，-1逆时针，+1顺时针，0不转/未知方向
    List<bool> OuterFanMagic = [false, false, false, false, false, false, false, false];   // 外圈魔法阵分布，是扇形为True，是半场刀为False
    static List<int> NekoOutputAtDir0 = [0, 1, -1, 2, 4, 6, -1, 7];    // 当猫猫Dir为0时，输入与输出的响应
    List<int> OuterTargetMagicDir = [-1, -1];  // 大转盘目标
    List<uint> IntercardFlags = [0x02000200, 0x00200020, 0x00020002, 0x00800080];   // 先斜角安全State标记
    public void Init(ScriptAccessory accessory)
    {
        InnerCircleId = [null, null, null];
        isCaptured = [false, false, false];

        InnCircleFaceDir = [0, 0, 0];       // 内魔法阵面向
        InnCircleType = [0, 0, 0];          // 内魔法阵类型

        RotateCircleDir = [0, 0, 0];        // 大转盘旋转方向，-1逆时针，+1顺时针，0不转
        OuterFanMagic = [false, false, false, false, false, false, false, false];   // 外圈魔法阵分布，是扇形为True，是半场刀为False
        NekoOutputAtDir0 = [0, 1, -1, 2, 4, 6, -1, 7];    // 当猫猫Dir为0时，输入与输出的响应
        OuterTargetMagicDir = [-1, -1];  // 大转盘目标

        IntercardFlags = [0x02000200, 0x00200020, 0x00020002, 0x00800080];   // 先斜角安全State标记

        // DebugMsg($"Init Rubicante-Ex Success.", accessory);

        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    #region 大转盘
    [ScriptMethod(name: "炼狱魔阵外圈类型记录（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:regex:^(157(59|60))$"], userControl: false)]
    public void MagicCircleOuterTypeRecord(Event @event, ScriptAccessory accessory)
    {
        // 15760 红 半场刀
        // 15759 蓝 扇形
        var srot = @event.SourceRotation();
        var sdid = @event.SourceDataId();
        var sdir = DirectionCalc.DirRadRoundToDirs(DirectionCalc.BaseInnGame2DirRad(srot), 8);
        OuterFanMagic[sdir] = sdid == FAN ? false : true;
    }

    [ScriptMethod(name: "炼狱魔阵内圈旋转方向记录（不可控）", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:regex:^(0000000[123])$"], userControl: false)]
    public void MagicCircleRotationRecord(Event @event, ScriptAccessory accessory)
    {
        var idx = @event.Index();
        var rotate_dir = @event.State();
        int mcidx;
        int mcdir;
        string log = "";

        // 定义 idx 和 rotate_dir 的映射关系
        var idxMapping = new Dictionary<uint, (int _mcidx, string _log)>
        {
            { INNER, (0, "内环转动，") },
            { MIDDLE, (1, "中环转动，") },
            { OUTER, (2, "外环转动，") }
        };

        var dirMapping = new Dictionary<uint, (int _mcdir, string _log)>
        {
            { COUNTER, (-1, "逆时针") },
            { CLOCKWISE, (1, "顺时针") }
        };

        if (idxMapping.ContainsKey(idx))
        {
            var (_mcidx, _log) = idxMapping[idx];
            log = log + _log;
            mcidx = _mcidx;
        }
        else
        {
            log = log + "未知环转动，";
            mcidx = 0;
        }

        if (dirMapping.ContainsKey(rotate_dir))
        {
            var (_mcdir, _log) = dirMapping[rotate_dir];
            log = log + _log;
            mcdir = _mcdir;
        }
        else
        {
            log = log + "未知方向";
            mcdir = 0;
        }
        RotateCircleDir[mcidx] = mcdir;
        DebugMsg(log, accessory);
    }

    [ScriptMethod(name: "炼狱魔阵内圈类型与ID记录（不可控）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(54[23456])$"], userControl: false)]
    public void MagicCircleTypeRecord(Event @event, ScriptAccessory accessory)
    {
        // 内为单线时，其面向与单线一致
        // 内为V字时，其面向与右侧（+2）方向有线
        // 内为双线时，其面向与后侧（+4）方向有线
        // 中为猫猫头时，其面向与猫耳朵相反
        // 外为八方，无人在意

        var param = @event.Param();
        var tid = @event.TargetId();

        var paramMapping = new Dictionary<uint, (int _idx, string _pos, string _type)>
        {
            { SINGLE_LINE, (0, "内圈", "单线") },
            { V_SHAPE, (0, "内圈", "V字") },
            { DOUBLE_LINE, (0, "内圈", "双线") },
            { NEKO, (1, "中圈", "猫猫")},
            { EIGHTPOS, (2, "外圈", "八方")}
        };
        if (!paramMapping.ContainsKey(param)) return;
        var (_idx, _pos, _type) = paramMapping[param];

        // 记录ID
        if (!isCaptured[_idx])
        {
            isCaptured[_idx] = true;
            InnerCircleId[_idx] = IbcHelper.GetById(tid);
            DebugMsg($"确定{_pos}ID：{tid}", accessory);
        }

        var rot = InnerCircleId[_idx]?.Rotation ?? 0f;
        var logic_dir = DirectionCalc.DirRadRoundToDirs(DirectionCalc.BaseInnGame2DirRad((float)rot), 8);
        InnCircleType[_idx] = param;
        InnCircleFaceDir[_idx] = logic_dir;
        DebugMsg($"检测到{_pos}魔法阵类型：{_type}，逻辑方向{logic_dir}", accessory);
    }

    [ScriptMethod(name: "大转盘算法", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33001"], userControl: false)]
    public void PurgationAlgorithm(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            // 基于内圈类型、内圈面向与内圈旋转方向获得起始点
            var startPos = getFireStartPos(InnCircleType[0], InnCircleFaceDir[0] + RotateCircleDir[0]);
            string startPosStr = string.Join(", ", startPos);
            DebugMsg($"- 检测到起始点：{startPosStr}", accessory);

            // 基于中圈猫猫的面向与旋转方向获得猫猫头响应
            List<int> NekoResponse = rotateNekoResponse(InnCircleFaceDir[1] + RotateCircleDir[1]);
            string NekoTarget = string.Join(", ", NekoResponse);
            DebugMsg($"- 检测到猫猫头响应：{NekoTarget}", accessory);

            for (int i = 0; i < 2; i++)
            {
                if (startPos[i] != -1)
                {
                    OuterTargetMagicDir[i] = NekoResponse[startPos[i]];
                }
            }
            string outerTarget = string.Join(", ", OuterTargetMagicDir);
            DebugMsg($"- 检测到大转盘目标：{outerTarget}", accessory);

            // 根据转向，更改外圈魔法阵的目标
            int count = OuterFanMagic.Count();
            string RotateCircleDirStr = string.Join(", ", RotateCircleDir);
            DebugMsg($"- 检测到大转盘转动数据：{RotateCircleDirStr}", accessory);

            var RotateParam = RotateCircleDir[2] == -1 ? 7 : RotateCircleDir[2];
            List<bool> OuterFanMagicBias = new List<bool>(OuterFanMagic.Skip(count - RotateParam).Concat(OuterFanMagic.Take(count - RotateParam)));
            for (int i = 0; i < 2; i++)
            {
                if (OuterTargetMagicDir[i] != -1)
                {
                    drawOuterMagic(OuterTargetMagicDir[i], OuterFanMagicBias[OuterTargetMagicDir[i]], accessory);
                }
            }

        });
    }

    public static void drawOuterMagic(int outDir, bool magicTypeIsFan, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        float radian = (float)Math.PI / 4 * outDir;

        dp.Name = $"外圈魔法阵{outDir}";
        dp.Position = DirectionCalc.RotatePoint(new(100, 0, 80), new(100, 0, 100), radian);
        dp.TargetPosition = new(100, 0, 100);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = 0;
        dp.DestoryAt = 20000;

        dp.Scale = magicTypeIsFan ? new(45f) : new(45, 20);
        dp.Radian = magicTypeIsFan ? (float)Math.PI / 3 : (float)Math.PI * 2;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, magicTypeIsFan ? DrawTypeEnum.Fan : DrawTypeEnum.Rect, dp);
    }

    public static int[] getFireStartPos(uint innType, int innDir)
    {
        int startPos1;
        int startPos2;
        switch (innType)
        {
            case SINGLE_LINE:
                startPos1 = innDir;
                startPos2 = -1;
                break;
            case V_SHAPE:
                startPos1 = innDir;
                startPos2 = (innDir + 2) % 8;
                // startPos2 = (innDir + 2) >= 8 ? innDir - 6 : innDir + 2;
                break;
            case DOUBLE_LINE:
                startPos1 = innDir;
                startPos2 = (innDir + 4) % 8;
                // startPos2 = (innDir + 4) >= 8 ? innDir - 4 : innDir + 4;
                break;
            default:
                startPos2 = -1;
                startPos1 = -1;
                break;
        }
        return [startPos1, startPos2];
    }

    public static List<int> rotateNekoResponse(int nekoDir)
    {
        if (nekoDir > 7 | nekoDir < 0) return [-1, -1, -1, -1, -1, -1, -1, -1];

        // List<int> nekoResponse = [0, 1, -1, 2, 4, 6, -1, 7];
        List<int> nekoResponse = new List<int>(NekoOutputAtDir0);

        // 先增加方向
        for (int i = 0; i < nekoResponse.Count(); i++)
        {
            if (nekoResponse[i] == -1) continue;
            nekoResponse[i] += nekoDir;
            nekoResponse[i] = nekoResponse[i] % 8;
        }

        // 根据输入的nekoDir，将NekoOutputAtDir0中的元素向右移动nekoDir次，最右边的回到index0的位置。
        int count = nekoResponse.Count();
        nekoResponse = new List<int>(nekoResponse.Skip(count - nekoDir).Concat(nekoResponse.Take(count - nekoDir)));

        return nekoResponse;
    }

    [ScriptMethod(name: "大转盘参数初始化（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33001"], userControl: false)]
    public void PurgationInit(Event @event, ScriptAccessory accessory)
    {
        // 初始化参数
        InnCircleFaceDir = [0, 0, 0];       // 内魔法阵面向
        InnCircleType = [0, 0, 0];          // 内魔法阵类型
        RotateCircleDir = [0, 0, 0];        // 大转盘旋转方向，-1逆时针，+1顺时针，0不转
        OuterFanMagic = [false, false, false, false, false, false, false, false];   // 外圈魔法阵分布，是扇形为True，是半场刀为False
        OuterTargetMagicDir = [-1, -1];     // 大转盘目标
        accessory.Method.RemoveDraw(".*");

        DebugMsg($"- 大转盘参数初始化。", accessory);

    }

    #endregion
    #region 烈风火焰流

    [ScriptMethod(name: "烈风火焰流：分散、双人分摊、四人分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3200[234])$"])]
    public void SpreadFlame(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var aid = @event.ActionId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"烈风火焰流{tid}";
        dp.Owner = tid;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Delay = 0;
        dp.DestoryAt = 5000;

        var aidMapping = new Dictionary<uint, (float scale, Vector4 color)>
        {
            { 32002, (5f, ColorHelper.colorPink.V4) },   // 单人分散
            { 32003, (6f, accessory.Data.DefaultSafeColor) }, // 四人分摊
            { 32004, (4f, accessory.Data.DefaultSafeColor) }  // 二人分摊
        };

        if (aidMapping.ContainsKey(aid))
        {
            var properties = aidMapping[aid];
            dp.Scale = new(properties.scale);
            dp.Color = properties.color;
        }

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "烈风火焰流：扇形分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31998"])]
    public void FanSpread(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Owner = sid;
        dp.Radian = float.Pi / 6;
        dp.Scale = new(30);
        dp.Delay = 0;
        dp.DestoryAt = 7000;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            dp.Name = $"放散火流{i}";
            dp.TargetObject = accessory.Data.PartyList[i];
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    [ScriptMethod(name: "扇形死刑", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:00E6"], userControl: false)]
    public void Dualfire(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"扇形死刑{tid}";
        dp.Scale = new(60);
        dp.Position = new(100, 0, 100);
        dp.TargetObject = tid;
        dp.Radian = (float)Math.PI / 1.5f;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    #endregion

    #region 炀火之咒
    [ScriptMethod(name: "炀火之咒初始危险区", eventType: EventTypeEnum.EnvControl, eventCondition: ["Index:00000004"])]
    public async void Flamespire(Event @event, ScriptAccessory accessory)
    {
        var state = @event.State();
        bool isInterCard = false;
        if (state == 0x00080004)
        {
            await Task.Delay(2500);
            accessory.Method.RemoveDraw($"炀火之咒初始危险区");
            return;
        }
        if (IntercardFlags.Contains(state))
        {
            DebugMsg($"检测到先四角安全", accessory);
            isInterCard = true;
        }

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"炀火之咒初始危险区";

        if (!isInterCard)
        {
            dp.Position = new(100, 0, 100);
            dp.Rotation = (float)Math.PI / 4;
            dp.Scale = new(12, 40);
            dp.Delay = 0;
            dp.DestoryAt = 20000;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);

            dp.Rotation = (float)Math.PI / -4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
        else
        {
            dp.Position = new(100, 0, 100);
            dp.Rotation = 0;
            dp.Scale = new(12, 40);
            dp.Delay = 0;
            dp.DestoryAt = 20000;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);

            dp.Rotation = (float)Math.PI / 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
    }

    [ScriptMethod(name: "炀火之咒Buff分散", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3485"])]
    public void FlamespireSpreadBuff(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"炀火之咒Buff分散";
        dp.Owner = tid;
        dp.Delay = 10000;
        dp.DestoryAt = 5000;
        dp.Scale = new(6);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "火焰流圆形分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32016"])]
    public void InfernoSpread(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"火焰流圆形分散";
        dp.Owner = tid;
        dp.DestoryAt = 5000;
        dp.Scale = new(5);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "环火烽火钢月分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3203[67])$"])]
    public void ScaldingFleet(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"环烽火";
        dp.Owner = sid;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);

        if (@event.ActionId() == 32036)
        {
            // 烽火钢铁
            dp.Scale = new(10);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            // 环火月环
            dp.Scale = new(20);
            dp.InnerScale = new(10);
            dp.Radian = float.Pi * 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Owner = sid;
            dp.Name = $"迅火预备{i}";
            dp.Scale = new(6, 40);
            dp.TargetObject = accessory.Data.PartyList[i];
            dp.Delay = 0;
            dp.DestoryAt = 6500;
            dp.Color = ColorHelper.colorPink.V4.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "赤灭热波迅火回返", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32039"])]
    public void ImmolationChargeBack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"迅火回返";
        dp.Owner = sid;
        dp.Delay = 0;
        dp.DestoryAt = 6500;
        dp.Scale = new(6, 60);
        dp.Color = ColorHelper.colorCyan.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

    }

    [ScriptMethod(name: "赤灭热波分摊分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3203[45])$"])]
    public void ImmolationSpreadStack(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"赤灭热波分摊分散";
        dp.Owner = tid;
        dp.Delay = 0;
        dp.DestoryAt = 7500;

        if (@event.ActionId() == 32035)
        {
            // 分摊
            dp.Scale = new(6);
            dp.Color = accessory.Data.DefaultSafeColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            // 分散
            dp.Scale = new(5);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "赤灭热波半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3203[23])$"])]
    public void ImmolationHalfCleave(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"赤灭热波半场刀";
        dp.Owner = sid;
        dp.Delay = 0;
        dp.DestoryAt = 7000;
        dp.Scale = new(40, 20);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    #endregion

    public static void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event["Message"].ToString();
        accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"扇形死刑";
        dp.Scale = new(60);
        dp.Position = new(100, 0, 100);
        dp.TargetObject = accessory.Data.PartyList[0];
        dp.Radian = (float)Math.PI / 1.5f;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
}

#region 函数集
public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }

    public static uint Index(this Event @event)
    {
        return ParseHexId(@event["Index"], out var id) ? id : 0;
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var id) ? id : 0;
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }

    public static uint StatusID(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }
}

public static class IbcHelper
{
    public static IBattleChara? GetById(uint id)
    {
        return (IBattleChara?)Svc.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe()
    {
        return Svc.ClientState.LocalPlayer;
    }

    public static IEnumerable<IBattleChara> GetByDataId(uint dataId)
    {
        return (IEnumerable<IBattleChara>)Svc.Objects.Where(x => x.DataId == dataId);
    }

    public static uint GetCharHpcur(uint id)
    {
        // 如果null，返回0
        var hp = GetById(id)?.CurrentHp ?? 0;
        return hp;
    }
}

public static class DirectionCalc
{

    // 以北为0建立list
    // InnGame      List    Dir
    // 0            - 4     pi
    // 0.25 pi      - 3     0.75pi
    // 0.5 pi       - 2     0.5pi
    // 0.75 pi      - 1     0.25pi
    // pi           - 0     0
    // 1.25 pi      - 7     1.75pi
    // 1.5 pi       - 6     1.5pi
    // 1.75 pi      - 5     1.25pi
    // Dir = Pi - InnGame (+ 2pi)

    public static float BaseInnGame2DirRad(float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < 0) r = (float)(r + 2 * Math.PI);
        if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    public static float BaseDirRad2InnGame(float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        if (r > Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    public static int DirRadRoundToDirs(float radian, int dirs)
    {
        var r = Math.Round(radian / (2f / dirs * Math.PI));
        if (r == dirs) r = r - dirs;
        return (int)r;
    }

    public static int PositionFloorToDirs(Vector3 point, Vector3 center, int dirs)
    {
        // 正分割，0°为分界线，将360°分为dirs份
        var r = Math.Floor(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    public static int PositionRoundToDirs(Vector3 point, Vector3 center, int dirs)
    {
        // 斜分割，0° return 0，将360°分为dirs份
        var r = Math.Round(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    public static float angle2Rad(float angle)
    {
        // 输入角度转为弧度
        float radian = (float)(angle * Math.PI / 180);
        return radian;
    }

    public static Vector3 RotatePoint(Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度，以北为0度
        // dp.Rotation是以南为0度，逆时针度数增加
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);
    }

    public static Vector3 ExtendPoint(Vector3 center, float radian, float length)
    {
        // 令某点以某弧度延伸一定长度
        return new(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    public static float FindRadian(Vector3 center, Vector3 new_point)
    {
        // 找到某点到中心的弧度
        float radian = MathF.PI - MathF.Atan2(new_point.X - center.X, new_point.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }
}

public static class IndexHelper
{
    public static int getPlayerIdIndex(ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    public static int getMyIndex(ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    public static string getPlayerJobIndex(ScriptAccessory accessory, uint pid)
    {
        // 获得玩家职能简称
        var a = accessory.Data.PartyList.IndexOf(pid);
        switch (a)
        {
            case 0: return "MT";
            case 1: return "ST";
            case 2: return "H1";
            case 3: return "H2";
            case 4: return "D1";
            case 5: return "D2";
            case 6: return "D3";
            case 7: return "D4";
            default: return "unknown";
        }
    }
}

public static class ColorHelper
{
    public static ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public static ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public static ScriptColor colorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };
}

#endregion
