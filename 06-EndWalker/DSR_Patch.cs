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
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using System.Drawing;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace UsamisScript.EndWalker.DSR_Patch;

[ScriptType(name: "DSR_Patch [幻想龙诗绝境战 补丁]", territorys: [968], guid: "cc6fb606-ff7b-4739-81aa-4861b204ab1e", version: "0.0.0.2", author: "Usami", note: noteStr)]

public class DSR_Patch
{
    const string noteStr =
    """
    基于K佬绝龙诗绘图的个人向补充，
    请先按需求检查并设置“用户设置”栏目。

    v0.0.0.2
    1. 修复P7地火间隔错误问题。
    2. 调整P7地火预设颜色，于“用户设置”增加一系列可选项。

    v0.0.0.1
    初版完成。
    鸭门。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("地火（百京核爆）使用程序预设颜色")]
    public static bool exaflareBuiltInColor { get; set; } = true;

    [UserSetting("地火（百京核爆）爆炸区颜色")]
    public ScriptColor exaflareColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

    [UserSetting("地火（百京核爆）是否绘制下一枚地火预警区")]
    public static bool exaflareWarnDrawn { get; set; } = true;

    [UserSetting("地火（百京核爆）预警区颜色")]
    public ScriptColor exaflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1.0f, 1.0f) };
    public enum DSR_Phase
    {
        Init,                   // 初始
        P5_HeavensWrath,        // P5 一运
        P5_HeavensDeath,        // P5 二运
        P6_IceAndFire_1,        // P6 一冰火
        P6_NearOrFar_1,         // P6 一远近
        P6_Flame,               // P6 十字火
        P6_NearOrFar_2,         // P6 二远近
        P6_IceAndFire_2,        // P6 二冰火
        P6_Cauterize,           // P6 俯冲
        P7_Exaflare_1,          // P7 一地火
        P7_Stack_1,             // P7 一分摊
        P7_Nuclear_1,           // P7 一核爆
        P7_Exaflare_2,          // P7 二地火
        P7_Stack_2,             // P7 二分摊
        P7_Nuclear_2,           // P7 二核爆
        P7_Exaflare_3,          // P7 三地火
        P7_Stack_3,             // P7 三分摊
        P7_Enrage,              // P7 狂暴
    }
    public static Vector3 CENTER = new Vector3(100, 0, 100);
    DSR_Phase phase = DSR_Phase.Init;
    List<bool> Drawn = new bool[20].ToList();                   // 绘图记录
    List<bool> P6_DragonsGlowAction = [false, false];           // P6 双龙吐息记录
    List<bool> P6_DragonsWingAction = [false, false, false];    // P6 双龙远近记录 [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
    List<bool> P7_FirstEntityOrder = [false, false];            // P7 平A仇恨记录
    List<int> P7_TrinityOrderIdx = [4, 5, 6, 7, 2, 3];          // P7 接刀顺序
    bool P7_TrinityDisordered = false;                          // P7 接刀顺序是否出错
    bool P7_TrinityTankDisordered = false;                      // P7 坦克接刀仇恨是否出错
    int P7_TrinityNum = 0;                                      // P7 接刀次数
    public void Init(ScriptAccessory accessory)
    {
        phase = DSR_Phase.Init;
        Drawn = new bool[20].ToList();   // 绘图记录
        accessory.Method.RemoveDraw(".*");
    }

    public static void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }


    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;

        // DEBUG CODE
        uint _bossid = 0x400036B1;
        // debugCircle(_bossid, accessory);
        debugDonut(_bossid, accessory);
        debugExaflare(new Vector3(106.87f, 0, 104.09f), 3.13f, accessory);
        debugExaflare(new Vector3(100.11f, 0, 92f), 3.13f, accessory);
        debugExaflare(new Vector3(93.02f, 0, 103.91f), 2.34f, accessory);
    }

    #region P2

    [ScriptMethod(name: "P2：引导不可视刀范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25545"])]
    public void P2_AscalonConcealed(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.drawFan(sid, float.Pi / 180 * 30, 30, 0, 1500, $"不可视刀");
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    #endregion

    #region P5

    [ScriptMethod(name: "P5：一运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27529"], userControl: false)]
    public void P5_HeavensWrath_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = DSR_Phase.P5_HeavensWrath;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "P5：旋风冲旋风预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
    public async void P5_TwistingDive(Event @event, ScriptAccessory accessory)
    {
        drawTwister(3000, 3000, accessory);
        await Task.Delay(3000);
        accessory.Method.TextInfo("旋风", 3000, true);
    }

    [ScriptMethod(name: "P5：旋风危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"])]
    public void TwisterField(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var dp = accessory.drawStatic(spos, 0, 0, 4800, $"旋风危险区{spos}");
        dp.Scale = new(1.5f);
        dp.Color = ColorHelper.colorRed.V4.WithW(3);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    private void drawTwister(int delay, int destoryAt, ScriptAccessory accessory)
    {
        for (var i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = accessory.drawCircle(accessory.Data.PartyList[i], delay, destoryAt, $"旋风{i}");
            dp.Scale = new(1.5f);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P5：大圈火预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25573"])]
    public void P5_AlterFlare(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P5_HeavensWrath) return;
        var spos = @event.SourcePosition();
        var dp = accessory.drawStatic(spos, 0, 0, 4000, $"大圈火危险区{spos}");
        dp.Scale = new(8f);
        dp.Color = ColorHelper.colorRed.V4.WithW(1.5f);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P5：二运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27538"], userControl: false)]
    public void P5_HeavensDeath_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = DSR_Phase.P5_HeavensDeath;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "P5：二运，找到斧头哥方位", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12637"])]
    public void P5_FindSerGuerrique(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P5_HeavensDeath) return;
        var spos = @event.SourcePosition();
        var dp = accessory.dirPos2Pos(CENTER, spos, 0, 4000, $"找到斧头哥");
        dp.Scale = new(2f);
        dp.Color = ColorHelper.colorWhite.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    #endregion

    #region P6 冰火

    [ScriptMethod(name: "P6：一冰火，阶段记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:12613"], userControl: false)]
    public void P6_IceAndFire1_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 圣龙出现代表进入一冰火
        if (phase != DSR_Phase.P5_HeavensDeath) return;
        phase = DSR_Phase.P6_IceAndFire_1;
        P6_DragonsGlowAction = [false, false];   // P6 双龙吐息记录
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "P6：二冰火，阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
    public void P6_IceAndFire2_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 以辣翅辣尾作为二冰火的开始
        if (phase != DSR_Phase.P6_NearOrFar_2) return;
        phase = DSR_Phase.P6_IceAndFire_2;
        P6_DragonsGlowAction = [false, false];   // P6 双龙吐息记录
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    const uint BLACK_BUSTER = 27954;
    const uint WHITE_BUSTER = 27956;
    const uint BLACK_GLOW = 27955;
    const uint WHITE_GLOW = 27957;

    [ScriptMethod(name: "P6：冰火吐息记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2795[4567])$"], userControl: false)]
    public void P6_IceAndFireGlowRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_IceAndFire_1 && phase != DSR_Phase.P6_IceAndFire_2) return;
        var aid = @event.ActionId();
        switch (aid)
        {
            case BLACK_BUSTER:
            case BLACK_GLOW:
                P6_DragonsGlowAction[0] = aid == BLACK_GLOW;
                break;
            case WHITE_BUSTER:
            case WHITE_GLOW:
                P6_DragonsGlowAction[1] = aid == WHITE_GLOW;
                break;
            default:
                break;
        }
    }

    [ScriptMethod(name: "P6：冰火死刑双T处理", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27960"])]
    public async void P6_IceAndFireTankSolution(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_IceAndFire_1 && phase != DSR_Phase.P6_IceAndFire_2) return;
        await Task.Delay(100);

        var myIndex = accessory.getMyIndex();
        Vector3[] _TankBusterPosition = new Vector3[4];
        _TankBusterPosition[0] = new(84.5f, 0, 88f);
        _TankBusterPosition[1] = _TankBusterPosition[0].FoldPointLR(CENTER.X);
        _TankBusterPosition[2] = _TankBusterPosition[0];
        _TankBusterPosition[3] = _TankBusterPosition[1].FoldPointUD(CENTER.Z);

        if (P6_DragonsGlowAction[0] && P6_DragonsGlowAction[1])
        {
            // 场中分摊死刑，自己不是T不显示指路
            if (myIndex > 1) return;
            // 删除K佬脚本中双T的小啾啾
            accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
            var dp = accessory.dirPos(CENTER, 0, 6000, $"冰火场中分摊指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else
        {
            // 场边死刑，自己的死刑不显示圈，避免瞎眼
            int _buster_idx = P6_DragonsGlowAction.FindIndex(x => x == false);
            DebugMsg($"黑龙喷:{P6_DragonsGlowAction[0]}, 白龙喷:{P6_DragonsGlowAction[1]}", accessory);
            DebugMsg($"是{(_buster_idx == 0 ? "黑龙" : "白龙")}的死刑。", accessory);

            var dp = accessory.drawCircle(accessory.Data.PartyList[_buster_idx], 0, 6000, $"冰火死刑");
            dp.Color = myIndex == _buster_idx ? ColorHelper.colorRed.V4 : ColorHelper.colorYellow.V4;
            // dp.Color = ColorHelper.colorYellow.V4.WithW(myIndex == _buster_idx ? 0f : 1f);
            dp.Scale = new(myIndex == _buster_idx ? 2f : 15f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            // 场边分散，自己不是T不显示指路
            if (myIndex > 1) return;
            // 删除K佬脚本中双T的小啾啾
            accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
            bool isIceAndFire2 = phase == DSR_Phase.P6_IceAndFire_2;

            var dp0 = accessory.dirPos(_TankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex], 0, 6000, $"冰火死刑位置指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

            var dp1 = accessory.drawStatic(_TankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex], 0, 0, 6000, $"冰火死刑点区域");
            dp1.Scale = new(1f);
            dp1.Color = posColorPlayer.V4.WithW(1.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
        }
    }

    #endregion

    #region P6 远近

    [ScriptMethod(name: "P6：远近，阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27970"], userControl: false)]
    public void P6_NearOrFar_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 因为黑龙先飞，白龙后读条，所以用无尽轮回的ActionEffect做阶段节点
        if (phase == DSR_Phase.P6_NearOrFar_1 || phase == DSR_Phase.P6_NearOrFar_2) return;

        phase = phase switch
        {
            DSR_Phase.P6_IceAndFire_1 => DSR_Phase.P6_NearOrFar_1,
            DSR_Phase.P6_Flame => DSR_Phase.P6_NearOrFar_2,
            _ => DSR_Phase.P6_NearOrFar_1,
        };

        P6_DragonsWingAction = [false, false, false];   // P6 双龙远近记录
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    // LEFT左翼发光，玩家视角左侧安全。
    const uint LEFT_FAR = 27940;
    const uint LEFT_NEAR = 27939;
    const uint RIGHT_FAR = 27943;
    const uint RIGHT_NEAR = 27942;
    const uint INSIDE_SAFE = 27947;
    const uint OUTSIDE_SAFE = 27949;

    [ScriptMethod(name: "P6：远近，翅膀记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"], userControl: false)]
    public void P6_NearOrFar_WingsRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_NearOrFar_1 && phase != DSR_Phase.P6_NearOrFar_2) return;
        var aid = @event.ActionId();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        P6_DragonsWingAction[0] = aid == LEFT_FAR || aid == RIGHT_FAR;
        P6_DragonsWingAction[1] = aid == LEFT_FAR || aid == LEFT_NEAR;
    }

    [ScriptMethod(name: "P6：远近，俯冲记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12612"], userControl: false)]
    public void P6_NearOrFar_CauterizeRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_NearOrFar_1) return;
        var spos = @event.SourcePosition();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        P6_DragonsWingAction[2] = spos.X < CENTER.X;
    }

    [ScriptMethod(name: "P6：远近，内外记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
    public void P6_NearOrFar_BlackWingsRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_NearOrFar_2) return;
        var aid = @event.ActionId();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        P6_DragonsWingAction[2] = aid == INSIDE_SAFE;
    }

    [ScriptMethod(name: "P6：一远近，指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"])]
    public async void P6_NearOrFar1_Dir(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_NearOrFar_1) return;
        await Task.Delay(100);

        Vector3[] _NearOrFarSafePos = getQuarterSafePos(P6_DragonsWingAction);
        int[] _NearOrFarDirPosIdx = getQuarterSafePosIdx(P6_DragonsWingAction);

        var myIndex = accessory.getMyIndex();
        var _myPartIdx = myIndex >= 2 ? 2 : myIndex;
        var _targetPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[_myPartIdx]];

        for (int i = 0; i < 3; i++)
        {
            var _tempPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[i]];
            var dp0 = accessory.drawStatic(_tempPos, 0, 0, 7500, $"一远近位置{i}");
            dp0.Scale = new(1f);
            dp0.Color = i == _myPartIdx ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = accessory.dirPos(_targetPos, 0, 7500, $"一远近指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private Vector3[] getQuarterSafePos(List<bool> wings)
    {
        // 第一象限内的四个端点
        // 象限内四个点Idx顺序为，以第一象限基准（面向白龙左上），从左上开始顺时针
        // 上下平移，左右折叠
        Vector3[] _QuarterSafePos = new Vector3[4];
        _QuarterSafePos[0] = new(120f, 0, 80f);
        _QuarterSafePos[1] = new(120f, 0, 98f);
        _QuarterSafePos[2] = new(102f, 0, 98f);
        _QuarterSafePos[3] = new(102f, 0, 80f);
        for (int i = 0; i < 4; i++)
        {
            // 后安全，向后平移
            if (!wings[2])
                _QuarterSafePos[i] = _QuarterSafePos[i] - new Vector3(22f, 0, 0);
            // 右安全，左右折叠
            if (!wings[1])
                _QuarterSafePos[i] = _QuarterSafePos[i].FoldPointUD(CENTER.Z);
        }
        return _QuarterSafePos;
    }

    private int[] getQuarterSafePosIdx(List<bool> wings)
    {
        // return数组，代表MT、ST、人群的安全位置Index

        // 打远，双T远离，人群靠近
        if (wings[0])
            return [2, 3, 1];
        // 打近，双T靠近，人群远离
        else
            return [1, 0, 3];
    }

    [ScriptMethod(name: "P6：二远近，指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"])]
    public async void P6_NearOrFar2_Dir(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_NearOrFar_2) return;

        // 黑龙读条慢
        await Task.Delay(100);

        Vector3[] _NearOrFarSafePos = getLineSafePos(P6_DragonsWingAction);
        int[] _NearOrFarDirPosIdx = getLineSafePosIdx(P6_DragonsWingAction);

        var myIndex = accessory.getMyIndex();
        var _myPartIdx = myIndex >= 2 ? 2 : myIndex;
        var _targetPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[_myPartIdx]];

        for (int i = 0; i < 3; i++)
        {
            var _tempPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[i]];
            var dp0 = accessory.drawStatic(_tempPos, 0, 0, 7500, $"二远近位置{i}");
            dp0.Scale = new(1f);
            dp0.Color = i == _myPartIdx ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = accessory.dirPos(_targetPos, 0, 7500, $"二远近指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private Vector3[] getLineSafePos(List<bool> wings)
    {
        // 直线近中远三点
        Vector3[] _LineSafePos = new Vector3[3];
        _LineSafePos[0] = new(120f, 0, 100f);
        _LineSafePos[1] = new(100f, 0, 100f);
        _LineSafePos[2] = new(80f, 0, 100f);

        Vector3 _dv = new(0f, 0f, 0f);

        // 左安全减，右安全加
        _dv = _dv + new Vector3(0f, 0f, 2f) * (wings[1] ? -1 : 1);
        // 内安全不动，外安全乘
        _dv = _dv * (wings[2] ? 1 : 5);

        for (int i = 0; i < 3; i++)
        {
            _LineSafePos[i] = _LineSafePos[i] + _dv;
        }
        return _LineSafePos;
    }

    private int[] getLineSafePosIdx(List<bool> wings)
    {
        // return数组，代表MT、ST、人群的安全位置Index

        // 打远，双T远离，人群靠近
        if (wings[0])
            return [1, 2, 0];
        // 打近，双T靠近，人群远离
        else
            return [1, 0, 2];
    }

    #endregion

    #region P6 十字火

    [ScriptMethod(name: "P6：十字火，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27973"], userControl: false)]
    public void P6_Flame_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = DSR_Phase.P6_Flame;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "P6：十字火，分摊目标", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"])]
    public void P6_FlameStackTarget(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_Flame) return;
        var tid = @event.TargetId();
        var dp = accessory.drawCircle(tid, 0, 12500, $"死亡轮回目标{tid}");
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new(6f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region P6 俯冲

    [ScriptMethod(name: "P6：俯冲，双T指路", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7737", "SourceDataId:12613"])]
    public void P6_CauterizeDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != DSR_Phase.P6_IceAndFire_2) return;
        phase = DSR_Phase.P6_Cauterize;
        DebugMsg($"当前阶段为：{phase}", accessory);

        Vector3[] _CauterizePos = new Vector3[2];
        _CauterizePos[0] = new(95f, 0, 79f);
        _CauterizePos[1] = new(105f, 0, 79f);

        var myIndex = accessory.getMyIndex();
        if (myIndex > 1) return;

        var dp = accessory.dirPos(_CauterizePos[myIndex], 0, 5000, $"俯冲T挡枪位置{myIndex}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    #endregion

    #region P7 接刀

    [ScriptMethod(name: "P7：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179]|28206)$"], userControl: false)]
    public void P7_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            DSR_Phase.P6_Cauterize => DSR_Phase.P7_Exaflare_1,
            DSR_Phase.P7_Exaflare_1 => DSR_Phase.P7_Stack_1,
            DSR_Phase.P7_Stack_1 => DSR_Phase.P7_Nuclear_1,
            DSR_Phase.P7_Nuclear_1 => DSR_Phase.P7_Exaflare_2,
            DSR_Phase.P7_Exaflare_2 => DSR_Phase.P7_Stack_2,
            DSR_Phase.P7_Stack_2 => DSR_Phase.P7_Nuclear_2,
            DSR_Phase.P7_Nuclear_2 => DSR_Phase.P7_Exaflare_3,
            DSR_Phase.P7_Exaflare_3 => DSR_Phase.P7_Stack_3,
            DSR_Phase.P7_Stack_3 => DSR_Phase.P7_Enrage,
            _ => DSR_Phase.P7_Exaflare_1
        };
        DebugMsg($"当前阶段为：{phase}", accessory);

        if (!P7_FirstEntityOrder.Contains(true))
        {
            // 初始化
            P7_FirstEntityOrder = [true, false];
            P7_TrinityDisordered = false;
            P7_TrinityTankDisordered = false;
            P7_TrinityNum = 0;
        }
        else
        {
            P7_FirstEntityOrder[0] = !P7_FirstEntityOrder[0];
            P7_FirstEntityOrder[1] = !P7_FirstEntityOrder[1];
            DebugMsg($"MT为{(P7_FirstEntityOrder[0] ? "一仇" : "二仇")}，ST为{(P7_FirstEntityOrder[1] ? "一仇" : "二仇")}", accessory);
        }
    }

    [ScriptMethod(name: "P7：三剑一体接刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179])$"])]
    public async void P7_TrinityAttack(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(100);

        var aid = @event.ActionId();
        var sid = @event.SourceId();
        const uint EXAFLARE = 28059;
        const uint STACK = 28051;
        const uint NUCLEAR = 28057;

        var delay = aid switch
        {
            EXAFLARE => 15000,
            STACK => 16000,
            NUCLEAR => 26000,
            _ => 0
        };
        if (phase == DSR_Phase.P7_Stack_1)
            delay = delay + 2000;
        if (phase == DSR_Phase.P7_Stack_2)
            delay = delay + 3000;
        if (phase == DSR_Phase.P7_Stack_3)
            delay = delay + 4000;

        drawTrinityAggro(sid, delay - 6000, 6000, 1, accessory);
        drawTrinityAggro(sid, delay - 6000, 6000, 2, accessory);
        drawTrinityAggro(sid, delay, 4000, 1, accessory);
        drawTrinityAggro(sid, delay, 4000, 2, accessory);

        drawTrinityNear(sid, delay - 6000, 6000, accessory);
        drawTrinityNear(sid, delay, 4000, accessory);
    }

    private void drawTrinityAggro(uint sid, int delay, int destory, uint aggroIdx, ScriptAccessory accessory)
    {
        var myIndex = accessory.getMyIndex();
        Vector4 color;

        if ((myIndex > 1) || P7_TrinityTankDisordered)
            color = accessory.Data.DefaultDangerColor;
        else
        {
            if (P7_FirstEntityOrder[myIndex] && (aggroIdx == 1))
                color = accessory.Data.DefaultSafeColor;
            else if (!P7_FirstEntityOrder[myIndex] && (aggroIdx == 2))
                color = accessory.Data.DefaultSafeColor;
            else
                color = accessory.Data.DefaultDangerColor;
        }

        var dp = accessory.drawCenterOrder(sid, aggroIdx, delay, destory, $"三剑一体1仇恨{aggroIdx}");
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp.Color = color.WithW(2f);
        dp.Scale = new(3f);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawTrinityNear(uint sid, int delay, int destory, ScriptAccessory accessory)
    {
        var myIndex = accessory.getMyIndex();
        var dp = accessory.drawCenterOrder(sid, 1, delay, destory, $"三剑一体近距");
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        if (P7_TrinityDisordered)
            dp.Color = accessory.Data.DefaultDangerColor;
        else
            dp.Color = myIndex == P7_TrinityOrderIdx[P7_TrinityNum] ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.Scale = new(3f);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P7：三剑一体接刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:28065"], userControl: false)]
    public void P7_TrinityOrderRecord(Event @event, ScriptAccessory accessory)
    {
        // 主视角为T
        var myIndex = accessory.getMyIndex();
        if (myIndex < 2) return;

        var targetIdx = @event.TargetIndex();
        if (targetIdx != 1)
        {
            if (!P7_TrinityDisordered)
            {
                DebugMsg($"多接了一刀，失效", accessory);
                accessory.Method.TextInfo($"多接了一刀，不再以安全色提示", 3000, true);
                P7_TrinityDisordered = true;
            }
            return;
        }

        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        if ((P7_TrinityOrderIdx[P7_TrinityNum] != tidx) && (!P7_TrinityDisordered))
        {
            DebugMsg($"接刀人错误，失效", accessory);
            accessory.Method.TextInfo($"接刀人错误，不再以安全色提示", 3000, true);
            P7_TrinityDisordered = true;
        }

        P7_TrinityNum++;
        if (P7_TrinityNum >= 6)
            P7_TrinityNum = 0;

        DebugMsg($"刚刚接刀的是{accessory.getPlayerJobByIndex(tidx)}，下一个接刀人为{accessory.getPlayerJobByIndex(P7_TrinityOrderIdx[P7_TrinityNum])}", accessory);
    }

    [ScriptMethod(name: "P7：三剑一体T刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2806[34])$"], userControl: false)]
    public void P7_TrinityTankRecord(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var tid = @event.TargetId();

        // 非T玩家接到刀
        var tidx = accessory.getPlayerIdIndex(tid);
        if (tidx > 1) return;

        // 主视角不是T
        var myIndex = accessory.getMyIndex();
        if (myIndex > 1) return;

        // 已经失效
        if (P7_TrinityTankDisordered) return;

        const uint AGGRO1 = 28063;
        const uint AGGRO2 = 28064;

        // 一仇效果，但目标是二仇 || 二仇效果，但目标是一仇
        if ((!P7_FirstEntityOrder[tidx] && (aid == AGGRO1)) || (P7_FirstEntityOrder[tidx] && (aid == AGGRO2)))
        {
            DebugMsg($"接刀仇恨错误，失效", accessory);
            accessory.Method.TextInfo($"接刀仇恨错误，不再以安全色提示", 3000, true);
            P7_TrinityTankDisordered = true;
        }
    }

    #endregion

    #region P7 地火

    [ScriptMethod(name: "P7：地火范围绘制", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28060"])]
    public void P7_ExaflareDrawn(Event @event, ScriptAccessory accessory)
    {
        // 面相为前、左、右的扩散
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();
        // var _logic_rad = srot.BaseInnGame2DirRad();
        // var _logic_angle = _rad.rad2Angle();

        const int INTERVAL_TIME = 1850;
        const int CAST_TIME = 7000;
        const int EXTEND_DISTANCE = 8;

        Vector3[,] _exaflarePos = new Vector3[3, 6];
        for (int i = 0; i < 6; i++)
        {
            _exaflarePos[0, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot), EXTEND_DISTANCE * i);
            _exaflarePos[1, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot - float.Pi / 2), EXTEND_DISTANCE * i);
            _exaflarePos[2, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot + float.Pi / 2), EXTEND_DISTANCE * i);
        }

        for (int i = 0; i < 6; i++)
        {
            var _destoryAt = i == 0 ? CAST_TIME : INTERVAL_TIME;
            var _delay = i == 0 ? 0 : CAST_TIME + (i - 1) * INTERVAL_TIME;
            // 本体地火
            if (i == 0)
            {
                // 第一轮只画一个
                drawExaflare(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[0, i], _delay, _destoryAt, accessory);
            }
            else
            {
                drawExaflare(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflare(_exaflarePos[1, i], _delay, _destoryAt, accessory);
                drawExaflare(_exaflarePos[2, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[1, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[2, i], _delay, _destoryAt, accessory);
            }

            // 预警地火
            if (exaflareWarnDrawn)
            {
                if (i < 5)
                {
                    drawExaflareWarn(_exaflarePos[0, i + 1], 1, _delay, _destoryAt, accessory);
                    drawExaflareWarn(_exaflarePos[1, i + 1], 1, _delay, _destoryAt, accessory);
                    drawExaflareWarn(_exaflarePos[2, i + 1], 1, _delay, _destoryAt, accessory);
                }
            }
            // if (i < 4)
            // {
            //     drawExaflareWarn(_exaflarePos[0, i + 2], 2, _delay, _destoryAt, accessory);
            //     drawExaflareWarn(_exaflarePos[1, i + 2], 2, _delay, _destoryAt, accessory);
            //     drawExaflareWarn(_exaflarePos[2, i + 2], 2, _delay, _destoryAt, accessory);
            // }
        }
    }

    private void drawExaflare(Vector3 spos, int delay, int destoryAt, ScriptAccessory accessory)
    {
        const int SCALE = 6;

        var dp = accessory.drawStatic(spos, 0, delay, destoryAt, $"地火{spos}");
        dp.Scale = new(SCALE);
        dp.Color = exaflareBuiltInColor ? ColorHelper.colorExaflare.V4 : exaflareColor.V4.WithW(1f);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawExaflareEdge(Vector3 spos, int delay, int destoryAt, ScriptAccessory accessory)
    {
        const int SCALE = 6;

        var dp = accessory.drawStatic(spos, 0, delay, destoryAt, $"地火边缘{spos}");
        dp.Scale = new(SCALE);
        dp.InnerScale = new(SCALE - 0.05f);
        dp.Color = exaflareBuiltInColor ? ColorHelper.colorExaflare.V4 : exaflareColor.V4.WithW(1.5f);
        // dp.Color = ColorHelper.colorDark.V4;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
    }

    private void drawExaflareWarn(Vector3 spos, int adv, int delay, int destoryAt, ScriptAccessory accessory)
    {
        const int INTERVAL_TIME = 1850;
        const int SCALE = 6;

        var destroy_add = INTERVAL_TIME * (adv - 1);
        var dp = accessory.drawStatic(spos, 0, delay, destoryAt + destroy_add, $"地火{spos}");
        dp.Scale = new(SCALE);
        dp.Color = exaflareBuiltInColor ? ColorHelper.colorExaflareWarn.V4.WithW(1f / adv) : exaflareWarnColor.V4.WithW(1f / adv);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void debugExaflare(Vector3 spos, float srot, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        const int INTERVAL_TIME = 1850;
        const int CAST_TIME = 7000;
        const int EXTEND_DISTANCE = 8;

        Vector3[,] _exaflarePos = new Vector3[3, 6];
        for (int i = 0; i < 6; i++)
        {
            _exaflarePos[0, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot), EXTEND_DISTANCE * i);
            _exaflarePos[1, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot - float.Pi / 2), EXTEND_DISTANCE * i);
            _exaflarePos[2, i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot + float.Pi / 2), EXTEND_DISTANCE * i);
        }

        for (int i = 0; i < 6; i++)
        {
            var _destoryAt = i == 0 ? CAST_TIME : INTERVAL_TIME;
            var _delay = i == 0 ? 0 : CAST_TIME + (i - 1) * INTERVAL_TIME;
            // 本体地火
            if (i == 0)
            {
                // 第一轮只画一个
                drawExaflare(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[0, i], _delay, _destoryAt, accessory);
            }
            else
            {
                drawExaflare(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflare(_exaflarePos[1, i], _delay, _destoryAt, accessory);
                drawExaflare(_exaflarePos[2, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[0, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[1, i], _delay, _destoryAt, accessory);
                drawExaflareEdge(_exaflarePos[2, i], _delay, _destoryAt, accessory);
            }

            // 预警地火
            if (exaflareWarnDrawn)
            {
                if (i < 5)
                {
                    drawExaflareWarn(_exaflarePos[0, i + 1], 1, _delay, _destoryAt, accessory);
                    drawExaflareWarn(_exaflarePos[1, i + 1], 1, _delay, _destoryAt, accessory);
                    drawExaflareWarn(_exaflarePos[2, i + 1], 1, _delay, _destoryAt, accessory);
                }
            }

            // if (i < 4)
            // {
            //     drawExaflareWarn(_exaflarePos[0, i + 2], 2, _delay, _destoryAt, accessory);
            //     drawExaflareWarn(_exaflarePos[1, i + 2], 2, _delay, _destoryAt, accessory);
            //     drawExaflareWarn(_exaflarePos[2, i + 2], 2, _delay, _destoryAt, accessory);
            // }
        }
    }

    private void debugCircle(uint boss_id, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var dp = accessory.drawCircle(boss_id, 0, 7000, $"钢铁");
        dp.Scale = new(8);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void debugDonut(uint boss_id, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var dp = accessory.drawDonut(boss_id, 0, 7000, $"月环");
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1f);
        dp.Scale = new(50);
        dp.InnerScale = new(8);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    #endregion

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

    public static uint TargetIndex(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
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
    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
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

    public static IEnumerable<IGameObject?> GetByDataId(uint dataId)
    {
        return Svc.Objects.Where(x => x.DataId == dataId);
    }

    public static uint GetCharHpcur(uint id)
    {
        // 如果null，返回0
        var hp = GetById(id)?.CurrentHp ?? 0;
        return hp;
    }

    public static bool isTank(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Tank;
    }
    public static bool isHealer(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Healer;
    }
    public static bool isDps(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.DPS;
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

    /// <summary>
    /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
    /// </summary>
    /// <param name="radian">游戏基角度</param>
    /// <returns>逻辑基角度</returns>
    public static float BaseInnGame2DirRad(this float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < 0) r = (float)(r + 2 * Math.PI);
        if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    /// <summary>
    /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <returns>游戏基角度</returns>
    public static float BaseDirRad2InnGame(this float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        if (r > Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    /// <summary>
    /// 输入逻辑基角度，获取逻辑方位
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>逻辑基角度对应的逻辑方位</returns>
    public static int DirRadRoundToDirs(this float radian, int dirs)
    {
        var r = Math.Round(radian / (2f / dirs * Math.PI));
        if (r == dirs) r = r - dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取正分割逻辑方位（以右上为0）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int PositionFloorToDirs(this Vector3 point, Vector3 center, int dirs)
    {
        // 正分割，0°为分界线，将360°分为dirs份
        var r = Math.Floor(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取斜分割逻辑方位（以正上为0）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int PositionRoundToDirs(this Vector3 point, Vector3 center, int dirs)
    {
        // 斜分割，0° return 0，将360°分为dirs份
        var r = Math.Round(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 将角度转为弧度
    /// </summary>
    /// <param name="angle">角度值</param>
    /// <returns>对应的弧度值</returns>
    public static float angle2Rad(this float angle)
    {
        // 输入角度转为弧度
        float radian = (float)(angle * Math.PI / 180);
        return radian;
    }

    /// <summary>
    /// 将弧度转为角度
    /// </summary>
    /// <param name="radian">弧度值</param>
    /// <returns>对应的角度值</returns>
    public static float rad2Angle(this float radian)
    {
        // 输入角度转为弧度
        float angle = (float)(radian / Math.PI * 180);
        return angle;
    }

    /// <summary>
    /// 以逻辑基弧度旋转某点
    /// </summary>
    /// <param name="point">待旋转点坐标</param>
    /// <param name="center">中心</param>
    /// <param name="radian">旋转弧度</param>
    /// <returns>旋转后坐标点</returns>
    public static Vector3 RotatePoint(this Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);

        // TODO 另一种方案待验证
        // var nextPos = Vector3.Transform((point - center), Matrix4x4.CreateRotationY(radian)) + center;
    }

    /// <summary>
    /// 以逻辑基角度从某中心点向外延伸
    /// </summary>
    /// <param name="center">待延伸中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">延伸长度</param>
    /// <returns>延伸后坐标点</returns>
    public static Vector3 ExtendPoint(this Vector3 center, float radian, float length)
    {
        // 令某点以某弧度延伸一定长度
        return new(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="new_point">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(this Vector3 new_point, Vector3 center)
    {
        // 找到某点到中心的弧度
        float radian = MathF.PI - MathF.Atan2(new_point.X - center.X, new_point.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerx">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointLR(this Vector3 point, float centerx)
    {
        Vector3 v3 = new(2 * centerx - point.X, point.Y, point.Z);
        return v3;
    }

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerz">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointUD(this Vector3 point, float centerz)
    {
        Vector3 v3 = new(point.X, point.Y, 2 * centerz - point.Z);
        return v3;
    }
}

public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataid，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int getPlayerIdIndex(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int getMyIndex(this ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataid，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string getPlayerJobByID(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
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

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string getPlayerJobByIndex(this ScriptAccessory accessory, int idx)
    {
        switch (idx)
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
    public static ScriptColor colorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
    public static ScriptColor colorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
    public static ScriptColor colorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
    public static ScriptColor colorYellow = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

    public static ScriptColor colorExaflare = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0.0f, 1.5f) };
    public static ScriptColor colorExaflareWarn = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1f, 1.0f) };

}

public static class AssignDp
{
    /// <summary>
    /// 返回自己指向某目标地点的dp，可修改dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="target_pos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirPos(this ScriptAccessory accessory, Vector3 target_pos, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(1f);
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = target_pos;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回起始地点指向某目标地点的dp，可修改dp.Position, dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="start_pos">起始地点</param>
    /// <param name="target_pos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirPos2Pos(this ScriptAccessory accessory, Vector3 start_pos, Vector3 target_pos, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(1f);
        dp.Position = start_pos;
        dp.TargetPosition = target_pos;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回自己指向某目标对象的dp，可修改dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="target_id">指向目标对象</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirTarget(this ScriptAccessory accessory, uint target_id, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(1f);
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = target_id;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回与某对象目标或某定点相关的dp，可修改dp.TargetResolvePattern, dp.TargetOrderIndex, dp.Owner
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为boss</param>
    /// <param name="order_idx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawTargetOrder(this ScriptAccessory accessory, uint owner_id, uint order_idx, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 40);
        dp.Owner = owner_id;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = order_idx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回与某对象仇恨或距离相关的dp，可修改dp.CentreResolvePattern, dp.CentreOrderIndex, dp.Owner
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为boss</param>
    /// <param name="order_idx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawCenterOrder(this ScriptAccessory accessory, uint owner_id, uint order_idx, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5);
        dp.Owner = owner_id;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = order_idx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己</param>
    /// <param name="target_id">目标单位id</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawOwner2Target(this ScriptAccessory accessory, uint owner_id, uint target_id, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 40);
        dp.Owner = owner_id;
        dp.TargetObject = target_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawCircle(this ScriptAccessory accessory, uint owner_id, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5);
        dp.Owner = owner_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawDonut(this ScriptAccessory accessory, uint owner_id, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(22);
        dp.InnerScale = new(6);
        dp.Radian = float.Pi * 2;
        dp.Owner = owner_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
    /// </summary>
    /// <param name="center">起始位置，通常为场地中心</param>
    /// <param name="radian">旋转角度，以北为0度顺时针</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawStatic(this ScriptAccessory accessory, Vector3 center, float radian, int delay, int destoryAt, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 20);
        dp.Position = center;
        dp.Rotation = DirectionCalc.BaseDirRad2InnGame(radian);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回矩形
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destory">绘图自出现起，经destory ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawRect(this ScriptAccessory accessory, uint owner_id, int width, int length, int delay, int destory, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(width, length);      // 宽度为6，长度为20
        dp.Owner = owner_id;             // 从哪个单位前方绘图
        dp.Color = accessory.Data.DefaultDangerColor;   // 绘图颜色
        dp.Delay = delay;               // 从捕获到对应日志行后，延迟多少毫秒开始绘图
        dp.DestoryAt = destory;        // 从绘图出现后，经过多少毫秒绘图消失
        return dp;
    }

    /// <summary>
    /// 返回扇形
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="radian">扇形弧度</param>
    /// <param name="length">扇形长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destory">绘图自出现起，经destory ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawFan(this ScriptAccessory accessory, uint owner_id, float radian, int length, int delay, int destory, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(length);
        dp.Radian = radian;
        dp.Owner = owner_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destory;
        return dp;
    }

}

#endregion

#region 测试区
// --------------------------------------
public static class ClassTest
{
    public static int Counting = 0;

    public static void classTestMethod(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.SendChat($"/e counting: {Counting++}");
    }
}

// 事件调用
// [ScriptMethod(name: "Test Class", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:133"])]
// public void StartCasting(Event @event, ScriptAccessory accessory) => ClassTest.classTestMethod(@event, accessory);

// List内数据打印
// string rotateDirStr = string.Join(", ", RotateCircleDir);
// DebugMsg(rotateDirStr, accessory);

// CLASS
public class Blade
{
    public UInt32 Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public Blade(UInt32 id, double x, double y, double rotation)
    {
        Id = id;
        X = x;
        Y = y;
        Rotation = rotation;
    }
}

// ConcurrentBag 和 List 的区别：
// Thread Safety: ConcurrentBag is designed to be used in multi-threaded scenarios.
// It allows multiple threads to add and remove items concurrently

// private ConcurrentBag<Blade> blades = new ConcurrentBag<Blade>();
// blades.Add(new Blade(
//     id: Convert.ToUInt32(@event["SourceId"], 16),
//     x: Convert.ToDouble(pos.X),
//     y: Convert.ToDouble(pos.Z),
//     rotation: Convert.ToDouble(@event["SourceRotation"])
// ));

#endregion

#region DEBUG代码段

#region 测试远近
// bool isFarBuster = false;
// bool isLeftSafe = true;
// bool isFrontSafe = false;
// bool isInsideSafe = true;

// P6_DragonsWingAction = [isFarBuster, isLeftSafe, isFrontSafe];
// Vector3[] _NearOrFarSafePos = getQuarterSafePos(P6_DragonsWingAction);
// int[] _NearOrFarDirPosIdx = getQuarterSafePosIdx(P6_DragonsWingAction);

// var myIndex = accessory.getMyIndex();
// var _myPartIdx = myIndex >= 2 ? 2 : myIndex;
// var _targetPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[_myPartIdx]];

// for (int i = 0; i < 3; i++)
// {
//     var _tempPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[i]];
//     var dp0 = accessory.drawStatic(_tempPos, 0, 0, 2000, $"一远近位置{i}");
//     dp0.Scale = new(1f);
//     dp0.Color = i == _myPartIdx ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
//     accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
// }

// var dp = accessory.dirPos(_targetPos, 0, 2000, $"一远近指路");
// accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);


// P6_DragonsWingAction = [isFarBuster, isLeftSafe, isInsideSafe];
// Vector3[] _NearOrFarSafePos = getLineSafePos(P6_DragonsWingAction);
// int[] _NearOrFarDirPosIdx = getLineSafePosIdx(P6_DragonsWingAction);

// var myIndex = accessory.getMyIndex();
// var _myPartIdx = myIndex >= 2 ? 2 : myIndex;
// var _targetPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[_myPartIdx]];

// for (int i = 0; i < 3; i++)
// {
//     var _tempPos = _NearOrFarSafePos[_NearOrFarDirPosIdx[i]];
//     var dp0 = accessory.drawStatic(_tempPos, 0, 0, 2000, $"二远近位置{i}");
//     dp0.Scale = new(1f);
//     dp0.Color = i == _myPartIdx ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
//     accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
// }

// var dp = accessory.dirPos(_targetPos, 0, 2000, $"二远近指路");
// accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
#endregion

#region 测试冰火范围
// Vector3[] _TankBusterPosition = new Vector3[4];
// _TankBusterPosition[0] = new(84.5f, 0, 88f);
// _TankBusterPosition[1] = _TankBusterPosition[0].FoldPointLR(CENTER.X);
// _TankBusterPosition[2] = _TankBusterPosition[0];
// _TankBusterPosition[3] = _TankBusterPosition[1].FoldPointUD(CENTER.Z);

// for (int i = 0; i < 4; i++)
// {
//     var dp = accessory.drawStatic(_TankBusterPosition[i], 0,i*2000, 2000, $"测试范围");
//     dp.Scale = new(15f);
//     dp.Color = ColorHelper.colorRed.V4;
//     accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
// }
#endregion

#endregion