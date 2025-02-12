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
using ECommons.MathHelpers;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;

namespace UsamisKodakku.Scripts.LocalTest.AMR;

[ScriptType(name: Name, territorys: [1155, 1156], guid: "amr", 
    version: Version, author: "Usami", note: NoteStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$

public class Amr
{
    const string NoteStr =
    """
    v0.0.0.0
    测试中。
    """;

    private const string Name = "Local AMR [异闻六根山]";
    private const string Version = "0.0.0.0";
    private const string DebugVersion = "a";
    private const string Note = "";
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };
    public void Init(ScriptAccessory accessory)
    {
        accessory.DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{Note}", DebugMode);
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        // ---- DEBUG CODE ----

        // -- DEBUG CODE END --
    }
    
    #region Boss1 舞狮

    [ScriptMethod(name: "---- Boss1 狮子王 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"], userControl: true)]
    public static void SplitLine_Boss1(Event @event, ScriptAccessory accessory) {}

    [ScriptMethod(name: "死刑与甩尾", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(338(19|20|5[89]))$"],
        userControl: true)]
    public static void SplittingCry(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var tid = @event.TargetId();
        var sid = @event.SourceId();

        HashSet<uint> tankBuster = [33819, 33858];
        HashSet<uint> backSwipe = [33820, 33859];
        
        if (tankBuster.Contains(aid))
        {
            var dp = accessory.DrawOwnersTarget(sid, 14f, 60f, 0, 5000, $"直线死刑");
            accessory.Method.SendDraw(0, DrawTypeEnum.Rect, dp);
        }
        
        if (backSwipe.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, false, 0, 2000, $"扇形后刀", 60f.DegToRad(), 25f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp0);
        }
    }

    [ScriptMethod(name: "六条奔雷矩形范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(33(790|829))$"],
        userControl: true)]
    public static void RokujoRevelRectAoe(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 14f, 60f, 0, 8000, $"六条奔雷矩形{sid}");
        accessory.Method.SendDraw(0, DrawTypeEnum.Straight, dp);
    }
    
    #endregion

    #region Boss2 捕鼠

    [ScriptMethod(name: "---- Boss2 铁鼠豪雷 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"], userControl: true)]
    public static void SplitLine_Boss2(Event @event, ScriptAccessory accessory) {}

    #endregion

    #region Boss3 捉鬼

    [ScriptMethod(name: "---- Boss3 怨灵猛虎 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"], userControl: true)]
    public static void SplitLine_Boss3(Event @event, ScriptAccessory accessory) {}

    #endregion
    
    #region Mob1

    [ScriptMethod(name: "---- Trash 小怪 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"], userControl: true)]
    public static void SplitLine_Trash(Event @event, ScriptAccessory accessory) {}
    
    [ScriptMethod(name: "紫州雷犼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((343(87|89|90))|3440[567])$"])]
    public void Mob1_Raiko(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        var tid = @event.TargetId();

        HashSet<uint> chariot = [34390, 34408];
        HashSet<uint> donut = [34389, 34407];
        HashSet<uint> charge = [34387, 34405];

        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 10, 0, 4000, $"雷犼钢铁");
            accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
        }

        if (donut.Contains(aid))
        {
            var dp = accessory.DrawDonut(sid, 30, 5, 0, 4000, $"雷犼月环");
            accessory.Method.SendDraw(0, DrawTypeEnum.Donut, dp);
        }

        if (charge.Contains(aid))
        {
            var dp = accessory.DrawTarget2Target(sid, tid, 7, 5, 0, 4000, $"雷光冲锋", true);
            accessory.Method.SendDraw(0, DrawTypeEnum.Donut, dp);
        }
    }

    [ScriptMethod(name: "紫州风犼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(39[234]|41[012]))$"])]
    public void Mob1_Fuko(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        var tid = @event.TargetId();

        HashSet<uint> stack = [34392, 34410];
        HashSet<uint> knockBack = [34393, 34411];
        HashSet<uint> chariot = [34394, 34412];

        if (stack.Contains(aid))
        {
            var dp = accessory.DrawCircle(tid, 8, 0, 5000, $"风犼分摊");
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
        }

        if (knockBack.Contains(aid))
        {
            var dp = accessory.DrawKnockBack(sid, 25, 0, 4000, $"风犼击退");
            accessory.Method.SendDraw(0, DrawTypeEnum.Displacement, dp);
        }

        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 10, 0, 4000, $"风犼钢铁");
            accessory.Method.SendDraw(0, DrawTypeEnum.Donut, dp);
        }
    }
    
    [ScriptMethod(name: "紫州幽鬼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(3[78]|4[01]))$"])]
    public void Mob1_Yuki(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();

        HashSet<uint> rightSwipe = [34437, 34440];
        HashSet<uint> leftSwipe = [34438, 34441];

        if (rightSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, false, 0, 4000, $"幽鬼右刀");
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        }

        if (leftSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, true, 0, 4000, $"幽鬼右刀");
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        }
    }
    #endregion
    
    #region Mob2
    
    [ScriptMethod(name: "紫州小天狗", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(39[678]|401|41[4569]))$"])]
    public void Mob2_Kotengu(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        
        HashSet<uint> backCleave = [34396, 34414];
        HashSet<uint> leftCleave = [34397, 34415];
        HashSet<uint> rightCleave = [34398, 34416];
        HashSet<uint> gaze = [34401, 34419];
        
        // TODO 绘图摧毁时间待测试
        if (rightCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawLeftRightCleave(sid, false, 0, 5000, $"天狗左刀", 90f.DegToRad());
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        }

        if (leftCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawLeftRightCleave(sid, true, 0, 5000, $"天狗右刀", 90f.DegToRad());
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        }
        
        if (backCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawFrontBackCleave(sid, false, 0, 5000, $"天狗后刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        }

        if (gaze.Contains(aid))
        {
            var dp = accessory.DrawSightAvoid(sid, 0, 4000, $"天狗背对");
            accessory.Method.SendDraw(0, DrawTypeEnum.SightAvoid, dp);
        }
    }

    [ScriptMethod(name: "风元精直线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(39|42))$"])]
    public void Mob2_WindElement(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 8, 40, 0, 6000, $"风元精直线", true);
        accessory.Method.SendDraw(0, DrawTypeEnum.Rect, dp);
    }
    
    [ScriptMethod(name: "紫州隐密头领", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(04|2[29]|30))$"])]
    public void Mob2_Onmitsugashira(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        
        HashSet<uint> shuriken = [34404, 34422];
        HashSet<uint> shurikenFast = [34429, 34430];
        
        var destroy = shuriken.Contains(aid) ? 3000 : 1500;
        var dp = accessory.DrawRect(sid, 3, 40, 0, destroy, $"忍者手里剑");
        accessory.Method.SendDraw(0, DrawTypeEnum.Rect, dp);
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
    
    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }

    public static uint StatusId(this Event @event)
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

    public static bool IsTank(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Tank;
    }
    public static bool IsHealer(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Healer;
    }
    public static bool IsDps(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.DPS;
    }
    public static bool AtNorth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z <= centerZ;
    }
    public static bool AtSouth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z > centerZ;
    }
    public static bool AtWest(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X <= centerX;
    }
    public static bool AtEast(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X > centerX;
    }
}

