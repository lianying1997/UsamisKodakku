using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommons;
using System.Linq;
using ImGuiNET;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using KodakkuAssist.Module.GameOperate;


namespace KarlinScriptNamespace
{
    [ScriptType(name: "M1s绘图+", territorys: [1226], guid: "2ccfe4a1-d36b-4fc1-8a14-0657ec062b8c", version: "0.0.0.6.alpha", author: "Karlin", note: "Original code by Karlin, minor adjustments by Usami. \n基于原v0.0.0.6版本增加个人风格的拉怪指引。")]
    public class M1sDraw
    {
        [UserSetting("地板修复击退,Mt组安全半场")]
        public KnockBackMtPosition MtSafeFloor { get; set; }

        public enum KnockBackMtPosition
        {
            NorthHalf,
            SouthHalf,
            EastHalf,
            WestHalf
        }

        [UserSetting("P3_分身目的地标记颜色设置")]
        public ScriptColor color_P3_copyCatDestination { get; set; } = new();


        // 《新增》
        [UserSetting("P3_启用T拉怪辅助指路")]
        public bool P3_TankPullAssist { get; set; } = true;

        // 《新增》
        [UserSetting("P3_启用聊天框指路提示")]
        public bool P3_ChatGuidance { get; set; } = false;

        int? firstTargetIcon = null;
        List<int> FloorBrokeList = new();
        uint copyCatTarget;
        uint parse;
        List<uint> P3TetherTarget = new();
        List<string> P3JumpSkill = new();
        // bool isFastLeft = false;

        // 《新增》
        Vector3[] P3_copyCatPos = new Vector3[2];

        // 《新增》
        int P3_copyCatCount;



        public void Init(ScriptAccessory accessory)
        {
            accessory.Method.RemoveDraw(@".*");
            //accessory.Method.MarkClear();

            firstTargetIcon = null;
            parse = 1;
            P3TetherTarget = new();
            P3JumpSkill = new();
            // isFastLeft = 0;
            P3_copyCatCount = 0;
            P3_copyCatPos = new Vector3[2];

            // accessory.Log.Debug($"检测到重新开始……此时parse为{parse}");

        }

        // 释放某两个技能分P
        [ScriptMethod(name: "分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(38036|37963)$"], userControl: false)]
        public void 分P(Event @event, ScriptAccessory accessory)
        {
            parse++;
            accessory.Method.RemoveDraw("跳跃目的地-A");
            accessory.Method.RemoveDraw("跳跃目的地-C");
            // accessory.Log.Debug($"现在是阶段：{parse}……");
        }

