using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
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
using ECommons.MathHelpers;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;

namespace UsamisScript.EndWalker.ASS;

[ScriptType(name: "ASS [异闻希拉狄哈水道]", territorys: [1075, 1076], guid: "bdd73dbd-2a93-4232-9324-0c9093d4a646", version: "0.0.1.4", author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

public class ASS
{
    private const string NoteStr =
    """
    请先按需求检查并设置“用户设置”栏目，
    并在/KTeam中设置 T>H>近>远。
    """;
    
    private const string UpdateInfo =
        """
        1. 删除了用户设置中的DebugMode。
        2. 再次修复了Boss2 金银咒象 金银Buff站位错误的Bug。
        """;
    
    private const bool Debugging = false;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("Boss1 泡泡危险范围颜色使用默认设置")]
    public static bool Boss1_DefaultDangerColor { get; set; } = false;

    [UserSetting("Boss1 泡泡危险范围颜色设置")]
    public static ScriptColor Boss1_SetDangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 0.0f, 1.0f, 1.0f) };

    [UserSetting("Boss3 场地危险范围颜色使用默认设置")]
    public static bool Boss3_DefaultDangerColor { get; set; } = false;

    [UserSetting("Boss3 场地危险范围颜色设置")]
    public static ScriptColor Boss3_SetDangerColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0.0f, 1.0f) };
    public enum ASS_Phase
    {
        // TODO：存在的问题是，团灭后，副本不会重置！Init函数不奏效！
        // TODO：需要找到一个方法，检测到团灭，然后重新执行Init
        
        Init,                   // 初始
        BOSS1_P2,               // BOSS1 一染
        BOSS1_P3,               // BOSS1 一拉
        BOSS1_P4,               // BOSS1 二染
        BOSS1_P5,               // BOSS1 二拉
        BOSS3_P1,               // BOSS3 咒具一
        BOSS3_P2,               // BOSS3 咒具二
        BOSS3_P3,               // BOSS3 咒具三
        BOSS3_P4,               // BOSS3 咒具四
        BOSS3_P5,               // BOSS3 咒具五
    }
    ASS_Phase phase = ASS_Phase.Init;
    List<bool> Drawn = new bool[20].ToList();                   // 绘图记录
    static Vector3 CENTER_BOSS1 = new Vector3(-335f, 471f, -155f);     // BOSS1 场地中心
    static Vector3 CENTER_BOSS2 = new Vector3(-35f, 521f, -271f);      // BOSS2 场地中心
    static Vector3 CENTER_BOSS3 = new Vector3(289f, 533f, -105f);      // BOSS3 场地中心
    const int FIELDX_BOSS3 = 30;                                // BOSS3 场地X总长
    const int FIELDZ_BOSS3 = 40;                                // BOSS3 场地Z总长
    
    uint Boss1_BubbleProperty = 0;                              // BOSS1 鼠鼠的泡泡属性
    List<uint> Boss1_P2_FieldBubbleProperty = [0, 0, 0];        // BOSS1 P2一染，场地泡泡属性记录
    List<uint> Boss1_P2_FieldBubbleSid = [0, 0, 0];             // BOSS1 P2一染，场地泡泡ID记录
    List<uint> Boss1_P3_FieldBubbleProperty = [0, 0, 0, 0];     // BOSS1 P3一拉，场地泡泡属性记录
    List<uint> Boss1_P3_FieldBubbleSid = [];                    // BOSS1 P3一拉，场地泡泡ID记录
    List<bool> Boss1_P4_WaterLine = new bool[4].ToList();       // BOSS1 P4二染，场地水壶记录
    
    List<uint> Boss2_CurseBuff = [0, 0, 0, 0];                  // BOSS2 记录分摊/天光轮回buff
    List<bool> Boss2_isLongBuff = [false, false, false, false]; // BOSS2 记录长/短Buff
    List<int> Boss2_GoldenSilverBuff = [0, 0, 0, 0];            // BOSS2 记录金银Buff
    List<List<int>> Boss2_GoldenSilverField = [[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]]; // BOSS2 金银Buff场地计算
    
    List<int> Boss3_StrikeTarget = [0, 0];                      // BOSS3 石火豪冲（被挡枪目标）记录
    List<int> Boss3_P2_PlayerBuff = [0, 0, 0, 0];               // BOSS3 P2麻将记录
    List<float> Boss3_P4_BrandRot = [0, 0, 0];                  // BOSS3 咒具面向逻辑角度记录
    List<uint> Boss3_P4_PanelSid = [0, 0, 0];                   // BOSS3 魔法阵ID记录
    int Boss3_P3_myRotateRow = 0;                               // BOSS3 旋转buff决定所在行
    List<Vector3> Boss3_P3_safeTeleportPos =
    [
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 0)
    ];        // BOSS3 咒具旋转安全位置
    List<Vector3> Boss3_P3_BaitPos =
    [
        new Vector3(289f, 533f, -119.5f),
        new Vector3(297f, 533f, -111.75f),
        new Vector3(297f, 533f, -111.75f).FoldPointUD(CENTER_BOSS3.Z).FoldPointLR(CENTER_BOSS3.X),
        new Vector3(289f, 533f, -119.5f).FoldPointUD(CENTER_BOSS3.Z),
    ];        // BOSS3 引导斧头安全位置

    public void Init(ScriptAccessory accessory)
    {
        ParamsInit(accessory);
    }

    public void ParamsInit(ScriptAccessory accessory)
    {
        phase = ASS_Phase.Init;
        Drawn = new bool[20].ToList();
        
        Boss1_BubbleProperty = 0;                           // BOSS1 鼠鼠的泡泡属性
        Boss1_P2_FieldBubbleProperty = [0, 0, 0];           // BOSS1 P2一染，场地泡泡属性记录
        Boss1_P2_FieldBubbleSid = [0, 0, 0];                // BOSS1 P2一染，场地泡泡ID记录
        Boss1_P3_FieldBubbleProperty = [0, 0, 0, 0];        // BOSS1 P3一拉，场地泡泡属性记录
        Boss1_P3_FieldBubbleSid = [];                       // BOSS1 P3一拉，场地泡泡ID记录
        Boss1_P4_WaterLine = new bool[4].ToList();          // BOSS1 P4二染，场地水壶记录

        Boss2_CurseBuff = [0, 0, 0, 0];                     // BOSS2 记录分摊/天光轮回buff
        Boss2_isLongBuff = [false, false, false, false];    // BOSS2 记录长/短Buff
        Boss2_GoldenSilverBuff = [0, 0, 0, 0];              // BOSS2 记录金银Buff
        Boss2_GoldenSilverField = [[0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0], [0, 0, 0, 0]];     // BOSS2 金银Buff场地计算

        Boss3_StrikeTarget = [0, 0];                        // BOSS3 石火豪冲（被挡枪目标）记录
        Boss3_P2_PlayerBuff = [0, 0, 0, 0];                 // BOSS3 P2麻将记录
        Boss3_P4_BrandRot = [0, 0, 0];                      // BOSS3 咒具面向逻辑角度记录
        Boss3_P4_PanelSid = [0, 0, 0];                      // BOSS3 魔法阵ID记录
        Boss3_P3_myRotateRow = 0;                           // BOSS3 旋转buff决定所在行

        Boss3_P3_safeTeleportPos =
        [
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 0)
        ];        // BOSS3 咒具旋转安全位置
        Boss3_P3_BaitPos =
        [
            new Vector3(289f, 533f, -119.5f),
            new Vector3(297f, 533f, -111.75f),
            new Vector3(297f, 533f, -111.75f).FoldPointUD(CENTER_BOSS3.Z).FoldPointLR(CENTER_BOSS3.X),
            new Vector3(289f, 533f, -119.5f).FoldPointUD(CENTER_BOSS3.Z),
        ];        // BOSS3 引导斧头安全位置
        
        accessory.Method.RemoveDraw(".*");
    }
    
    [ScriptMethod(name: "测试项 - 获得玩家位置", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: Debugging)]
    public void CheckPlayerRole(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"玩家位置：{sa.getMyIndex()}");
    }
    
    [ScriptMethod(name: "测试项 - 获得金银Buff场地", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: Debugging)]
    public void CheckGoldenSilverField(Event ev, ScriptAccessory sa)
    {
        var a = Boss2_GoldenSilverField;
        var str = "";
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                str += Boss2_GoldenSilverField[i][j].ToString();
                str += "\t";
            }
            str += "\n";
        }
        sa.Log.Debug($"---- 金银场地 ----\n{str}");
    }
    
    [ScriptMethod(name: "测试项 - 获得金银Buff站位", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: Debugging)]
    public void CheckGoldenSilverSafeField(Event ev, ScriptAccessory sa)
    {
        int[] high = findGoldenSilverIndex(11, true);
        int[] low = findGoldenSilverIndex(11, false);
        sa.Log.Debug($"高优先级金银 {high[0]}行{high[1]}列；低优先级金银 {low[0]}行{low[1]}列");
    }
    
    [ScriptMethod(name: "团灭初始化", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(1483[47]|14757|14864|14762|14817)$"], userControl: Debugging, suppress: 3000)]
    public void WipeInit(Event @event, ScriptAccessory accessory)
    {
        var bossId = @event.DataId();
        const uint nSilkie = 14834;
        const uint sSilkie = 14837;
        const uint nGladiator = 14757;
        const uint sGladiator = 14864;
        const uint nShadowCaster = 14762;
        const uint sShadowCaster = 14817;

        var bossName = bossId switch
        {
            nSilkie => "Boss1 鼠鼠",
            sSilkie => "Boss1 零式鼠鼠",
            nGladiator => "Boss2 守护者",
            sGladiator => "Boss2 零式守护者",
            nShadowCaster => "Boss3 影火",
            sShadowCaster => "Boss3 零式影火",
            _ => "未知"
        };
        
        ParamsInit(accessory);
        accessory.Log.Debug($"检测到{bossName}的刷新，参数初始化。");
    }

    #region Mob1

    [ScriptMethod(name: "Mob1：树人", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3107[567]|31099|3110[01])$"])]
    public void Mob1_Kaluk(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> LEFT_CLEAVE = [31076, 31100];
        HashSet<uint> RIGHT_CLEAVE = [31075, 31099];
        HashSet<uint> FRONT_CLEAVE = [31077, 31101];

        var sid = @event.SourceId();
        var aid = @event.ActionId();

        var _scale = FRONT_CLEAVE.Contains(aid) ? 10 : 30;
        var _radian = FRONT_CLEAVE.Contains(aid) ? float.Pi / 180 * 90 : float.Pi / 180 * 210;
        var _rotation = FRONT_CLEAVE.Contains(aid) ? 0 :
                        LEFT_CLEAVE.Contains(aid) ? float.Pi / 180 * 75 : -float.Pi / 180 * 75;

        var dp = accessory.drawFan(sid, _radian, _scale, 250, 3750, $"树人刀");
        // dp.Rotation正值为逆时针
        dp.Rotation = _rotation;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Mob1：花", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3107[23]|3109[67])$"])]
    public void Mob1_Belladonna(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> DONUT = [31072, 31096];
        HashSet<uint> SIGHT = [31073, 31097];

        var sid = @event.SourceId();
        var aid = @event.ActionId();

        if (DONUT.Contains(aid))
        {
            var dp = accessory.drawDonut(sid, 0, 4000, $"花月环");
            dp.InnerScale = new(9f);
            dp.Scale = new(40f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
        else if (SIGHT.Contains(aid))
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"花背对";
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetObject = sid;
            dp.Delay = 0;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
        }
        else return;
    }

    [ScriptMethod(name: "Mob1：三头龙", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3106[789]|3109[123])$"])]
    public void Mob1_Udumbara(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> LEFT_CLEAVE = [31067, 31091];
        HashSet<uint> RIGHT_CLEAVE = [31068, 31092];
        HashSet<uint> FRONT_CLEAVE = [31069, 31093];

        var sid = @event.SourceId();
        var aid = @event.ActionId();

        var _scale = 30;
        var _radian = FRONT_CLEAVE.Contains(aid) ? float.Pi / 180 * 120 : float.Pi / 180 * 180;
        var _rotation = FRONT_CLEAVE.Contains(aid) ? 0 :
                        LEFT_CLEAVE.Contains(aid) ? float.Pi / 180 * 135 : -float.Pi / 180 * 135;

        var dp = accessory.drawFan(sid, _radian, _scale, 250, 3750, $"三头龙刀");
        dp.Rotation = _rotation;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Mob1：大树", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31063|31087)$"])]
    public void Mob1_Dryad(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var _scale = 12;
        var dp = accessory.drawCircle(sid, 0, 4250, $"大树钢铁");
        dp.Scale = new(_scale);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region BOSS1 鼠鼠

    [ScriptMethod(name: "BOSS1：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30566|30601)$"])]
    public void Boss1_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            ASS_Phase.Init => ASS_Phase.BOSS1_P2,
            ASS_Phase.BOSS1_P2 => ASS_Phase.BOSS1_P3,
            ASS_Phase.BOSS1_P3 => ASS_Phase.BOSS1_P4,
            ASS_Phase.BOSS1_P4 => ASS_Phase.BOSS1_P5,
            _ => ASS_Phase.BOSS1_P2
        };
        accessory.Log.Debug($"当前阶段为：{phase}");
    }

    [ScriptMethod(name: "BOSS1：泡泡范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3055[567]|3056[89]|30570|3059[012]|3060[345])$"])]
    public void Boss1_Bubbles(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();

        HashSet<uint> LIGHT_AIDS = [30557, 30570, 30592, 30605];
        HashSet<uint> WIND_AIDS = [30556, 30569, 30591, 30604];
        HashSet<uint> ICE_AIDS = [30555, 30568, 30590, 30603];

        if (LIGHT_AIDS.Contains(aid))
            drawLightFan(sid, true, accessory);
        if (WIND_AIDS.Contains(aid))
            drawWindDonut(sid, true, accessory);
        if (ICE_AIDS.Contains(aid))
            drawIcePlusShaped(sid, true, accessory);
    }

    private DrawPropertiesEdit drawLightFan(uint sid, bool draw, ScriptAccessory accessory)
    {
        var _scale = 60;
        var _radian = float.Pi / 180 * 45;
        var dp = accessory.drawFan(sid, _radian, _scale, 0, 5000, $"雷泡泡四角");
        dp.Color = Boss1_DefaultDangerColor ? accessory.Data.DefaultDangerColor : Boss1_SetDangerColor.V4;
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        return dp;
    }

    private void drawLightSpread(uint sid, ScriptAccessory accessory)
    {
        var _scale = 5;
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = accessory.drawCircle(accessory.Data.PartyList[i], 6000, 3000, $"雷滑行分散");
            dp.Scale = new(_scale);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        
        List<int> dirPosition = [2, 0, 1, 3];
        var myIndex = accessory.getMyIndex();
        for (int i = 0; i < dirPosition.Count(); i++)
        {
            var dp = accessory.drawRect(sid, 2, 10, 7000, 2000, $"分散位置指引{i}");
            dp.Rotation = dirPosition[i] * float.Pi / 2;
            dp.Color = i == myIndex ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
            dp.Scale = new Vector2(4f, 20f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    private DrawPropertiesEdit drawWindDonut(uint sid, bool draw, ScriptAccessory accessory)
    {
        var _scale = 60;
        var _innerscale = 5;
        var dp = accessory.drawDonut(sid, 0, 5000, $"风泡泡月环");
        dp.Color = Boss1_DefaultDangerColor ? accessory.Data.DefaultDangerColor : Boss1_SetDangerColor.V4;
        dp.Scale = new(_scale);
        dp.InnerScale = new(_innerscale);
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        return dp;
    }

    private DrawPropertiesEdit[] drawIcePlusShaped(uint sid, bool draw, ScriptAccessory accessory)
    {
        var _scale = 10;
        var _rotation = float.Pi / 180 * 90;

        var dp0 = accessory.drawRect(sid, _scale, 90, 0, 5000, $"冰泡泡十字");
        dp0.Color = Boss1_DefaultDangerColor ? accessory.Data.DefaultDangerColor : Boss1_SetDangerColor.V4;
        dp0.Rotation = _rotation;
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp0);

        var dp1 = accessory.drawRect(sid, _scale, 90, 0, 5000, $"冰泡泡十字");
        dp1.Color = Boss1_DefaultDangerColor ? accessory.Data.DefaultDangerColor : Boss1_SetDangerColor.V4;
        dp1.Rotation = 0;
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);

        return [dp0, dp1];
    }

    [ScriptMethod(name: "BOSS1：擦拭", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3054[56]|3058[01])$"])]
    public void Boss1_SqueakyClean(Event @event, ScriptAccessory accessory)
    {
        // LEFT DOWN是左边安全，打右边
        HashSet<uint> LEFT_DOWN = [30545, 30580];
        HashSet<uint> RIGHT_DOWN = [30546, 30581];

        var sid = @event.SourceId();
        var aid = @event.ActionId();

        var _scale = 60;
        var _radian = float.Pi / 180 * 225;
        var _rotation = LEFT_DOWN.Contains(aid) ? -float.Pi / 180 * 67.5f : float.Pi / 180 * 67.5f;

        var dp = accessory.drawFan(sid, _radian, _scale, 0, 9000, $"擦拭");
        // dp.Rotation正值为逆时针
        dp.Rotation = _rotation;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    HashSet<uint> LIGHT_BUBBLE = [30553, 30588];
    HashSet<uint> ICE_BUBBLE = [30552, 30587];
    HashSet<uint> WIND_BUBBLE = [30551, 30586];
    [ScriptMethod(name: "BOSS1：泡泡属性记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3055[123]|3058[678])$"], userControl: false)]
    public void Boss1_BubblePropertyRecord(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        Boss1_BubbleProperty = aid;
    }

    [ScriptMethod(name: "BOSS1：泡泡滑行", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30558|30593)$"])]
    public void Boss1_SlipperySoap(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        DrawPropertiesEdit?[] dps;
        DrawTypeEnum _type;

        if (LIGHT_BUBBLE.Contains(Boss1_BubbleProperty))
        {
            // 雷泡泡叉字后带分散
            dps = [drawLightFan(sid, false, accessory), null];
            _type = DrawTypeEnum.Fan;
            drawLightSpread(sid, accessory);
        }
        else if (ICE_BUBBLE.Contains(Boss1_BubbleProperty))
        {
            dps = drawIcePlusShaped(sid, false, accessory);
            _type = DrawTypeEnum.Straight;
            accessory.Method.TextInfo($"保持移动", 4000, true);
        }
        else if (WIND_BUBBLE.Contains(Boss1_BubbleProperty))
        {
            // 风泡泡画月环是为了提醒击退
            dps = [drawWindDonut(sid, true, accessory), null];
            _type = DrawTypeEnum.Donut;
        }
        else
            return;

        for (int i = 0; i < 2; i++)
        {
            if (dps[i] == null) continue;
            dps[i].Delay = 6000;
            dps[i].DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, _type, dps[i]);
        }
    }

    [ScriptMethod(name: "BOSS1：一染色球名称记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:regex:^(4561)$", "SourceDataId:regex:^(1483[58])$"], userControl: false)]
    public void Boss1_P2_BubbleSidRecord(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var sid = @event.SourceId();

        if (phase == ASS_Phase.BOSS1_P2)
        {
            if (spos.X < CENTER_BOSS1.X - 5)
                Boss1_P2_FieldBubbleSid[0] = sid;
            else if (spos.X > CENTER_BOSS1.X + 5)
                Boss1_P2_FieldBubbleSid[2] = sid;
            else
                Boss1_P2_FieldBubbleSid[1] = sid;
        }

        if (phase == ASS_Phase.BOSS1_P3)
        {
            Boss1_P3_FieldBubbleSid.Add(sid);
        }

    }

    [ScriptMethod(name: "BOSS1：一染色提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(330[567])$"])]
    public async void Boss1_P2_PrintBubble(Event @event, ScriptAccessory accessory)
    {
        const uint WIND = 3305;
        const uint ICE = 3306;
        const uint LIGHT = 3307;

        if (phase != ASS_Phase.BOSS1_P2) return;
        await Task.Delay(500);  // 等待SID记录

        var tid = @event.TargetId();
        if (!Boss1_P2_FieldBubbleSid.Contains(tid)) return;
        var _idx = Boss1_P2_FieldBubbleSid.IndexOf(tid);

        var stid = @event.StatusID();
        Boss1_P2_FieldBubbleProperty[_idx] = stid;

        await Task.Delay(100);  // 等待集合数值改变
        if (Boss1_P2_FieldBubbleProperty.Count(x => x == WIND) != 2) return;

        var _spec_idx = Boss1_P2_FieldBubbleProperty.IndexOf(x => x != WIND);
        int _tidx;
        if (Boss1_P2_FieldBubbleProperty[_spec_idx] == ICE)
            // 剩冰球，去下半场另一只染色
            _tidx = Math.Abs(2 - _spec_idx);
        else
            // 剩电球，去上半场染色
            _tidx = 1;

        var dp = accessory.drawRect(Boss1_P2_FieldBubbleSid[_tidx], 10, 20, 0, 10000, $"一染色提示");
        dp.TargetPosition = CENTER_BOSS1;
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "BOSS1：一拉球记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(330[567])$"], userControl: false)]
    public async void Boss1_P3_GrabBubbleRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS1_P3) return;
        await Task.Delay(500);  // 等待SID记录

        var tid = @event.TargetId();
        if (!Boss1_P3_FieldBubbleSid.Contains(tid)) return;
        var _idx = Boss1_P3_FieldBubbleSid.IndexOf(tid);

        var stid = @event.StatusID();
        Boss1_P3_FieldBubbleProperty[_idx] = stid;
    }

    [ScriptMethod(name: "BOSS1：一拉球提示", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(00D8)$"])]
    public void Boss1_P3_GrabBubble(Event @event, ScriptAccessory accessory)
    {
        const uint WIND = 3305;
        const uint ICE = 3306;
        const uint LIGHT = 3307;
        if (phase != ASS_Phase.BOSS1_P3) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;

        var sid = @event.SourceId();
        var spos = @event.SourcePosition();

        // 泡泡们默认朝向为下

        // 先确认与自己相连的泡泡属性
        var _bubble_idx = Boss1_P3_FieldBubbleSid.IndexOf(sid);
        bool _is_light_bubble = Boss1_P3_FieldBubbleProperty[_bubble_idx] == LIGHT;

        if (_is_light_bubble)
            // 电球，无脑向外指
            grabBubbleToOut(spos, 0, 10000, accessory);
        else
        {
            // 冰球，看BOSS
            if (ICE_BUBBLE.Contains(Boss1_BubbleProperty))
                // 如果BOSS是冰球，无脑向外指
                grabBubbleToOut(spos, 0, 10000, accessory);
            else
            {
                // 如果BOSS是电球，视位置拉
                grabBubbleToPos(spos, 0, 10000, accessory);
            }
        }
    }

    private void grabBubbleToOut(Vector3 bubble_pos, int delay, int destory, ScriptAccessory accessory)
    {
        var _dir = bubble_pos.PositionRoundToDirs(CENTER_BOSS1, 8);
        var _rot_radian = _dir * float.Pi / 180 * 45;
        var _spos = bubble_pos.ExtendPoint(_rot_radian, 5);
        var dp0 = accessory.drawStatic(_spos, 0, delay, destory, $"一拉球提示");
        dp0.Color = posColorPlayer.V4;
        dp0.Scale = new(1f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

        var dp = accessory.dirPos(_spos, delay, destory, $"一拉球指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private void grabBubbleToPos(Vector3 bubble_pos, int delay, int destory, ScriptAccessory accessory)
    {
        var _dir = bubble_pos.PositionRoundToDirs(CENTER_BOSS1, 8);
        // 不唯一，但简单
        int[] _rot_dir = [2, 2, 0, 2, 2, 6, 0, 0];
        var _rot_radian = _rot_dir[_dir] * float.Pi / 180 * 45;
        var _spos = bubble_pos.ExtendPoint(_rot_radian, 5);
        var dp0 = accessory.drawStatic(_spos, 0, delay, destory, $"一拉球提示");
        dp0.Color = posColorPlayer.V4;
        dp0.Scale = new(1f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

        var dp = accessory.dirPos(_spos, delay, destory, $"一拉球指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "BOSS1：一拉球提示移除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(577|612|656))$"], userControl: false)]
    public void Boss1_P3_GrabBubbleRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS1_P3) return;
        accessory.Method.RemoveDraw($"一拉球提示");
        accessory.Method.RemoveDraw($"一拉球指路");
    }

    [ScriptMethod(name: "BOSS1：二染水壶位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(574|609))$"], userControl: false)]
    public void Boss1_P4_WaterRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS1_P4) return;
        var spos = @event.SourcePosition();
        // -347, -339, [-335], -331, -323,
        // -43, -42, -41, -40
        int _idx = (int)(spos.X / 8) + 43;
        accessory.Log.Debug($"检测到水壶出现在第{_idx + 1}列");
        Boss1_P4_WaterLine[_idx] = true;
    }

    [ScriptMethod(name: "BOSS1：二染死刑位置提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(543|578))$"])]
    public void Boss1_P4_TankBusterDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS1_P4) return;
        if (!IbcHelper.isTank(accessory.Data.Me)) return;
        // 没有被清掉的一侧为少
        var _isleft = Boss1_P4_WaterLine.IndexOf(false) > 1;
        accessory.Log.Debug($"第{Boss1_P4_WaterLine.IndexOf(false)}列被移除，T需去{(_isleft ? "左" : "右")}引导。");
        Vector3 _pos = new Vector3(-350, 471, -155);    // 左侧
        if (!_isleft)
            _pos = _pos.FoldPointLR(CENTER_BOSS1.X);

        var dp0 = accessory.drawStatic(_pos, 0, 0, 10000, $"二染死刑位置提示");
        dp0.Color = posColorPlayer.V4;
        dp0.Scale = new(1f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

        var dp = accessory.dirPos(_pos, 0, 10000, $"二染死刑位置指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "BOSS1：二染死刑位置提示移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(30(543|578))$"])]
    public void Boss1_P4_TankBusterDirRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS1_P4) return;
        accessory.Method.RemoveDraw($"二染死刑位置提示");
        accessory.Method.RemoveDraw($"二染死刑位置指路");
    }

    #endregion

    #region Mob2

    [ScriptMethod(name: "Mob2：瘦子", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31083|31107)$"])]
    public void Mob2_Armor(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        // 此处月环意为：续剑扇形，要么出去，要么靠近
        var _scale = 12;
        var _innerscale = 2.5f;
        var dp = accessory.drawDonut(sid, 0, 5000, $"瘦子加重");
        dp.Scale = new(_scale);
        dp.InnerScale = new(_innerscale);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    [ScriptMethod(name: "Mob2：胖子", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31078|31102)$"])]
    public void Mob2_Dullahan(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var _scale = 10;
        var dp = accessory.drawCircle(sid, 0, 4000, $"胖子钢铁");
        dp.Scale = new(_scale);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region BOSS2 斗士

    [ScriptMethod(name: "Boss2：冲锋前后刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3029[678]|3061[89]|30620)$"])]
    public void Boss2_RushCleave(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> ONE = [30296, 30618];
        HashSet<uint> TWO = [30297, 30619];
        HashSet<uint> THREE = [30298, 30620];

        var aid = @event.ActionId();
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();

        var _rushDistance = ONE.Contains(aid) ? 20f :
                            TWO.Contains(aid) ? 27.5f :
                            THREE.Contains(aid) ? 35f : 20f;

        var _radian = @event.SourceRotation().BaseInnGame2DirRad();
        var _target_pos = spos.ExtendPoint(_radian, _rushDistance);

        var dp = accessory.drawStatic(_target_pos, 0, 0, 10500, $"冲锋刀1");
        dp.Scale = new(60, 60);
        dp.Rotation = srot;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        var dp2 = accessory.drawStatic(_target_pos, 0, 10500, 2000, $"冲锋刀2");
        dp2.Scale = new(60, 60);
        dp2.Rotation = srot + float.Pi;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    [ScriptMethod(name: "Boss2：挡枪分摊提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30316|30638)$"])]
    public void Boss2_WildCharge(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.TextInfo($"坦克挡枪分摊", 4000, true);
    }

    const uint CURSE_SPREAD = 3290;
    const uint CURSE_STACK = 3293;
    const uint CURSE_BAIT = 3292;

    [ScriptMethod(name: "Boss2：残响Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(329[023])$"], userControl: false)]
    public void Boss2_Curse_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        var stid = @event.StatusID();
        var tid = @event.TargetId();
        var dur = @event.DurationMilliseconds();
        var tidx = accessory.getPlayerIdIndex(tid);

        Boss2_isLongBuff[tidx] = dur > 15000;
        if (stid != CURSE_SPREAD)
            Boss2_CurseBuff[tidx] = stid;
    }

    [ScriptMethod(name: "Boss2：残响Buff绘图", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(329[023])$"])]
    public async void Boss2_CurseDraw(Event @event, ScriptAccessory accessory)
    {
        var stid = @event.StatusID();
        var tid = @event.TargetId();
        var dur = @event.DurationMilliseconds();
        var _myIndex = accessory.getMyIndex();
        var _scale = stid switch
        {
            CURSE_SPREAD => 6,
            CURSE_STACK => 5,
            CURSE_BAIT => 5,
            _ => 5,
        };

        await Task.Delay(100);
        var _delay = (int)dur - 3000 - 100;
        var _destory = 3000;
        var dp = accessory.drawCircle(tid, _delay, _destory, $"残响{stid}");
        dp.Scale = new(_scale);

        if (stid == CURSE_BAIT)
            dp.Color = ColorHelper.colorRed.V4.WithW(2f);
        else
            dp.Color = (Boss2_CurseBuff[_myIndex] != CURSE_BAIT && stid == CURSE_STACK) ? accessory.Data.DefaultSafeColor.WithW(2f) : accessory.Data.DefaultDangerColor.WithW(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Boss2：蓄力钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3030[123456]|3062[345678])$"])]
    public void Boss2_ChariotDonut(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> CHARIOT_1 = [30301, 30623];
        HashSet<uint> CHARIOT_2 = [30302, 30624];
        HashSet<uint> CHARIOT_3 = [30303, 30625];

        HashSet<uint> DONUT_1 = [30304, 30626];
        HashSet<uint> DONUT_2 = [30305, 30627];
        HashSet<uint> DONUT_3 = [30306, 30628];

        var aid = @event.ActionId();
        var sid = @event.SourceId();
        DrawTypeEnum _type = CHARIOT_1.Contains(aid) || CHARIOT_2.Contains(aid) || CHARIOT_3.Contains(aid) ?
                                DrawTypeEnum.Circle : DrawTypeEnum.Donut;

        var _innerscale = CHARIOT_1.Contains(aid) || DONUT_1.Contains(aid) ? 8f :
                          CHARIOT_2.Contains(aid) || DONUT_2.Contains(aid) ? 13f :
                          CHARIOT_3.Contains(aid) || DONUT_3.Contains(aid) ? 18f : 8f;

        if (_type == DrawTypeEnum.Circle)
        {
            var dp = accessory.drawCircle(sid, 0, 10000, $"刚武旋击钢铁");
            accessory.Log.Debug($"刚武旋击钢铁 {_innerscale} 技能ID {aid}");
            dp.Scale = new(_innerscale);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, _type, dp);
        }
        else
        {
            var dp = accessory.drawDonut(sid, 10000, 2000, $"刚武旋击月环");
            accessory.Log.Debug($"刚武旋击月环 {_innerscale} 技能ID {aid}");
            dp.InnerScale = new(_innerscale);
            dp.Scale = new(30f);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, _type, dp);
        }

        List<int> dirPosition = [0, 2, 3, 1];
        var myIndex = accessory.getMyIndex();
        for (int i = 0; i < dirPosition.Count(); i++)
        {
            var dp = accessory.dirPos2Pos(CENTER_BOSS2, CENTER_BOSS2.ExtendPoint(dirPosition[i] * float.Pi / 2, 20), 0,
                8000, $"十字站位指引{i}");
            dp.Color = i == myIndex ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
            dp.Scale = new Vector2(4f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    HashSet<uint> GOLDEN_BEAM = [30319, 30641];
    HashSet<uint> SILVER_BEAM = [30320, 30642];
    const uint GOLDEN_BUFF = 3295;
    const uint SILVER_BUFF = 3296;

    [ScriptMethod(name: "Boss2：金银Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(329[56])$"], userControl: false)]
    public void Boss2_GoldenSilver_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        var stid = @event.StatusID();
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        var param = @event.Param();

        // 此处金BUFF+1，银BUFF+10，与场地的金炮+10，银炮+1可反向对应
        int _dx = stid == GOLDEN_BUFF ? 1 : 10;
        Boss2_GoldenSilverBuff[tidx] = Boss2_GoldenSilverBuff[tidx] + (int)param * _dx;

        if (stid != CURSE_SPREAD)
            Boss2_CurseBuff[tidx] = stid;
    }

    [ScriptMethod(name: "Boss2：金银射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(303(19|20)|3064[12])$"])]
    public async void Boss2_GoldenSilverBeam(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();

        Vector4 _color = accessory.Data.DefaultDangerColor;

        if (Boss2_GoldenSilverBuff.All(x => x == 0))
        {
            // 无金银Buff时
            // 显示时间无需过长，给九字切让路
            var dp = accessory.drawRect(sid, 10, 40, 0, 8400, $"射线{aid}");
            dp.Color = _color;
            dp.Rotation = SILVER_BEAM.Contains(aid) ? float.Pi : 0;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            return;
        }

        // 有金银Buff时
        bool _is_golden = GOLDEN_BEAM.Contains(aid);
        // _color = _is_golden ? ColorHelper.colorYellow.V4 : ColorHelper.colorWhite.V4;
        var _idx = calcGoldenSilverRowCol(spos);
        var _face_dir = calcGoldenSilverFaceDir(srot);
        calcGoldenSilverField(_idx, _face_dir, _is_golden);

        // 安全格高亮与指路
        if (Drawn[0]) return;
        Drawn[0] = true;
        await Task.Delay(100);

        var myIndex = accessory.getMyIndex();
        // 11代表金银各1
        accessory.Log.Debug($"根据顺序，金银Buff为：{Boss2_GoldenSilverBuff[0]}, {Boss2_GoldenSilverBuff[1]}, {Boss2_GoldenSilverBuff[2]}, {Boss2_GoldenSilverBuff[3]},");
        if (Boss2_GoldenSilverBuff[myIndex] != 11)
        {
            int[] _idxs = findGoldenSilverIndex(Boss2_GoldenSilverBuff[myIndex], true);
            drawSafeSquare(_idxs[0], _idxs[1], accessory);
        }
        else
        {
            int[] _priority = [0, 3, 1, 2];   // T 近 远 奶
            var _my_prior = _priority[myIndex];
            var _other_idx = Boss2_GoldenSilverBuff.IndexOf(11) != myIndex ? Boss2_GoldenSilverBuff.IndexOf(11) : Boss2_GoldenSilverBuff.LastIndexOf(11);
            var _other_prior = _priority[_other_idx];
            var _is_first = _my_prior < _other_prior;
            accessory.Log.Debug($"我({myIndex})的优先级为 {_my_prior}，另一人({_other_idx})的优先级为 {_other_prior}，我isFirst {_is_first}");
            int[] _idxs = findGoldenSilverIndex(Boss2_GoldenSilverBuff[myIndex], _is_first);
            drawSafeSquare(_idxs[0], _idxs[1], accessory);
        }
    }

    [ScriptMethod(name: "Boss2：隆起分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30348|30653)$"])]
    public void Boss2_EarthSpread(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.drawCircle(tid, 3000, 2000, $"隆起{tid}");
        dp.Scale = new(8f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "Boss2：大残响Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3291)$"], userControl: false)]
    public void Boss2_HugeCurse_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        var dur = @event.DurationMilliseconds();
        Boss2_isLongBuff[tidx] = dur > 20000;
    }

    [ScriptMethod(name: "Boss2：大残响范围绘图", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3291)$"])]
    public void Boss2_HugeCurseDraw(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dur = @event.DurationMilliseconds();
        var _delay = dur - 4000;
        var _destory = 4000;

        var dp = accessory.drawCircle(tid, (int)_delay, _destory, $"大残响{tid}");
        dp.Scale = new(15f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private int calcGoldenSilverRowCol(Vector3 pos)
    {
        // 1行 -35 521 -286
        // 2行 -35 521 -276
        // 3行 -35 521 -266
        // 4行 -35 521 -256

        // 1列 -50 521 -271
        // 2列 -40 521 -271
        // 3列 -30 521 -271
        // 4列 -20 521 -271
        int idx;
        bool isHorizon = Math.Abs(pos.Z - -271) < 1.5f;
        if (isHorizon)
            idx = (int)((pos.X + 51) / 10);
        else
            idx = (int)((pos.Z + 287) / 10);
        return idx;
    }

    private int calcGoldenSilverFaceDir(float rot)
    {
        int dir = (int)Math.Round(rot.BaseInnGame2DirRad() / (float.Pi / 2));
        return dir;
    }

    /// <summary>
    /// 计算金银激光对场地的影响
    /// </summary>
    /// <param name="idx">第几行/第几列</param>
    /// <param name="face_dir">面向方向（0上1右2下3左）</param>
    /// <param name="is_golden">是否为金炮</param>
    private void calcGoldenSilverField(int idx, int face_dir, bool is_golden)
    {
        // 金+10，银+1，分别找11、20、2。
        if (face_dir % 2 == 0)
        {
            // 如果face_dir是偶数，必是水平雕像，影响竖格
            var _col = idx;
            var _row = (face_dir + 2 * (is_golden ? 0 : 1)) % 4;
            // [_row][_col], [_row+1][_col]
            Boss2_GoldenSilverField[_row][_col] += is_golden ? 10 : 1;
            Boss2_GoldenSilverField[_row + 1][_col] += is_golden ? 10 : 1;
        }
        else
        {
            var _row = idx;
            var _col = (face_dir + 1 + 2 * (is_golden ? 0 : 1)) % 4;
            // [_row][_col], [_row][_col+1]
            Boss2_GoldenSilverField[_row][_col] += is_golden ? 10 : 1;
            Boss2_GoldenSilverField[_row][_col + 1] += is_golden ? 10 : 1;
        }
    }
    private void drawSafeSquare(int row, int col, ScriptAccessory accessory)
    {
        // 1行 -35 521 -286
        // 2行 -35 521 -276
        // 3行 -35 521 -266
        // 4行 -35 521 -256

        // 1列 -50 521 -271
        // 2列 -40 521 -271
        // 3列 -30 521 -271
        // 4列 -20 521 -271
        var _x = -50 + col * 10;
        var _z = -286 + row * 10;
        Vector3 pos = new Vector3(_x, 521f, _z);
        var dp = accessory.drawStatic(pos, 0, 0, 7500, $"金银射线安全{row}{col}");
        dp.Scale = new(10, 10);
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);

        var dp0 = accessory.dirPos(pos, 0, 7500, $"金银射线指路{row}{col}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }

    private int[] findGoldenSilverIndex(int value, bool is_first)
    {
        List<List<int>> priority = [[30, 30, 0, 0], [30, 30, 0, 0], [20, 20, 10, 10], [20, 20, 10, 10]];
        int[] safeField1 = [4, 4];
        int[] safeField2 = [4, 4];
        for (int i = 0; i < Boss2_GoldenSilverField.Count; i++)
        {
            for (int j = 0; j < Boss2_GoldenSilverField[i].Count; j++)
            {
                if (Boss2_GoldenSilverField[i][j] != value) continue;
                
                if (value != 11) return [i, j];
                    
                if (safeField1.SequenceEqual([4, 4]))
                    safeField1 = [i, j];
                else
                    safeField2 = [i, j];
            }
        }

        var isSf1HighPiror = priority[safeField1[0]][safeField1[1]] < priority[safeField2[0]][safeField2[1]] ? 1 : -1;
        var isFirst = is_first ? 1 : -1;
        
        return isSf1HighPiror * isFirst > 0 ? safeField1 : safeField2;
    }

    [ScriptMethod(name: "Boss2：连线集合提示绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(310|632))$"])]
    public void Boss2_TetherGatherFirstDraw(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.drawStatic(CENTER_BOSS2, 0, 0, 4000, $"连线集合");
        dp.Scale = new Vector2(1.5f);
        dp.Color = posColorPlayer.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp0 = accessory.dirPos(CENTER_BOSS2, 0, 4000, $"连线集合指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }
    
    [ScriptMethod(name: "Boss2：连线安全方向绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(312|634))$"])]
    public void Boss2_TetherSafePosDraw(Event @event, ScriptAccessory accessory)
    {
        var tpos = @event.TargetPosition();
        var v = new Vector2(tpos.X - CENTER_BOSS2.X, tpos.Z - CENTER_BOSS2.Z);
        if (v.Length() < 5) return;
        
        if (Drawn[1]) return;
        Drawn[1] = true;
        var tetherDangerPos = tpos.PositionRoundToDirs(CENTER_BOSS2, 4);
        // AC先出
        List<Vector3> safePos = [];
        if (tetherDangerPos % 2 == 0)
        {
            // 初始安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * -45, 13 * float.Sqrt(2)));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 135, 13 * float.Sqrt(2)));
            // 后续安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 0, 13));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 180, 13));
        }
        else
        {
            // 初始安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 45, 13 * float.Sqrt(2)));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * -135, 13 * float.Sqrt(2)));
            // 后续安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 90, 13));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 270, 13));
        }

        var myIndex = accessory.getMyIndex();
        DrawPropertiesEdit dp1;
        DrawPropertiesEdit dp2;
        if (myIndex % 2 == 0)
        {
            // 是T与近战，上半场
            dp1 = accessory.dirPos(safePos[0], 0, 10000, $"第一步");
            dp2 = accessory.dirPos2Pos(safePos[0], safePos[2], 0, 10000, $"第二步准备");
        }
        else
        {
            // 是H与远程，上半场
            dp1 = accessory.dirPos(safePos[1], 0, 10000, $"第一步");
            dp2 = accessory.dirPos2Pos(safePos[1], safePos[3], 0, 10000, $"第二步准备");
        }
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    }
    
    [ScriptMethod(name: "Boss2：连线安全方向指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(30(312|634))$"])]
    public void Boss2_TetherSafePosDir(Event @event, ScriptAccessory accessory)
    {
        var tpos = @event.TargetPosition();
        var v = new Vector2(tpos.X - CENTER_BOSS2.X, tpos.Z - CENTER_BOSS2.Z);
        if (v.Length() < 5) return;
        
        if (Drawn[2]) return;
        Drawn[2] = true;
        var tetherDangerPos = tpos.PositionRoundToDirs(CENTER_BOSS2, 4);
        // AC先出
        List<Vector3> safePos = [];
        if (tetherDangerPos % 2 == 0)
        {
            // 初始安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * -45, 13 * float.Sqrt(2)));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 135, 13 * float.Sqrt(2)));
            // 后续安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 0, 13));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 180, 13));
        }
        else
        {
            // 初始安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 45, 13 * float.Sqrt(2)));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * -135, 13 * float.Sqrt(2)));
            // 后续安全点
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 90, 13));
            safePos.Add(CENTER_BOSS2.ExtendPoint(float.Pi / 180 * 270, 13));
        }

        var myIndex = accessory.getMyIndex();
        DrawPropertiesEdit dp2;
        if (myIndex % 2 == 0)
            // 是T与近战，上半场
            dp2 = accessory.dirPos(safePos[2], 0, 2000, $"第二步");
        else
            // 是H与远程，上半场
            dp2 = accessory.dirPos(safePos[3], 0, 2000, $"第二步");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        accessory.Method.RemoveDraw($"第一步");
        accessory.Method.RemoveDraw($"第二步准备");
    }

    [ScriptMethod(name: "Boss2：踩塔与放圈安全方向指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(30(314|636))$"])]
    public void Boss2_TowerAndChariotSafeDir(Event @event, ScriptAccessory accessory)
    {
        if (Drawn[3]) return;
        Drawn[3] = true;
        
        var spos = @event.SourcePosition();
        var pos = spos.PositionRoundToDirs(CENTER_BOSS2,4);
        List<Vector3> chariotPos = [];
        List<Vector3> towerPos = [];

        if (pos % 2 == 0)
        {
            // 先上下塔，塔半径3
            // 近战右上，远程左下
            towerPos.Add(CENTER_BOSS2.ExtendPoint(0 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(2 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(1 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(3 * float.Pi / 2, 7.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(1 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(3 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(0 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(2 * float.Pi / 2, 19.5f));
        }
        else
        {
            // 先左右塔，塔半径3
            // 近战右上，远程左下
            towerPos.Add(CENTER_BOSS2.ExtendPoint(1 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(3 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(0 * float.Pi / 2, 7.5f));
            towerPos.Add(CENTER_BOSS2.ExtendPoint(2 * float.Pi / 2, 7.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(0 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(2 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(1 * float.Pi / 2, 19.5f));
            chariotPos.Add(CENTER_BOSS2.ExtendPoint(3 * float.Pi / 2, 19.5f));
        }

        // 若同组内数字相同，dps需互换
        var swap = (Boss2_isLongBuff[0] && Boss2_isLongBuff[2]) || (Boss2_isLongBuff[1] && Boss2_isLongBuff[3]);
        var myIndex = accessory.getMyIndex();
        
        // isLongBuff影响先踩塔还是先放圈
        // playerIdx影响[0,2]还是[1,3]
        List<bool> actionJudge = [false, false];
        // actionJudge[0]: true则先踩塔，false则先放圈
        // actionJudge[1]: true则视为近战组0-2，false则视为远程组1-3
        if (Boss2_isLongBuff[myIndex])
            actionJudge[0] = true;
        if (myIndex % 2 == 0)
            actionJudge[1] = true;
        if (myIndex >= 2 && swap)
            actionJudge[1] = !actionJudge[1];

        accessory.Log.Debug($"我{(swap ? "要换" : "不换")}，先{(actionJudge[0] ? "踩塔" : "放圈")}，我是{(actionJudge[1] ? "右上" : "左下")}组。");

        Vector3 tpos1 = (actionJudge[0] ? towerPos : chariotPos)[actionJudge[1] ? 0 : 1];
        Vector3 tpos2 = (actionJudge[0] ? chariotPos : towerPos)[actionJudge[1] ? 2 : 3];
        var dp1 = accessory.dirPos(tpos1, 0, 6500, $"第一步");
        var dp12 = accessory.dirPos2Pos(tpos1, tpos2, 0, 6500, $"第二步准备");
        dp12.Color = accessory.Data.DefaultDangerColor;
        var dp2 = accessory.dirPos(tpos2, 6500, 4500, $"第二步");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    }
    #endregion

    #region BOSS3 键山雏

    [ScriptMethod(name: "Boss3：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(29841)$"], userControl: false)]
    public void Boss3_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            ASS_Phase.Init => ASS_Phase.BOSS3_P1,
            ASS_Phase.BOSS3_P1 => ASS_Phase.BOSS3_P2,
            ASS_Phase.BOSS3_P2 => ASS_Phase.BOSS3_P3,
            ASS_Phase.BOSS3_P3 => ASS_Phase.BOSS3_P4,
            ASS_Phase.BOSS3_P4 => ASS_Phase.BOSS3_P5,
            _ => ASS_Phase.BOSS3_P1
        };
        accessory.Log.Debug($"当前阶段为：{phase}");
    }

    [ScriptMethod(name: "Boss3：顺劈死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(29869|30404)$"])]
    public void Boss3_TankBuster(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var sid = @event.SourceId();

        var dp = accessory.drawFan(sid, float.Pi / 180 * 60, 40, 0, 5000, $"顺劈{tid}");
        dp.TargetObject = tid;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "Boss3：石火豪冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(29872)$"])]
    public void Boss3_Strike(Event @event, ScriptAccessory accessory)
    {
        var myIndex = accessory.getMyIndex();
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            if (i == myIndex) continue;
            var dp = accessory.drawCircle(accessory.Data.PartyList[i], 0, 5000, $"石火豪冲{i}");
            dp.Scale = new(10f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "Boss3：石火豪冲目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2987[34]|3040[67])$"], userControl: false)]
    public void Boss3_StrikeTargetRecord(Event @event, ScriptAccessory accessory)
    {
        if (@event.TargetIndex() != 1) return;

        const uint FIRST = 29873;
        const uint SECOND = 29874;
        const uint FIRST_SAVAGE = 30406;
        const uint SECOND_SAVAGE = 30407;

        var aid = @event.ActionId();
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);

        var _idx = aid switch
        {
            FIRST or FIRST_SAVAGE => 0,
            SECOND or SECOND_SAVAGE => 1,
            _ => 0
        };

        Boss3_StrikeTarget[_idx] = tidx;
    }

    [ScriptMethod(name: "Boss3：祝福圣火挡枪", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(29875)$"])]
    public void Boss3_BlessedBeacon(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var myIndex = accessory.getMyIndex();
        for (int i = 0; i < Boss3_StrikeTarget.Count(); i++)
        {
            var dp = accessory.drawRect(sid, 8, 65, 0, 7000, $"祝福圣火挡枪{Boss3_StrikeTarget[i]}");
            dp.TargetObject = accessory.Data.PartyList[Boss3_StrikeTarget[i]];
            dp.Color = Boss3_StrikeTarget.Contains(myIndex) ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "Boss3：祝福圣火挡枪消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2987[67]|3040[89])$"], userControl: false)]
    public void Boss3_BlessedBeaconRemove(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        accessory.Method.RemoveDraw($"祝福圣火挡枪{tidx}");
    }

    const uint CLOCKWISE = 64;
    const uint COUNTER_CLOCKWISE = 256;

    [ScriptMethod(name: "Boss3：咒具一转火球", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:regex:^(64|256)$"])]
    public void Boss3_P1_FireBall(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P1) return;
        var spos = @event.SourcePosition();
        var id1 = @event.Id1();

        // 出特效的地方一定是有火球的地方
        Vector3 CENTER_UP = new(289f, 533, -115f);
        Vector3 CENTER_DOWN = new(289f, 533, -95f);

        Vector3 _array_center = spos.Z > CENTER_BOSS3.Z ? CENTER_DOWN : CENTER_UP;
        var _pos = spos.PositionRoundToDirs(_array_center, 4);
        var _rot_radian = id1 switch
        {
            CLOCKWISE => float.Pi / 2,
            COUNTER_CLOCKWISE => -float.Pi / 2,
            _ => float.Pi / 2
        };
        drawRotFireball(spos, _pos, _rot_radian, _array_center, true, 0, 10000, accessory);
    }

    private DrawPropertiesEdit drawRotFireball(Vector3 spos, int pos, float rot_radian, Vector3 center, bool draw, int delay, int destory, ScriptAccessory accessory)
    {
        // 先找到对面
        var _mirror_pos = pos % 2 == 0 ? spos.FoldPointUD(center.Z) : spos.FoldPointLR(center.X);
        // 再旋转
        var _tpos = _mirror_pos.RotatePoint(spos, rot_radian);
        var dp = accessory.drawStatic(_tpos, 0, delay, destory, $"旋转火球{spos}");
        dp.Scale = new(12f);
        dp.Color = Boss3_DefaultDangerColor ? accessory.Data.DefaultDangerColor.WithW(1.5f) : Boss3_SetDangerColor.V4.WithW(1.5f);
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        return dp;
    }

    [ScriptMethod(name: "Boss3：咒具二立体魔法阵", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(14770|14821)$"])]
    public void Boss3_P2_Panels(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();
        var sid = @event.SourceId();

        if (phase == ASS_Phase.BOSS3_P2)
        {
            var _delay = 12000;
            var _destory = 25000;
            drawPanelStraight(sid, _delay, _destory, true, accessory);
        }
        else if (phase == ASS_Phase.BOSS3_P4)
        {
            int _idx;
            if (spos.X < CENTER_BOSS3.X - 5)
                _idx = 0;
            else if (spos.X > CENTER_BOSS3.X + 5)
                _idx = 2;
            else
                _idx = 1;
            Boss3_P4_PanelSid[_idx] = sid;
        }
        else
            return;
    }

    private DrawPropertiesEdit drawPanelStraight(uint sid, int delay, int destory, bool draw, ScriptAccessory accessory)
    {
        var dp = accessory.drawRect(sid, 10, 100, delay, destory, $"圣火炮{sid}");
        dp.Color = Boss3_DefaultDangerColor ? accessory.Data.DefaultDangerColor.WithW(1f) : Boss3_SetDangerColor.V4.WithW(1f);
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        return dp;
    }

    [ScriptMethod(name: "Boss3：圣火炮消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(29862|30401)$"], userControl: false)]
    public void Boss3_PanelsRemove(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        accessory.Method.RemoveDraw($"圣火炮{sid}");
    }
    
    [ScriptMethod(name: "Boss3：咒具二麻将Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(32(6[89]|7[01]))$"], userControl: false)]
    public void Boss3_P2_PlayerBuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P2) return;
        const uint one = 3268;
        const uint two = 3269;
        const uint three = 3270;
        const uint four = 3271;

        var aid = @event.StatusID();
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        Boss3_P2_PlayerBuff[tidx] = aid switch
        {
            one => 1,
            two => 2,
            three => 3,
            four => 4,
            _ => 0
        };
        
        accessory.Log.Debug($"检测到玩家{tidx}的Buff为{Boss3_P2_PlayerBuff[tidx]}");
    }
    
    [ScriptMethod(name: "Boss3：咒具二剪线位置指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(45[01234567])$"])]
    public async void Boss3_P2_CutLineDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P2) return;
        HashSet<uint> one = [450, 454];
        HashSet<uint> two = [451, 455];
        HashSet<uint> three = [452, 456];
        HashSet<uint> four = [453, 457];

        var param = @event.Param();
        var tid = @event.TargetId();
        var tpos = @event.TargetPosition();

        await Task.Delay(100);
        var myIndex = accessory.getMyIndex();
        accessory.Log.Debug($"检测到线{tid}的Buff{(param - 450) % 4 + 1}");
        
        if ((param - 450) % 4 + 1 != Boss3_P2_PlayerBuff[myIndex]) return;
        
        accessory.Log.Debug($"检测到线{tid}的Buff与主视角的Buff相同");

        var dpl = accessory.drawRect(tid, 4f, 40f, 0, 30000, $"线{tid}");
        dpl.Color = accessory.Data.DefaultSafeColor;
        dpl.Offset = new Vector3(0, -1.75f, 0);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dpl);
        
        // 寻找线的位置，确定剪线起始点
        if (Drawn[4]) return;
        Drawn[4] = true;
        
        var linePos = FindLineIdx(tpos);
        accessory.Log.Debug($"我在寻找{tpos}位置的线，linePos为{linePos}");
        Vector3 cutLinePos;
        if (IsLineAtLeftDownSide(linePos))
            cutLinePos = CENTER_BOSS3 with { X = CENTER_BOSS3.X - 10, Z = CENTER_BOSS3.Z + 10 };
        else
            cutLinePos = CENTER_BOSS3 with { X = CENTER_BOSS3.X + 10, Z = CENTER_BOSS3.Z - 10 };

        var dp = accessory.drawStatic(cutLinePos, 0, 0,
                8000, $"剪线起始点");
        dp.Color = posColorPlayer.V4.WithW(1.5f);
        dp.Scale = new Vector2(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp0 = accessory.dirPos(cutLinePos, 0, 8000, $"剪线起始点指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }

    [ScriptMethod(name: "Boss3：咒具二线条删除", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:regex:^(7738)$"], userControl: false)]
    public void Boss3_P2_CutLineRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P2) return;
        var sid = @event.SourceId();
        accessory.Method.RemoveDraw($"线{sid}.*");
    }
    
    private int FindLineIdx(Vector3 pos)
    {
        // 1,2,3,4与11,12,13,14，>10代表竖线
        // 数小代表在左下
        var idx = 0;
        if (Math.Abs(pos.X - CENTER_BOSS3.X) < 5)
        {
            idx += 10;
            idx += (int)Math.Floor(pos.X - 283) / 2;
        }
        else
        {
            idx += (int)Math.Floor(-pos.Z - 99) / 2;
        }

        return idx;
    }

    private bool IsLineAtLeftDownSide(int idx)
    {
        return idx % 10 <= 2;
    }
    
    [ScriptMethod(name: "Boss3：咒具三传送阵转转", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:regex:^(64|256)$"])]
    public void Boss3_P3_Portal(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P3) return;
        var spos = @event.SourcePosition();
        var id1 = @event.Id1();
        
        // 出特效的地方一定是有传送阵的地方
        Vector3 CENTER_LEFT = new(281f, 533, CENTER_BOSS3.Z);
        Vector3 CENTER_RIGHT = new(297f, 533, CENTER_BOSS3.Z);
        Vector3 _part_center = spos.X > CENTER_BOSS3.X ? CENTER_RIGHT : CENTER_LEFT;

        var _rot_radian = id1 switch
        {
            CLOCKWISE => float.Pi / 2,
            COUNTER_CLOCKWISE => -float.Pi / 2,
            _ => float.Pi / 2
        };
        drawRotPortal(spos, _rot_radian, _part_center, true, 0, 10000, accessory);
    }

    private DrawPropertiesEdit drawRotPortal(Vector3 spos, float rot_radian, Vector3 center, bool draw, int delay, int destory, ScriptAccessory accessory)
    {
        // 先找到对面
        var _mirror_pos = spos.FoldPointLR(center.X);
        // 再旋转
        var _tpos = _mirror_pos.RotatePoint(spos, rot_radian);
        var _isSafe = isInBoss3Field(_tpos);

        var dp = accessory.drawStatic(_tpos, 0, delay, destory, $"旋转传送阵{_tpos}");
        dp.Scale = new(1.5f);
        dp.Color = _isSafe ? posColorPlayer.V4.WithW(2f) : accessory.Data.DefaultDangerColor.WithW(2f);
        if (draw)
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

        var dp0 = accessory.drawStatic(spos, 0, delay, destory, $"旋转传送阵{spos}");
        dp0.Scale = new(1.5f);
        dp0.Color = _isSafe ? posColorPlayer.V4.WithW(2f) : accessory.Data.DefaultDangerColor.WithW(2f);
        if (draw)
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp0);

        if (!_isSafe) return dp;
        
        var row_tpos = (int)Math.Floor(_tpos.Z + 123) / 10;
        var row_spos = (int)Math.Floor(spos.Z + 123) / 10;
        Boss3_P3_safeTeleportPos[row_tpos] = _tpos;
        Boss3_P3_safeTeleportPos[row_spos] = spos;
        accessory.Log.Debug($"记录下了第{row_spos}行的安全坐标{spos}");
        accessory.Log.Debug($"记录下了第{row_tpos}行的安全坐标{_tpos}");
        return dp;
    }

    private bool isInBoss3Field(Vector3 pos)
    {
        if (Math.Abs(pos.X - CENTER_BOSS3.X) > FIELDX_BOSS3 / 2)
            return false;
        if (Math.Abs(pos.Z - CENTER_BOSS3.Z) > FIELDZ_BOSS3 / 2)
            return false;
        return true;
    }

    [ScriptMethod(name: "Boss3：咒具三玩家转转传送阵", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2970)$"])]
    public async void Boss3_P3_PlayerPortal(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P3) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        
        await Task.Delay(1000);

        var param = @event.Param();
        // 逆奇，顺偶，左%5=2，右%2=1
        // 或者，数小向上传送；数大向下传送
        const uint LEFT_CW = 462;   // 1
        const uint RIGHT_CW = 466;  // 2
        const uint LEFT_CCW = 467;  // 4
        const uint RIGHT_CCW = 461; // 3

        Boss3_P3_myRotateRow = param switch
        {
            LEFT_CW => 1,
            RIGHT_CW => 2,
            LEFT_CCW => 4,
            RIGHT_CCW => 3,
            _ => 0
        };
        
        accessory.Log.Debug($"我应该去第{Boss3_P3_myRotateRow}行，对应坐标{Boss3_P3_safeTeleportPos[Boss3_P3_myRotateRow-1]}");
        
        drawPlayerPortal(tid, param, 14000, 3000, accessory);
        var dp = accessory.dirPos(Boss3_P3_safeTeleportPos[Boss3_P3_myRotateRow-1], 0, 9000, $"玩家传送{Boss3_P3_myRotateRow}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private DrawPropertiesEdit drawPlayerPortal(uint tid, uint param, int delay, int destory, ScriptAccessory accessory)
    {
        var _isup = param < 464;
        float _rot_radian = _isup ? DirectionCalc.BaseDirRad2InnGame(0) : DirectionCalc.BaseDirRad2InnGame(float.Pi);

        // TODO 很可惜的是，我不会在传送目的地画一个Imgui的圆形，尝试失败
        var dp = accessory.drawRect(tid, 1, 10, delay, destory, $"自传送指向{tid}");
        dp.Color = accessory.Data.DefaultSafeColor.WithW(3f);
        dp.Rotation = _rot_radian;
        dp.FixRotation = true;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        return dp;
    }

    [ScriptMethod(name: "Boss3：咒具三引导斧头", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3278)$"])]
    public void Boss3_P3_BaitAxe(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P3) return;
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);
        var myIndex = accessory.getMyIndex();
        if (tidx != myIndex) return;

        Vector3 baitPos;
        int myBaitRow;
        if (!Drawn[5])
        {
            Drawn[5] = true;
            myBaitRow = Boss3_P3_myRotateRow switch
            {
                1 => 1,
                2 => 0,
                3 => 3,
                4 => 2,
                _ => 0
            };
            baitPos = Boss3_P3_BaitPos[myBaitRow];
        }
        else if (!Drawn[6])
        {
            Drawn[6] = true;
            myBaitRow = Boss3_P3_myRotateRow - 1;
            baitPos = Boss3_P3_BaitPos[myBaitRow];
        }
        else
            return;

        for (int i = 0; i < 4; i++)
        {
            var dp0 = accessory.drawStatic(Boss3_P3_BaitPos[i], 0, 0,
                10000, $"引导斧头位置{i}");
            dp0.Color = i == myBaitRow ? posColorPlayer.V4.WithW(1.5f) : posColorNormal.V4;
            dp0.Scale = new Vector2(0.5f);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp0);
        }
        
        accessory.Log.Debug($"我应该去第{myBaitRow + 1}行引导斧头，对应坐标{baitPos}");
        var dp = accessory.dirPos(baitPos, 0, 10000, $"引导斧头位置指路{myBaitRow}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "Boss3：引导斧头指路消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(29883|30413)$"], userControl: false)]
    public void Boss3_P3_BaitAxeRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw("引导斧头位置{i}.*");
        accessory.Method.RemoveDraw("引导斧头位置指路{i}.*");
    }
    
    // 日，写到最后发现可以通过咒具的面向看传送方向，吐了
    [ScriptMethod(name: "Boss3：咒具四立体魔法阵面向记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(2013025)$", "Operate:Add"], userControl: false)]
    public void Boss3_P4_BrandRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P4) return;
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();
        int _idx;
        if (spos.X < CENTER_BOSS3.X - 5)
            _idx = 0;
        else if (spos.X > CENTER_BOSS3.X + 5)
            _idx = 2;
        else
            _idx = 1;

        Boss3_P4_BrandRot[_idx] = srot.BaseInnGame2DirRad();
    }

    [ScriptMethod(name: "Boss3：咒具四魔法阵转转", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:regex:^(64|256)$"])]
    public void Boss3_P4_Panels(Event @event, ScriptAccessory accessory)
    {
        if (phase != ASS_Phase.BOSS3_P4) return;
        var spos = @event.SourcePosition();
        var id1 = @event.Id1();

        int _idx;
        if (spos.X < CENTER_BOSS3.X - 5)
            _idx = 0;
        else if (spos.X > CENTER_BOSS3.X + 5)
            _idx = 2;
        else
            _idx = 1;

        var _rot_radian = id1 switch
        {
            CLOCKWISE => float.Pi / 2,
            COUNTER_CLOCKWISE => -float.Pi / 2,
            _ => float.Pi / 2
        };
        drawRotPanel(spos, _idx, _rot_radian, true, 0, 15000, accessory);
    }

    private DrawPropertiesEdit drawRotPanel(Vector3 spos, int idx, float rot_radian, bool draw, int delay, int destory, ScriptAccessory accessory)
    {
        // 延伸方向角度
        var _rot_radian = Boss3_P4_BrandRot[idx] + rot_radian;
        var _panel_pos = spos.ExtendPoint(_rot_radian, 10);

        var _panel_face_rad = idx switch
        {
            0 => float.Pi / 2,
            1 => 0,
            2 => -float.Pi / 2,
            _ => 0
        };
        var _sid = Boss3_P4_PanelSid[idx];
        var dp = accessory.drawStatic(_panel_pos, _panel_face_rad, delay, destory, $"圣火炮{_sid}");
        dp.Scale = new(10, 100);
        dp.Color = Boss3_DefaultDangerColor ? accessory.Data.DefaultDangerColor.WithW(1f) : Boss3_SetDangerColor.V4.WithW(1f);
        if (draw) accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        return dp;
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
    
    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }
    
    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }
    public static uint Id1(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Id1"]);
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
    public static DrawPropertiesEdit drawRect(this ScriptAccessory accessory, uint owner_id, float width, float length, int delay, int destory, string name)
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

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*$