public static class DirectionCalc
{
    // 以北为0建立list
    // Game         List    Logic
    // 0            - 4     pi
    // 0.25 pi      - 3     0.75pi
    // 0.5 pi       - 2     0.5pi
    // 0.75 pi      - 1     0.25pi
    // pi           - 0     0
    // 1.25 pi      - 7     1.75pi
    // 1.5 pi       - 6     1.5pi
    // 1.75 pi      - 5     1.25pi
    // Logic = Pi - Game (+ 2pi)

    /// <summary>
    /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
    /// 算法与Logic2Game完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">游戏基角度</param>
    /// <returns>逻辑基角度</returns>
    public static float Game2Logic(this float radian)
    {
        // if (r < 0) r = (float)(r + 2 * Math.PI);
        // if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);

        var r = float.Pi - radian;
        r = (r + float.Pi * 2) % (float.Pi * 2);
        return r;
    }

    /// <summary>
    /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
    /// 算法与Game2Logic完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <returns>游戏基角度</returns>
    public static float Logic2Game(this float radian)
    {
        // var r = (float)Math.PI - radian;
        // if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        // if (r > Math.PI) r = (float)(r - 2 * Math.PI);

        return radian.Game2Logic();
    }

    /// <summary>
    /// 输入逻辑基角度，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>逻辑基角度对应的逻辑方位</returns>
    public static int Rad2Dirs(this float radian, int dirs, bool diagDivision = true)
    {
        var r = diagDivision
            ? Math.Round(radian / (2f * float.Pi / dirs))
            : Math.Floor(radian / (2f * float.Pi / dirs));
        r = (r + dirs) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int Position2Dirs(this Vector3 point, Vector3 center, int dirs, bool diagDivision = true)
    {
        double dirsDouble = dirs;
        var r = diagDivision
            ? Math.Round(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble
            : Math.Floor(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble;
        return (int)r;
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
        return new Vector3(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);
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
        return new Vector3(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="newPoint">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(this Vector3 newPoint, Vector3 center)
    {
        var radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
    {
        return point with { X = 2 * centerX - point.X };
    }

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
    {
        return point with { Z = 2 * centerZ - point.Z };
    }
    
    /// <summary>
    /// 将输入点朝某中心点往内/外同角度延伸，默认向内
    /// </summary>
    /// <param name="point">待延伸点</param>
    /// <param name="center">中心点</param>
    /// <param name="length">延伸长度</param>
    /// <param name="isOutside">是否向外延伸</param>>
    /// <returns></returns>
    public static Vector3 PointInOutside(this Vector3 point, Vector3 center, float length, bool isOutside = false)
    {
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var targetPos = (point - center) / v2.Length() * length * (isOutside ? 1 : -1) + point;
        return targetPos;
    }
}

public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = accessory.Data.PartyList.IndexOf(pid);
        var str = accessory.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx)
    {
        var str = idx switch
        {
            0 => "MT",
            1 => "ST",
            2 => "H1",
            3 => "H2",
            4 => "D1",
            5 => "D2",
            6 => "D3",
            7 => "D4",
            _ => "unknown"
        };
        return str;
    }
}

public static class ColorHelper
{
    public static ScriptColor ColorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public static ScriptColor ColorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };
    public static ScriptColor ColorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
    public static ScriptColor ColorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
    public static ScriptColor ColorYellow = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

}

public static class AssignDp
{
    /// <summary>
    /// 返回箭头指引相关dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerObj">箭头起始，可输入uint或Vector3</param>
    /// <param name="targetObj">箭头指向目标，可输入uint或Vector3，为0则无目标</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="scale">箭头宽度</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Rotation = rotation;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        
        switch (ownerObj)
        {
            case uint sid:
                dp.Owner = sid;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException("ownerObj的目标类型输入错误");
        }

        switch (targetObj)
        {
            case uint tid:
                if (tid != 0) dp.TargetObject = tid;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
        }

        return dp;
    }

    /// <summary>
    /// 返回扇形左右刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isLeftCleave">是左刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLeftRightCleave(this ScriptAccessory accessory, uint ownerId, bool isLeftCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isLeftCleave ? float.Pi / 2 : -float.Pi / 2;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形前后刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isFrontCleave">是前刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFrontBackCleave(this ScriptAccessory accessory, uint ownerId, bool isFrontCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isFrontCleave ? 0 : -float.Pi;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某对象目标最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某坐标位置最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="position">特定坐标点</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawPositionNearFarOrder(this ScriptAccessory accessory, Vector3 position, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = position;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.TargetResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.TargetOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId施法目标的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersTarget(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay,
        int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId仇恨相关的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersEntityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回owner与target之间的连线dp，使用Line绘制
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="scale">线条宽度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnectionBetweenTargets(this ScriptAccessory accessory, uint ownerId,
        uint targetId, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= ScaleMode.YByDistance;
        return dp;
    }

    /// <summary>
    /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="scale">圆圈尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, uint ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">外环实心尺寸</param>
    /// <param name="innerScale">内环空心尺寸</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory accessory, uint ownerId, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.DrawFan(ownerId, float.Pi * 2, 0, scale, innerScale, delay, destroy, name, byTime);
        return dp;
    }

    /// <summary>
    /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
    /// </summary>
    /// <param name="center">绘图中心位置</param>
    /// <param name="radian">图形角度</param>
    /// <param name="rotation">旋转角度，以北为0度顺时针</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, Vector3 center, float radian, float rotation, float width, float length, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = center;
        dp.Radian = radian;
        dp.Rotation = rotation.Logic2Game();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    /// <summary>
    /// 返回静态圆圈dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">圆圈中心位置</param>
    /// <param name="color">圆圈颜色</param>
    /// <param name="scale">圆圈尺寸，默认1.5f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
    {
        var dp = accessory.DrawStatic(center, 0, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        return dp;
    }

    /// <summary>
    /// 返回静态月环dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">月环中心位置</param>
    /// <param name="color">月环颜色</param>
    /// <param name="scale">月环外径，默认1.5f</param>
    /// <param name="innerscale">月环内径，默认scale-0.05f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale, float innerscale = 0)
    {
        var dp = accessory.DrawStatic(center, float.Pi * 2, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
        return dp;
    }

    /// <summary>
    /// 返回矩形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="radian">扇形弧度</param>
    /// <param name="rotation">图形旋转角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="innerScale">扇形内环空心尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory accessory, uint ownerId, float radian, float rotation, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.InnerScale = new Vector2(innerScale);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回击退
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">击退源，可输入uint或Vector3</param>
    /// <param name="width">击退绘图宽度</param>
    /// <param name="length">击退绘图长度/距离</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length, int delay, int destroy, string name, float width = 1.5f, uint ownerId = 0, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId == 0 ? accessory.Data.Me : ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawKnockBack的目标类型输入错误");
        }
        dp.Rotation = float.Pi;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回背对
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">背对源，可输入uint或Vector3</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay, int destroy, string name, uint ownerId = 0)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = ownerId == 0 ? accessory.Data.Me : ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawSightAvoid的目标类型输入错误");
        }
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }
    
    /// <summary>
    /// 外部用调试模式
    /// </summary>
    /// <param name="str"></param>
    /// <param name="debugMode"></param>
    /// <param name="accessory"></param>
    public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode)
    {
        if (!debugMode)
            return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }
}