        // 开始释放 Quadruple Crossing (cast) 0x943C 37948
        [ScriptMethod(name: "扇形引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37948"])]
        public void 扇形引导(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var Interval = 1000;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-1";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            // 将目标始终取位最近的玩家，做一个list，由TargetOrderIndex决定谁离得最近
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-2";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-3";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-4";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-1";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            // Delay延时，可以决定SendDraw后多久画画
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);



            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-2";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-3";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-4";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "扇形引导二段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37952"])]
        public void 扇形引导二段(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导二段";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.DestoryAt = 1500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        // 37947 0x943B 第一刀左 isleft isfast
        // 37943 0x9437 第一刀右 ------ isfast
        // 37944 0x9438 第二刀左 isleft ------
        // 37946 0x943A 第二刀右 ------ ------
        [ScriptMethod(name: "左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^3794[3467]$"])]
        public void 左右刀(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var idStr = @event["ActionId"];
            var isfast = (idStr == "37943" || idStr == "37947");
            var isleft = (idStr == "37947" || idStr == "37944");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"左右刀-{(isleft ? "左" : "右")}{(isfast ? "快" : "慢")}";
            dp.Scale = new(40, 20);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
            dp.Owner = sid;
            dp.Rotation = isleft ? float.Pi / 2 : float.Pi / -2;
            dp.Delay = isfast ? 0 : 6000;
            dp.DestoryAt = isfast ? 6000 : 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        [ScriptMethod(name: "捕捉本体左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^3794[37]$"])]
        public void 捕捉左右刀(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var idStr = @event["ActionId"];
            // isFastLeft = (idStr == "37947");
        }

        // 37993 0x9469 第一刀左 isleft isfast
        // 37989 0x9465 第一刀右 ------ isfast
        // 37990 0x9466 第二刀左 isleft ------
        // 37992 0x9468 第二刀右 ------ ------
        // 这是那个后面接分摊的机制
        [ScriptMethod(name: "分身左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(37989|3799[023])$"])]
        public void 分身左右刀(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            var idStr = @event["ActionId"];
            var isfast = (idStr == "37989" || idStr == "37993");
            var isleft = (idStr == "37993" || idStr == "37990");

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"分身左右刀-{(isleft ? "左" : "右")}{(isfast ? "快" : "慢")}";
            dp.Scale = new(100);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
            dp.Owner = sid;
            dp.Rotation = isleft ? float.Pi / 2 : float.Pi / -2;
            dp.Delay = isfast ? 0 : 6000;
            dp.DestoryAt = isfast ? 6000 : 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            // string direction = isFastLeft == 1 ? "左正" : "右斜";
            // accessory.Method.TextInfo($"脑海里出现【{direction}】的想法……", 6000);
        }

        // 37965 0x944D 左跳右刀 左西，场外 => 场内
        // 37966 0x944E 左跳左刀 左西，场内 => 场外
        // 37967 0x944F 右跳右刀 右东，场内 => 场外
        // 37968 0x9450 右跳左刀 右东，场外 => 场内
        [ScriptMethod(name: "跳跃左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^3796[5678]$"])]
        public void 跳跃左右刀(Event @event, ScriptAccessory accessory)
        {
            var actionId = @event["ActionId"];
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var leftJump = (actionId == "37965" || actionId == "37966");
            var leftFast = (actionId == "37966" || actionId == "37968");
            Vector3 dv;
            if (leftJump) dv = new(-10, 0, 0);
            else dv = new(10, 0, 0);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"跳跃左右刀-{(leftJump ? "左" : "右")}跳{(leftFast ? "左" : "右")}刀快";
            dp.Position = pos + dv;
            dp.Scale = new(60, 30);
            dp.Rotation = leftFast ? float.Pi / -2 : float.Pi / 2;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(3); ;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"跳跃左右刀-{(leftJump ? "左" : "右")}跳{(leftFast ? "右" : "左")}刀慢";
            dp.Position = pos + dv;
            dp.Scale = new(60, 30);
            dp.Rotation = leftFast ? float.Pi / 2 : float.Pi / -2;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(3); ;
            dp.Delay = 7000;
            dp.DestoryAt = 2000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        // 38959 0x982F 右跳扇形
        // 37975 0x9457 左跳扇形
        [ScriptMethod(name: "跳跃扇形引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(38959|37975)$"])]
        public void 跳跃扇形引导(Event @event, ScriptAccessory accessory)
        {
            // 获得当前的位置
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

            // 如果是左跳，X轴向左偏移10，否则向右偏移10，但是……
            Vector3 dv = @event["ActionId"] == "37975" ? new(-10, 0, 0) : new(10, 0, 0);
            // 若此时Boss位置在X=110，面向场中，那不该让X轴向左偏移10，而是Y轴向下偏移10（+10）
            if (Math.Abs(pos.X - 110) < 1)
            {
                dv = @event["ActionId"] == "37975" ? new(0, 0, 10) : new(0, 0, -10);
            }
            // 同理，若此时Boss位置在X=90，面向场中，那不该让X轴向左偏移10，而是Y轴向上偏移10（-10）
            if (Math.Abs(pos.X - 90) < 1)
            {
                dv = @event["ActionId"] == "37975" ? new(0, 0, -10) : new(0, 0, 10);
            }
            var Interval = 1000;

            // 计算完偏移坐标后，使画图的扇形起始位置Position设置为(pos + dv)
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-1";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.Delay = 0;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-2";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Delay = 0;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-3";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Delay = 0;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-1-4";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Delay = 0;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-1";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 1;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-2";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 2;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-3";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 3;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导-2-4";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Position = pos + dv;
            dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.TargetOrderIndex = 4;
            dp.Delay = 6000 + Interval;
            dp.DestoryAt = 3000 - Interval;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        [ScriptMethod(name: "跳跃扇形引导二段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37980"])]
        public void 跳跃扇形引导二段(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "扇形引导二段";
            dp.Scale = new(100);
            dp.Radian = float.Pi / 4;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.DestoryAt = 1500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        // 一个新的Method Tether，似乎记录了Boss对分身的连线
        [ScriptMethod(name: "P3连线收集", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0066"], userControl: false)]
        public void 本体连线收集(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            P3TetherTarget.Add(sid);
            // accessory.Log.Debug($"执行了一次“P3连线收集”行为……");
        }

        // 本体在P3释放
        [ScriptMethod(name: "P3跳跃技能收集", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(38959|37975|3796[5678])$"], userControl: false)]
        public void P3跳跃技能收集(Event @event, ScriptAccessory accessory)
        {
            //38959 右扇        982F
            //37975 左扇        9457
            //37965 左跳右刀    944D
            //37966 左跳左刀    944E
            //37967 右跳右刀    944F
            //37968 右跳左刀    9450
            if (parse != 3) return;
            P3JumpSkill.Add(@event["ActionId"]);
            // accessory.Log.Debug($"执行了一次“P3跳跃技能收集”行为……");
        }

        // TODO：是否要保留？保留吧。
        [ScriptMethod(name: "P3_跳跃技能收集2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(38959|37975|3796[5678])$"], userControl: false)]
        public void P3_跳跃技能收集2(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            P3_copyCatCount++;
        }

        // TODO：需要确定的是，P3TetherTarget.Count < 3 return这个事，发生什么情况会使TetherTarget > 2？
        // ! 出现BUG，最后一把没有标记？
        // ! 是未解决的战斗结束重置问题
        [ScriptMethod(name: "P3_跳跃目的地标记", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0066"])]
        public void P3_跳跃目的地标记(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            // accessory.Log.Debug($"执行了一次“P3_跳跃目的地标记”行为……");

            // 记录下被释放SoulShade的分身位置，找到对应的skillId，判断分身是否位于北
            var tpos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            // var skillId = P3JumpSkill[P3_copyCatCount - 1];
            var skillId = P3JumpSkill[P3TetherTarget.IndexOf(sid)];
            var isNorthCopy = Math.Abs(tpos.Z - 95) < 1;

            Vector3 dv = default;

            if (isNorthCopy)
            {                
                dv = isLeftJumpSkill(skillId) ? new(10, 0, 0) : new(-10, 0, 0);
                string mentionTxt = $"{(isLeftJumpSkill(skillId) ? "2" : "1")}{(isFanSkill(skillId) ? "扇" : (isOutSafeFirst(skillId) ? "外" : "内"))}";
                accessory.Method.TextInfo($"似乎可以提醒队友：{mentionTxt}……", 5000);
                if (P3_ChatGuidance)
                {
                    accessory.Method.SendChat($"/p ---- {mentionTxt} ----");
                } else {
                    accessory.Method.SendChat($"/e ---- {mentionTxt} ----");
                }
            }
            else
            {                
                dv = isLeftJumpSkill(skillId) ? new(-10, 0, 0) : new(10, 0, 0);
                string mentionTxt = $"{(isLeftJumpSkill(skillId) ? "4" : "3")}{(isFanSkill(skillId) ? "扇" : (isOutSafeFirst(skillId) ? "外" : "内"))}";
                accessory.Method.TextInfo($"似乎可以提醒队友：{mentionTxt}……", 5000);
                if (P3_ChatGuidance)
                {
                    accessory.Method.SendChat($"/p ---- {mentionTxt} ----");
                } else {
                    accessory.Method.SendChat($"/e ---- {mentionTxt} ----");
                }
            }

            Vector3 destinationPos = default;
            destinationPos = tpos + dv;
            P3_copyCatPos[P3_copyCatCount - 1] = destinationPos;

            if (P3_copyCatCount > 2) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"跳跃目的地-{(isNorthCopy ? "A" : "C")}";
            dp.Scale = new(2);
            dp.Color = color_P3_copyCatDestination.V4;
            dp.Position = destinationPos;
            dp.Delay = 0;
            dp.DestoryAt = 60000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }

        // TODO：拉起始
        [ScriptMethod(name: "P3_T拉怪辅助", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0066"])]
        public void P3_T拉怪辅助(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            if (P3_copyCatCount < 2) return;
            Vector3 startPos = default;

            var isOnSameSide = Math.Abs(P3_copyCatPos[0].X - P3_copyCatPos[1].X) < 1;
            // 此时具备判断条件，可以判断拉怪起点
            if (isOnSameSide)
            {
                startPos = new(P3_copyCatPos[0].X, 0, 100);
                // accessory.Log.Debug($"检测到分身目的地在同侧，定义拉怪起始点于一侧……");
            }
            else
            {
                startPos = new(100, 0, 100);
                // accessory.Log.Debug($"检测到分身目的地在异侧，定义拉怪起始点于场中……");
            }

            if (P3_TankPullAssist)
            {
                var dp0 = accessory.Data.GetDefaultDrawProperties();
                dp0 = accessory.Data.GetDefaultDrawProperties();
                dp0.Name = $"拉怪辅助-起始";
                dp0.Scale = new(2);
                dp0.Owner = tid;
                dp0.TargetPosition = startPos;
                dp0.ScaleMode |= ScaleMode.YByDistance;
                dp0.Color = color_P3_copyCatDestination.V4;
                dp0.DestoryAt = 60000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
            }
        }

        // TODO：拉过程
        [ScriptMethod(name: "P3_跳跃目的地标记_换色与辅助", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0066"])]
        public void P3_跳跃目的地标记_换色与辅助(Event @event, ScriptAccessory accessory)
        {
            // 其实可以通过第一次的技能释放，获得两次指路的路径。但没有必要。
            if (parse != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            
            Task.Delay(100).ContinueWith((t) =>
            {                
                if (P3TetherTarget.Count < 3) return;
                // accessory.Log.Debug($"执行了一次“P3_跳跃目的地标记_换色与辅助”行为……");

                var skillId = P3JumpSkill[P3TetherTarget.IndexOf(sid)];
                Vector3 destinationPos = default;
                destinationPos = P3_copyCatPos[P3TetherTarget.IndexOf(sid)];
                // accessory.Log.Debug($"destinationPos: {destinationPos}……");
                if (P3_TankPullAssist)
                {
                    accessory.Method.RemoveDraw("拉怪辅助-起始");

                    var dp0 = accessory.Data.GetDefaultDrawProperties();
                    dp0 = accessory.Data.GetDefaultDrawProperties();
                    dp0.Name = $"拉怪辅助-分身";
                    dp0.Scale = new(2);
                    dp0.Owner = tid;
                    dp0.TargetPosition = destinationPos;
                    dp0.ScaleMode |= ScaleMode.YByDistance;
                    dp0.Color = color_P3_copyCatDestination.V4;
                    dp0.DestoryAt = 20000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
                }

            });
        }

        [ScriptMethod(name: "P3分身连线技能", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0066"])]
        public void P3分身连线技能(Event @event, ScriptAccessory accessory)
        {
            if (parse != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            Task.Delay(100).ContinueWith((t) =>
            {
                //38959 右扇
                //37975 左扇
                //37965 左跳右刀
                //37966 左跳左刀
                //37967 右跳右刀
                //37968 右跳左刀
                if (P3TetherTarget.Count < 3) return;
                var skillId = P3JumpSkill[P3TetherTarget.IndexOf(sid)];

                if (skillId == "38959" || skillId == "37975")
                {
                    var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var leftJump = (skillId == "37975");
                    var isNouthCopy = Math.Abs(pos.Z - 95) < 1;
                    Vector3 dv = new(0, 0, 0);
                    if (isNouthCopy)
                    {
                        dv = leftJump ? new(10, 0, 0) : new(-10, 0, 0);
                    }
                    else
                    {
                        dv = leftJump ? new(-10, 0, 0) : new(10, 0, 0);
                    }

                    var Interval = 1000;

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-1-1";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 1;
                    dp.Delay = 0;
                    dp.DestoryAt = 17000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-1-2";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 2;
                    dp.Delay = 0;
                    dp.DestoryAt = 17000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-1-3";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 3;
                    dp.Delay = 0;
                    dp.DestoryAt = 17000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-1-4";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 4;
                    dp.Delay = 0;
                    dp.DestoryAt = 17000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-2-1";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 1;
                    dp.Delay = 17000 + Interval;
                    dp.DestoryAt = 3000 - Interval;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);



                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-2-2";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 2;
                    dp.Delay = 17000 + Interval;
                    dp.DestoryAt = 3000 - Interval;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-2-3";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 3;
                    dp.Delay = 17000 + Interval;
                    dp.DestoryAt = 3000 - Interval;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);


                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"分身跳跃扇形-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳引导-2-4";
                    dp.Scale = new(100);
                    dp.Radian = float.Pi / 4;
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Position = pos + dv;
                    dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
                    dp.TargetOrderIndex = 4;
                    dp.Delay = 17000 + Interval;
                    dp.DestoryAt = 3000 - Interval;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }
                if (skillId == "37965" || skillId == "37966" || skillId == "37967" || skillId == "37968")
                {
                    var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
                    var leftJump = (skillId == "37965" || skillId == "37966");
                    var leftFast = (skillId == "37966" || skillId == "37968");
                    var isNouthCopy = Math.Abs(pos.Z - 95) < 1;
                    Vector3 dv = new(0, 0, 0);

                    if (isNouthCopy)
                    {
                        dv = leftJump ? new(10, 0, 0) : new(-10, 0, 0);
                    }
                    else
                    {
                        dv = leftJump ? new(-10, 0, 0) : new(10, 0, 0);
                    }

                    var rotation = leftFast ? float.Pi / -2 : float.Pi / 2;
                    rotation += isNouthCopy ? float.Pi : 0;

                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"跳跃左右刀-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳{(leftFast ? "左" : "右")}刀快";
                    dp.Position = pos + dv;
                    dp.Scale = new(60, 30);
                    dp.Rotation = rotation;
                    dp.Color = accessory.Data.DefaultDangerColor.WithW(3); ;
                    dp.DestoryAt = 16000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"跳跃左右刀-{(isNouthCopy ? "北" : "南")}分身{(leftJump ? "左" : "右")}跳{(leftFast ? "左" : "右")}刀慢";
                    dp.Position = pos + dv;
                    dp.Scale = new(60, 30);
                    dp.Rotation = -rotation;
                    dp.Color = accessory.Data.DefaultDangerColor.WithW(3); ;
                    dp.Delay = 16000;
                    dp.DestoryAt = 2000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }

            });
        }



        [ScriptMethod(name: "双人分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(37982|38016)$"])]
        public void 双人分摊(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["TargetId"], out var sid)) return;

            // D3 D2 D1 D4 H1 ST MT H2
            // 这个意思就是，MT要找D3，ST要找D2，H1要找D1，H2要找D4，这里的参数是搭档的Index，后续同理
            // 所以该小队列表的顺序应当为MT-ST-H1-H2-D1-D2-D3-D4
            int[] stackGroup = [6, 5, 4, 7, 2, 1, 0, 3];
            var index = accessory.Data.PartyList.ToList().IndexOf(sid);
            var myIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);
            var isMyStack = (index == myIndex || myIndex == stackGroup[index]);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"双人分摊";
            dp.Scale = new(4);
            dp.Color = isMyStack ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        [ScriptMethod(name: "四人分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(37984|38018)$"])]
        public void 四人分摊(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["TargetId"], out var sid)) return;

            int[] h1Group = [0, 2, 4, 6];
            var index = accessory.Data.PartyList.ToList().IndexOf(sid);
            var myIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

            var isMyStack = (h1Group.Contains(index) == h1Group.Contains(myIndex));

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"双人分摊";
            dp.Scale = new(5);
            dp.Color = isMyStack ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        [ScriptMethod(name: "四人直线分摊", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:34722"])]
        public void 四人直线分摊(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;

            int[] h1Group = [0, 2, 4, 6];
            var index = accessory.Data.PartyList.ToList().IndexOf(tid);
            var myIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

            var isMyStack = (h1Group.Contains(index) == h1Group.Contains(myIndex));

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"双人分摊";
            dp.Scale = new(6, 40);
            dp.Color = isMyStack ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.TargetObject = tid;
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "七人直线分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:38039"])]
        public void 七人直线分摊(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"七人直线分摊";
            dp.Scale = new(5, 40);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = sid;
            dp.TargetObject = tid;
            dp.DestoryAt = 5500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        [ScriptMethod(name: "风圈分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:38022"])]
        public void 风圈分散(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["TargetId"], out var sid)) return;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"风圈分散";
            dp.Scale = new(5);
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = sid;
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            if (sid == accessory.Data.Me) accessory.Method.TextInfo("风圈散开", 8000, true);
        }
        [ScriptMethod(name: "职能分散提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:38041"])]
        public void 职能分散提示(Event @event, ScriptAccessory accessory)
        {
            accessory.Method.TextInfo("职能分散", 5500, false);
        }

        [ScriptMethod(name: "地板破坏安全区重置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37953"], userControl: false)]
        public void 地板破坏安全区重置(Event @event, ScriptAccessory accessory)
        {
            FloorBrokeList = new();
        }
        [ScriptMethod(name: "地板破坏安全区", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(39276|37955)$"])]
        public void 地板破坏安全区(Event @event, ScriptAccessory accessory)
        {
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var centre = new Vector3(100, 0, 100);
            var dv = pos - centre;
            if (dv.Length() > 10) return;
            lock (FloorBrokeList)
            {
                var index = FloorToIndex(pos);
                FloorBrokeList.Add(index);

                if (FloorBrokeList.Count == 5)
                {
                    var during = 20000;
                    var nwSafe = (index == 0 || index == 2);
                    Vector3 endPos = default;
                    if (accessory.Data.PartyList.IndexOf(accessory.Data.Me) == 0)
                    {
                        endPos = nwSafe ? new Vector3(95, 0, 95) : new Vector3(105, 0, 95);
                    }
                    else
                    {
                        endPos = nwSafe ? new Vector3(105, 0, 105) : new Vector3(95, 0, 105);
                    }
                    var safePosIndex1 = FloorToIndex(endPos);

                    var safePosBrokeIndex = FloorBrokeList.IndexOf(safePosIndex1);
                    if (safePosBrokeIndex == 0)
                    {
                        var startPos = Math.Abs(FloorBrokeList[3] - safePosIndex1) % 4 == 1 ? IndexToFloor(FloorBrokeList[3]) : IndexToFloor(FloorBrokeList[2]);
                        var dp = accessory.Data.GetDefaultDrawProperties();
                        dp.Name = $"地板破坏安全区";
                        dp.Scale = new(2);
                        dp.Position = startPos;
                        dp.TargetPosition = endPos;
                        dp.ScaleMode |= ScaleMode.YByDistance;
                        dp.Color = accessory.Data.DefaultSafeColor;
                        dp.DestoryAt = during;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                    }
                    if (safePosBrokeIndex == 1)
                    {
                        if (Math.Abs(FloorBrokeList[3] - safePosIndex1) % 4 == 1)
                        {
                            var startPos = IndexToFloor(FloorBrokeList[3]);
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = startPos;
                            dp.TargetPosition = endPos;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            var startPos = IndexToFloor(FloorBrokeList[3]);
                            var pos2 = IndexToFloor(FloorBrokeList[0]);
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = startPos;
                            dp.TargetPosition = pos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos2;
                            dp.TargetPosition = endPos;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }

                    }
                    if (safePosBrokeIndex == 2)
                    {
                        if (Math.Abs(FloorBrokeList[0] - safePosIndex1) % 4 == 1)
                        {
                            var pos2 = IndexToFloor(FloorBrokeList[0]);
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = endPos;
                            dp.TargetPosition = pos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos2;
                            dp.TargetPosition = new((endPos.X - 100) * 0.6f + 100, 0, (endPos.Z - 100) * 0.6f + 100);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            var pos1 = IndexToFloor(FloorBrokeList[3]);
                            var pos2 = IndexToFloor(FloorBrokeList[0]);
                            var pos3 = IndexToFloor(FloorBrokeList[1]);

                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos1;
                            dp.TargetPosition = pos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos2;
                            dp.TargetPosition = pos3;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos3;
                            dp.TargetPosition = endPos;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }
                    if (safePosBrokeIndex == 3)
                    {
                        if (Math.Abs(FloorBrokeList[0] - safePosIndex1) % 4 == 1)
                        {
                            var pos2 = IndexToFloor(FloorBrokeList[0]);
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = endPos;
                            dp.TargetPosition = pos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos2;
                            dp.TargetPosition = new((endPos.X - 100) * 0.6f + 100, 0, (endPos.Z - 100) * 0.6f + 100);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                        else
                        {
                            var pos2 = IndexToFloor(FloorBrokeList[1]);
                            var dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = endPos;
                            dp.TargetPosition = pos2;
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                            dp = accessory.Data.GetDefaultDrawProperties();
                            dp.Name = $"地板破坏安全区";
                            dp.Scale = new(2);
                            dp.Position = pos2;
                            dp.TargetPosition = new((endPos.X - 100) * 0.6f + 100, 0, (endPos.Z - 100) * 0.6f + 100);
                            dp.ScaleMode |= ScaleMode.YByDistance;
                            dp.Color = accessory.Data.DefaultSafeColor;
                            dp.DestoryAt = during;
                            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                        }
                    }











                }






            }



        }



        [ScriptMethod(name: "分身猫爪点名", eventType: EventTypeEnum.TargetIcon, userControl: false)]
        public void 分身猫爪点名(Event @event, ScriptAccessory accessory)
        {
            if (ParsTargetIcon(@event["Id"]) != 320) return;
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            copyCatTarget = tid;
        }
        [ScriptMethod(name: "分身砸地击飞", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37958"])]
        public void 分身砸地击飞(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(50).ContinueWith(t =>
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "分身砸地击飞";
                dp.Scale = new(1.5f, 10);
                dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
                dp.Owner = copyCatTarget;
                dp.DestoryAt = 8000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            });
        }

        [ScriptMethod(name: "分身砸地十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(37960|37958)$"])]
        public void 分身砸地十字(Event @event, ScriptAccessory accessory)
        {
            Task.Delay(50).ContinueWith(t =>
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "分身砸地十字";
                dp.Scale = new(1.5f, 80);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Owner = copyCatTarget;
                dp.FixRotation = true;
                dp.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "分身砸地十字";
                dp.Scale = new(1.5f, 80);
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Owner = copyCatTarget;
                dp.FixRotation = true;
                dp.Rotation = float.Pi / 2;
                dp.DestoryAt = 9000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
            });
        }

        [ScriptMethod(name: "地板修复安全区", eventType: EventTypeEnum.EnvControl, eventCondition: ["Id:00080004", "Index:regex:^(0000000[1247])$"])]
        public void 地板修复安全区(Event @event, ScriptAccessory accessory)
        {
            var myIndex = accessory.Data.PartyList.ToList().IndexOf(accessory.Data.Me);

            if (MtSafeFloor == KnockBackMtPosition.SouthHalf || MtSafeFloor == KnockBackMtPosition.NorthHalf)
            {
                int[] northGroup = MtSafeFloor == KnockBackMtPosition.NorthHalf ? [0, 2, 4, 6] : [1, 3, 5, 7];
                var isNorthGroup = northGroup.Contains(myIndex);
                if (@event["Index"] == "00000001")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(20f, 10);
                    dp.Position = isNorthGroup ? new(90, 0, 85) : new(110, 0, 115);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000002")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(20f, 10);
                    dp.Position = isNorthGroup ? new(110, 0, 85) : new(90, 0, 115);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000004")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(10, 20);
                    dp.Position = isNorthGroup ? new(85, 0, 90) : new(115, 0, 110);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000007")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(10, 20);
                    dp.Position = isNorthGroup ? new(115, 0, 90) : new(85, 0, 110);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
            }
            else
            {
                int[] eastGroup = MtSafeFloor == KnockBackMtPosition.EastHalf ? [0, 2, 4, 6] : [1, 3, 5, 7];
                var isEastGroup = eastGroup.Contains(myIndex);
                if (@event["Index"] == "00000001")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(20f, 10);
                    dp.Position = isEastGroup ? new(110, 0, 115) : new(90, 0, 85);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000002")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(20f, 10);
                    dp.Position = isEastGroup ? new(110, 0, 85) : new(90, 0, 115);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000004")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(10, 20);
                    dp.Position = isEastGroup ? new(115, 0, 110) : new(85, 0, 90);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
                if (@event["Index"] == "00000007")
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"地板修复安全区";
                    dp.Scale = new(10, 20);
                    dp.Position = isEastGroup ? new(115, 0, 90) : new(85, 0, 110);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.DestoryAt = 9000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Straight, dp);
                }
            }


        }
        [ScriptMethod(name: "击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:37964"])]
        public void 击退(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"击退";
            dp.Scale = new(1.5f, 21);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetObject = sid;
            dp.Rotation = float.Pi;
            dp.DestoryAt = 7000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        [ScriptMethod(name: "远近分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3961[12])$"])]
        public void 远近分摊(Event @event, ScriptAccessory accessory)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
            dur += 1300;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"远近分摊近";
            dp.Scale = new(4);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = sid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"远近分摊远";
            dp.Scale = new(4);
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = sid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            dp.DestoryAt = dur;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }


        private static bool ParseObjectId(string? idStr, out uint id)
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
        private int ParsTargetIcon(string id)
        {
            firstTargetIcon ??= int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return int.Parse(id, System.Globalization.NumberStyles.HexNumber) - (int)firstTargetIcon;
        }

        private int FloorToIndex(Vector3 pos)
        {
            var centre = new Vector3(100, 0, 100);
            var dv = pos - centre;
            var index = 0;
            if (dv.X > 0)
            {
                if (dv.Z > 0)
                {
                    index = 3;
                }
                else
                {
                    index = 0;
                }
            }
            else
            {
                if (dv.Z > 0)
                {
                    index = 2;
                }
                else
                {
                    index = 1;
                }
            }
            return index;
        }
        private Vector3 IndexToFloor(int index)
        {
            switch (index)
            {
                case 0: return new(105, 0, 95);
                case 1: return new(95, 0, 95);
                case 2: return new(95, 0, 105);
                case 3: return new(105, 0, 105);
            }
            return default;
        }

        private bool isLeftJumpSkill(string skillId)
        {
            return (skillId == "37975" || skillId == "37965" || skillId == "37966");
        }

        private bool isFanSkill(string skillId)
        {
            //38959 右扇        982F
            //37975 左扇        9457
            return (skillId == "38959" || skillId == "37975");
        }

        private bool isOutSafeFirst(string skillId)
        {

            //37965 左跳右刀    944D
            //37966 左跳左刀    944E
            //37967 右跳右刀    944F
            //37968 右跳左刀    9450

            return (skillId == "37965" || skillId == "37968");
        }
    }
}