#endregion

#region Enums

public enum ActionId : uint
{
    //------Boss1------
    NTeleport = 33821, // NBoss->location, no cast, single-target, teleport
    STeleport = 33860, // SBoss->location, no cast, single-target, teleport
    
    NStormcloudSummons = 33784, // NBoss->self, 3.0s cast, single-target, visual (summon clouds)
    NSmokeaterFirst = 33785, // NBoss->self, 2.5s cast, single-target, visual (first breath in)
    NSmokeaterRest = 33786, // NBoss->self, no cast, single-target, visual (optional second/third breath in)
    NSmokeaterAbsorb = 33787, // NRaiun->NBoss, no cast, single-target, visual (absorb cloud)
    NRokujoRevelFirst = 33788, // NBoss->self, 7.5s cast, single-target, visual (first line aoe)
    NRokujoRevelRest = 33789, // NBoss->self, no cast, single-target, visual (second/third line aoe)
    NRokujoRevelAOE = 33790, // Helper->self, 8.0s cast, range 60 width 14 rect
    NLeapingLevin1 = 33791, // NRaiun->self, 1.0s cast, range 8 circle
    NLeapingLevin2 = 33792, // NRaiun->self, 1.0s cast, range 12 circle
    NLeapingLevin3 = 33793, // NRaiun->self, 1.0s cast, range 23 circle
    NLightningBolt = 33794, // NBoss->self, 3.0s cast, single-target, visual (start multiple lines)
    NLightningBoltAOE = 33795, // Helper->location, 4.0s cast, range 6 circle
    NCloudToCloud1 = 33796, // NRaiun->self, 2.5s cast, range 100 width 2 rect
    NCloudToCloud2 = 33797, // NRaiun->self, 4.0s cast, range 100 width 6 rect
    NCloudToCloud3 = 33798, // NRaiun->self, 4.0s cast, range 100 width 12 rect
    
    SStormcloudSummons = 33823, // SBoss->self, 3.0s cast, single-target, visual
    SSmokeaterFirst = 33824, // SBoss->self, 2.5s cast, single-target, visual (first breath in)
    SSmokeaterRest = 33825, // SBoss->self, no cast, single-target, visual (optional second/third breath in)
    SSmokeaterAbsorb = 33826, // SRaiun->SBoss, no cast, single-target, visual (absorb cloud)
    SRokujoRevelFirst = 33827, // SBoss->self, 7.5s cast, single-target, visual (first line aoe)
    SRokujoRevelRest = 33828, // SBoss->self, no cast, single-target, visual (second/third line aoe)
    SRokujoRevelAOE = 33829, // Helper->self, 8.0s cast, range 60 width 14 rect
    SLeapingLevin1 = 33830, // SRaiun->self, 1.0s cast, range 8 circle
    SLeapingLevin2 = 33831, // SRaiun->self, 1.0s cast, range 12 circle
    SLeapingLevin3 = 33832, // SRaiun->self, 1.0s cast, range 23 circle
    SLightningBolt = 33833, // SBoss->self, 3.0s cast, single-target, visual (start multiple lines)
    SLightningBoltAOE = 33834, // Helper->location, 4.0s cast, range 6 circle
    SCloudToCloud1 = 33835, // SRaiun->self, 2.5s cast, range 100 width 2 rect
    SCloudToCloud2 = 33836, // SRaiun->self, 4.0s cast, range 100 width 6 rect
    SCloudToCloud3 = 33837, // SRaiun->self, 4.0s cast, range 100 width 12 rect

    NNoblePursuitFirst = 33799, // NBoss->location, 8.0s cast, width 12 rect charge
    NNoblePursuitRest = 33800, // NBoss->location, no cast, width 12 rect charge
    NLevinburst = 33801, // NRairin->self, no cast, range 10 width 100 rect
    SNoblePursuitFirst = 33838, // SBoss->location, 8.0s cast, width 12 rect charge
    SNoblePursuitRest = 33839, // SBoss->location, no cast, width 12 rect charge
    SLevinburst = 33840, // SRairin->self, no cast, range 10 width 100 rect

    NUnnaturalWail = 33815, // NBoss->self, 3.0s cast, single-target, visual (spread/stack debuffs)
    NUnnaturalAilment = 33816, // Helper->players, no cast, range 6 circle spread
    NUnnaturalForce = 33817, // Helper->players, no cast, range 6 circle 2-man stack
    NHauntingCry = 33802, // NBoss->self, 3.0s cast, single-target, visual (spawn adds)
    NRightSwipe = 33803, // NDevilishThrall->self, 10.0s cast, range 40 180-degree cone
    NLeftSwipe = 33804, // NDevilishThrall->self, 10.0s cast, range 40 180-degree cone
    NEyeOfTheThunderVortexFirst = 33811, // NBoss->self, 5.2s cast, range 15 circle
    NEyeOfTheThunderVortexSecond = 33812, // NBoss->self, no cast, range 8-30 donut
    NVortexOfTheThunderEyeFirst = 33813, // NBoss->self, 5.2s cast, range 8-30 donut
    NVortexOfTheThunderEyeSecond = 33814, // NBoss->self, no cast, range 15 circle
    SUnnaturalWail = 33854, // SBoss->self, 3.0s cast, single-target, visual (spread/stack debuffs)
    SUnnaturalAilment = 33855, // Helper->players, no cast, range 6 circle spread
    SUnnaturalForce = 33856, // Helper->players, no cast, range 6 circle 2-man stack
    SHauntingCry = 33841, // SBoss->self, 3.0s cast, single-target, visual (spawn adds)
    SRightSwipe = 33842, // SDevilishThrall->self, 10.0s cast, range 40 180-degree cone
    SLeftSwipe = 33843, // SDevilishThrall->self, 10.0s cast, range 40 180-degree cone
    SEyeOfTheThunderVortexFirst = 33850, // SBoss->self, 5.2s cast, range 15 circle
    SEyeOfTheThunderVortexSecond = 33851, // SBoss->self, no cast, range 8-30 donut
    SVortexOfTheThunderEyeFirst = 33852, // SBoss->self, 5.2s cast, range 8-30 donut
    SVortexOfTheThunderEyeSecond = 33853, // SBoss->self, no cast, range 15 circle

    NReisho = 33805, // Helper->self, no cast, range 6 circle
    NVengefulSouls = 33806, // NBoss->self, 15.0s cast, single-target, visual (towers/defamations)
    NVermilionAura = 33807, // Helper->self, 15.0s cast, range 4 circle tower
    NStygianAura = 33808, // Helper->players, 15.0s cast, range 15 circle spread
    NUnmitigatedExplosion = 33809, // Helper->self, 1.0s cast, range 60 circle unsoaked tower
    SReisho = 33844, // Helper->self, no cast, range 6 circle
    SVengefulSouls = 33845, // SBoss->self, 15.0s cast, single-target, visual (towers/defamations)
    SVermilionAura = 33846, // Helper->self, 15.0s cast, range 4 circle tower
    SStygianAura = 33847, // Helper->players, 15.0s cast, range 15 circle spread
    SUnmitigatedExplosion = 33848, // Helper->self, 1.0s cast, range 60 circle unsoaked tower

    NThunderVortex = 33810, // NBoss->self, 5.0s cast, range 8-30 donut
    SThunderVortex = 33849, // SBoss->self, 5.0s cast, range 8-30 donut
    
    //------Boss2------
    Unenlightenment = 34100, // *Boss->self, 5.0s cast, single-target, visual (raidwide)
    NUnenlightenmentAOE = 34101, // Helper->self, no cast, range 60 circle, raidwide
    SUnenlightenmentAOE = 34133, // Helper->self, no cast, range 60 circle, raidwide

    SealOfScurryingSparks = 34051, // *Boss->self, 4.0s cast, single-target, visual (2-man stacks)
    SealOfScurryingSparksAOE = 34052, // Helper->player, no cast, single-target, visual (applies stack debuff)
    NGreaterBallOfFire = 34053, // Helper->players, no cast, range 6 circle, 2-man stack
    NGreatBallOfFire = 34054, // Helper->players, no cast, range 10 circle spread
    SGreaterBallOfFire = 34105, // Helper->players, no cast, range 6 circle, 2-man stack
    SGreatBallOfFire = 34106, // Helper->players, no cast, range 10 circle spread
    FlameAndSulphur = 34056, // *Boss->self, 3.0s cast, single-target, visual (create rocks)
    BrazenBalladExpanding = 34057, // *Boss->self, 5.0s cast, single-target, visual (expanding aoes)
    BrazenBalladSplitting = 34058, // *Boss->self, 5.0s cast, single-target, visual (splitting aoes)
    NFireSpreadExpand = 34059, // Helper->self, no cast, range 46 width 10 rect
    NFireSpreadSplit = 34060, // Helper->self, no cast, range 46 width 5 rect
    NFallingRockExpand = 34062, // Helper->self, no cast, range 11 circle
    NFallingRockSplit = 34063, // Helper->self, no cast, range 6-16 donut
    SFireSpreadExpand = 34108, // Helper->self, no cast, range 46 width 10 rect
    SFireSpreadSplit = 34109, // Helper->self, no cast, range 46 width 5 rect
    SFallingRockExpand = 34111, // Helper->self, no cast, range 11 circle
    SFallingRockSplit = 34112, // Helper->self, no cast, range 6-16 donut

    ImpurePurgation = 34095, // *Boss->self, 3.6s cast, single-target, visual (proteans)
    NImpurePurgationBait = 34096, // Helper->self, no cast, range 60 45-degree cone (baited)
    NImpurePurgationAOE = 34097, // Helper->self, 2.0s cast, range 60 45-degree cone (casted)
    SImpurePurgationBait = 34130, // Helper->self, no cast, range 60 45-degree cone (baited)
    SImpurePurgationAOE = 34131, // Helper->self, 2.0s cast, range 60 45-degree cone (casted)

    Thundercall = 34080, // *Boss->self, 3.0s cast, single-target, visual (create lightning orbs)
    HumbleHammer = 34084, // *Boss->self, 5.0s cast, single-target, visual (reduce size)
    NHumbleHammerAOE = 34085, // Helper->players, 5.0s cast, range 3 circle
    SHumbleHammerAOE = 34123, // Helper->players, 5.0s cast, range 3 circle
    ShockVisual = 34081, // *BallOfLevin->self, 7.0s cast, range 8 circle, visual
    NShockSmall = 34082, // NBallOfLevin->self, no cast, range 8 circle, small aoe
    NShockLarge = 34083, // NBallOfLevin->self, no cast, range 8+10 circle, large aoe
    SShockSmall = 34121, // SBallOfLevin->self, no cast, range 8 circle, small aoe
    SShockLarge = 34122, // SBallOfLevin->self, no cast, range 8+10 circle, large aoe
    Flintlock = 34086, // *Boss->self, no cast, single-target, visual (wild charge)
    NFlintlockAOE = 34087, // Helper->self, no cast, range 50 width 8 rect wild charge
    SFlintlockAOE = 34124, // Helper->self, no cast, range 50 width 8 rect wild charge

    TorchingTorment = 34098, // *Boss->player, 5.0s cast, single-target, visual (tankbuster)
    NTorchingTormentAOE = 34099, // Helper->player, no cast, range 6 circle tankbuster
    STorchingTormentAOE = 34132, // Helper->player, no cast, range 6 circle tankbuster

    RousingReincarnation = 34066, // *Boss->self, 5.0s cast, single-target, visual (towers)
    NRousingReincarnationAOE = 34067, // Helper->player, no cast, single-target, damage + debuffs
    SRousingReincarnationAOE = 34180, // Helper->player, no cast, single-target, damage + debuffs
    MalformedPrayer = 34072, // *Boss->self, 4.0s cast, single-target, visual (towers)
    PointedPurgation = 34077, // *Boss->self, 8.0s cast, single-target, visual (proteans on tethers)
    PointedPurgationRest = 34078, // *Boss->self, no cast, single-target, visual (proteans on tethers)
    NPointedPurgationAOE = 34079, // Helper->self, no cast, range 60 ?-degree cone
    SPointedPurgationAOE = 34120, // Helper->self, no cast, range 60 ?-degree cone
    NBurstOrange = 34073, // Helper->self, no cast, range 4 circle tower
    NDramaticBurstOrange = 34074, // Helper->self, no cast, range 60 circle unsoaked tower
    NBurstBlue = 34075, // Helper->self, no cast, range 4 circle tower
    NDramaticBurstBlue = 34076, // Helper->self, no cast, range 60 circle unsoaked tower
    SBurstOrange = 34116, // Helper->self, no cast, range 4 circle tower
    SDramaticBurstOrange = 34117, // Helper->self, no cast, range 60 circle unsoaked tower
    SBurstBlue = 34118, // Helper->self, no cast, range 4 circle tower
    SDramaticBurstBlue = 34119, // Helper->self, no cast, range 60 circle unsoaked tower
    //_Spell_OdderFodder = 34071, // Helper->self, no cast, range 60 circle

    CloudToGround = 34088, // *Boss->self, 6.2s cast, single-target, visual (exaflares)
    NCloudToGroundAOEFirst = 34089, // Helper->self, 7.0s cast, range 6 circle
    NCloudToGroundAOERest = 34090, // Helper->self, no cast, range 6 circle
    SCloudToGroundAOEFirst = 34125, // Helper->self, 7.0s cast, range 6 circle
    SCloudToGroundAOERest = 34126, // Helper->self, no cast, range 6 circle

    FightingSpirits = 34091, // *Boss->self, 5.0s cast, single-target, visual (limit cut)
    NFightingSpiritsAOE = 34092, // Helper->self, 6.2s cast, range 30 circle, knockback 16
    SFightingSpiritsAOE = 34127, // Helper->self, 6.2s cast, range 30 circle, knockback 16
    NWorldlyPursuitJump = 34093, // NBoss->location, no cast, single-target
    NWorldlyPursuitAOE = 34061, // Helper->self, 0.6s cast, range 60 width 20 cross
    SWorldlyPursuitJump = 34128, // SBoss->location, no cast, single-target
    SWorldlyPursuitAOE = 34110, // Helper->self, 0.6s cast, range 60 width 20 cross

    MalformedReincarnation = 34068, // *Boss->self, 5.0s cast, single-target, visual (last towers)
    NMalformedReincarnationAOE = 34069, // Helper->player, no cast, single-target, damage + debuffs
    SMalformedReincarnationAOE = 34181, // Helper->player, no cast, single-target, damage + debuffs
    FlickeringFlame = 34064, // *Boss->self, 3.0s cast, single-target, visual (criss-cross)
    NFireSpreadCross = 34065, // Helper->self, 3.5s cast, range 46 width 5 rect
    SFireSpreadCross = 34113, // Helper->self, 3.5s cast, range 46 width 5 rect
    
    //------Boss3------
    NKenkiRelease = 34272, // NBoss->self, 5.0s cast, range 60 circle, raidwide
    NLateralSlice = 34275, // NBoss->self/player, 5.0s cast, range 40 ?-degree cone
    SKenkiRelease = 34316, // SBoss->self, 5.0s cast, range 60 circle, raidwide
    SLateralSlice = 34317, // SBoss->self/player, 5.0s cast, range 40 ?-degree cone

    NTripleKasumiGiriOutFrontFirst = 34224, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutRightFirst = 34225, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutBackFirst = 34226, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutLeftFirst = 34227, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInFrontFirst = 34234, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInRightFirst = 34235, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInBackFirst = 34236, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInLeftFirst = 34237, // NBoss->self, 12.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutFrontRest = 34228, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutRightRest = 34229, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutBackRest = 34230, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriOutLeftRest = 34231, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInFrontRest = 34238, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInRightRest = 34239, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInBackRest = 34240, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NTripleKasumiGiriInLeftRest = 34241, // NBoss->self, 1.0s cast, range 60 270-degree cone
    NUnboundSpirit = 34232, // Helper->self, no cast, range 6 circle (kasumi-giri out)
    NAzureCoil = 34233, // Helper->self, no cast, range 6-40 donut (kasumi-giri in)
    STripleKasumiGiriOutFrontFirst = 34276, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutRightFirst = 34277, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutBackFirst = 34278, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutLeftFirst = 34279, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriInFrontFirst = 34286, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriInRightFirst = 34287, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriInBackFirst = 34288, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriInLeftFirst = 34289, // SBoss->self, 12.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutFrontRest = 34280, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutRightRest = 34281, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutBackRest = 34282, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriOutLeftRest = 34283, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriInFrontRest = 34290, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriInRightRest = 34291, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriInBackRest = 34292, // SBoss->self, 1.0s cast, range 60 270-degree cone
    STripleKasumiGiriInLeftRest = 34293, // SBoss->self, 1.0s cast, range 60 270-degree cone
    SUnboundSpirit = 34284, // Helper->self, no cast, range 6 circle (kasumi-giri out)
    SAzureCoil = 34285, // Helper->self, no cast, range 6-40 donut (kasumi-giri in)

    NScarletAuspice = 34257, // NBoss->self, 5.0s cast, range 6 circle
    SScarletAuspice = 34304, // SBoss->self, 5.0s cast, range 6 circle
    BoundlessScarlet = 34201, // *Boss->self, 2.4s cast, single-target, visual (expanding lines)
    NBoundlessScarletAOE = 34258, // Helper->self, 3.0s cast, range 60 width 10 rect
    NBoundlessScarletExplosion = 34259, // Helper->self, 12.0s cast, range 60 width 30 rect (expanded line)
    SBoundlessScarletAOE = 34305, // Helper->self, 3.0s cast, range 60 width 10 rect
    SBoundlessScarletExplosion = 34306, // Helper->self, 12.0s cast, range 60 width 30 rect (expanded line)
    InvocationOfVengeance = 34267, // *Boss->self, 3.0s cast, single-target, visual (tether)
    FleetingIaiGiri = 34242, // *Boss->self, 9.0s cast, single-target, visual (jumping cleave)
    FleetingIaiGiriJump = 34243, // *Boss->location, no cast, single-target, teleport dist 3 behind target
    NFleetingIaiGiriFront = 34244, // Boss->self, 1.0s cast, range 60 270-degree cone
    NFleetingIaiGiriRight = 34245, // Boss->self, 1.0s cast, range 60 270-degree cone
    NFleetingIaiGiriLeft = 34246, // Boss->self, 1.0s cast, range 60 270-degree cone
    SFleetingIaiGiriFront = 34294, // Boss->self, 1.0s cast, range 60 270-degree cone
    SFleetingIaiGiriRight = 34295, // Boss->self, 1.0s cast, range 60 270-degree cone
    SFleetingIaiGiriLeft = 34296, // Boss->self, 1.0s cast, range 60 270-degree cone
    NVengefulFlame = 34268, // Helper->players, no cast, range 3 circle spread
    NVengefulPyre = 34269, // Helper->players, no cast, range 3 circle 2-man stack
    SVengefulFlame = 34312, // Helper->players, no cast, range 3 circle spread
    SVengefulPyre = 34313, // Helper->players, no cast, range 3 circle 2-man stack

    ShadowTwin = 34247, // Boss->self, 3.0s cast, single-target, visual (mechanic start)
    DoubleIaiGiri = 34248, // *MokoShadow->self, 12.0s cast, single-target, visual (jump + cleaves)
    ShadowKasumiGiriJump = 34249, // NMokoShadow->location, no cast, single-target
    NShadowKasumiGiriFrontFirst = 34250, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriRightFirst = 34251, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriBackFirst = 34252, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriLeftFirst = 34253, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriFrontFirst = 34297, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriRightFirst = 34298, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriBackFirst = 34299, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriLeftFirst = 34300, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriFrontSecond = 34499, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriRightSecond = 34500, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriBackSecond = 34501, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NShadowKasumiGiriLeftSecond = 34502, // NMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriFrontSecond = 34507, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriRightSecond = 34508, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriBackSecond = 34509, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    SShadowKasumiGiriLeftSecond = 34510, // SMokoShadow->self, 1.0s cast, range 23 270-degree cone
    NMoonlessNight = 34270, // NBoss->self, 3.0s cast, range 60 circle, raidwide
    SMoonlessNight = 34314, // SBoss->self, 3.0s cast, range 60 circle, raidwide
    FarEdge = 34264, // *Boss->self, 6.0s cast, single-target, visual (accursed edge on 2 farthest)
    NearEdge = 34265, // *Boss->self, 6.0s cast, single-target (accursed edge on 2 closest)
    NAccursedEdge = 34266, // NAncientKatana->self, no cast, range 6 circle
    SAccursedEdge = 34311, // SAncientKatana->self, no cast, range 6 circle
    // TODO: clarify meaning, add remaining spells incl. savage variants...
    NClearout1 = 35873, // NOniClaw->self, 6.0s cast, range 22 180-degree cone
    NClearout2 = 35879, // NOniClaw->self, no cast, range 22 180-degree cone
    NClearout3 = 34271, // NOniClaw->self, no cast, range 22 180-degree cone

    NAzureAuspice = 34260, // NBoss->self, 5.0s cast, range ?-40 donut
    SAzureAuspice = 34307, // SBoss->self, 5.0s cast, range ?-40 donut
    BoundlessAzure = 34205, // *Boss->self, 2.4s cast, single-target, visual (splitting lines)
    NBoundlessAzureAOE = 34261, // Helper->self, 3.0s cast, range 60 width 10 rect
    NUpwellFirst = 34262, // Helper->self, 7.0s cast, range 60 width 10 rect
    NUpwellRest = 34263, // Helper->self, no cast, range 60 width 5 rect
    SBoundlessAzureAOE = 34308, // Helper->self, 3.0s cast, range 60 width 10 rect
    SUpwellFirst = 34309, // Helper->self, 7.0s cast, range 60 width 10 rect
    SUpwellRest = 34310, // Helper->self, no cast, range 60 width 5 rect

    SoldiersOfDeath = 34195, // *Boss->self, 3.0s cast, single-target, visual (mechanic start)
    NIronRainFirst = 34255, // NAshigaruKyuhei->location, 15.0s cast, range 10 circle
    NIronStormFirst = 34256, // NAshigaruKyuhei->location, 15.0s cast, range 20 circle
    NIronRainSecond = 34727, // NAshigaruKyuhei->location, 1.0s cast, range 10 circle
    NIronStormSecond = 34728, // NAshigaruKyuhei->location, 1.0s cast, range 20 circle
    SIronRainFirst = 34302, // SAshigaruKyuhei->location, 15.0s cast, range 10 circle
    SIronStormFirst = 34303, // SAshigaruKyuhei->location, 15.0s cast, range 20 circle
    SIronRainSecond = 34729, // SAshigaruKyuhei->location, 1.0s cast, range 10 circle
    SIronStormSecond = 34730, // SAshigaruKyuhei->location, 1.0s cast, range 20 circle

    NKenkiReleaseShadow = 34254, // NMokoShadow->self, 3.0s cast, range 60 circle
}

public enum StatusId : uint
{
    //------Boss1------
    ScatteredWailing = 3563, // *Boss->player, extra=0x0
    IntensifiedWailing = 3564, // *Boss->player, extra=0x0
    
    //------Boss2------
    LiveBrazier = 3607, // none->player, extra=0x0, stack
    LiveCandle = 3608, // none->player, extra=0x0, spread
    RodentialRebirth1 = 3597, // none->player, extra=0x0 (orange 1)
    RodentialRebirth2 = 3598, // none->player, extra=0x0 (orange 2)
    RodentialRebirth3 = 3599, // none->player, extra=0x0 (orange 3)
    RodentialRebirth4 = 3600, // none->player, extra=0x0 (orange 4)
    OdderIncarnation1 = 3601, // none->player, extra=0x0 (blue 1)
    OdderIncarnation2 = 3602, // none->player, extra=0x0 (blue 2)
    OdderIncarnation3 = 3603, // none->player, extra=0x0 (blue 3)
    OdderIncarnation4 = 3604, // none->player, extra=0x0 (blue 4)
    SquirrellyPrayer = 3605, // none->player, extra=0x0 (orange)
    OdderPrayer = 3606, // none->player, extra=0x0 (blue)
    
    //------Boss3------
    Giri = 2970, // none->*Boss/*MokoShadow, extra=0x248 (front)/0x249 (right)/0x24A (back)/0x24B (left)/0x24C (out-front)/0x24D (out-right)/0x24E (out-back)/0x24F (out-left)/0x250 (in-front)/0x251 (in-right)/0x252 (in-back)/0x253 (in-left)
    RatAndMouse = 3609, // none->player, extra=0x0, jump target
    VengefulFlame = 3610, // none->player, extra=0x0, spread
    VengefulPyre = 3611, // none->player, extra=0x0, stack
}

public enum IconId : uint
{
    HumbleHammer = 27, // player
    TorchingTorment = 344, // player
    Order1 = 336, // player
    Order2 = 337, // player
    Order3 = 338, // player
    Order4 = 339, // player
}

public enum TetherId : uint
{
    RousingReincarnation = 248, // player->NBoss
    PointedPurgation = 89, // player->NBoss
    RatAndMouse = 17, // *Boss/*MokoShadow->player
}


#endregion