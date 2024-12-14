using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
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
using Lumina.Excel.GeneratedSheets2;

namespace UsamisUsefulFunc
{


    public class Func
    {

        public ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
        public ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };

        /// <summary>
        /// �����ط�Ϊ�����ޣ�����Ϊ0������Ϊ1������Ϊ2������Ϊ3
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionFloorTo4Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: NE = 0, SE = 1, SW = 2, NW = 3
            var r = Math.Floor(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;
        }

        /// <summary>
        /// ��������X�ַ�Ϊ�����ޣ���Ϊ0����Ϊ1����Ϊ2����Ϊ3
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionRoundTo4Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, E = 1, S = 2, W = 3
            var r = Math.Round(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
            return (int)r;
        }

        /// <summary>
        /// ÿ45��Ϊ1�񣬴�������ʼ˳ʱ�룬0~7
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo8Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(4 - 4 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 8;
            return (int)r;

        }

        /// <summary>
        /// ÿ30��Ϊ1�񣬴�������ʼ˳ʱ�룬0~11
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo12Dir(Vector3 point, Vector3 centre)
        {
            // Dirs: N = 0, NE = 1, ..., NW = 7
            var r = Math.Round(6 - 6 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 12;
            return (int)r;
        }

        /// <summary>
        /// ÿ22.5��Ϊ1�񣬴�������ʼ˳ʱ�룬0~15
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <returns></returns>
        private int PositionTo16Dir(Vector3 point, Vector3 centre)
        {
            var r = Math.Round(8 - 8 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 16;
            return (int)r;
        }

        /// <summary>
        /// Χ��ĳ����ת�ض��Ƕ�
        /// </summary>
        /// <param name="point"></param>
        /// <param name="centre"></param>
        /// <param name="radian"></param>
        /// <returns></returns>
        private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
        {

            Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

            var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
            var length = v2.Length();
            return new(centre.X + MathF.Sin(rot) * length, centre.Y, centre.Z - MathF.Cos(rot) * length);
        }

        /// <summary>
        /// ��ĳ�������ָ����������ת�Ƕȣ�����µ�
        /// </summary>
        /// <param name="centre"></param>
        /// <param name="radian"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private Vector3 ExtendPoint(Vector3 centre, float radian, float length)
        {
            return new(centre.X + MathF.Sin(radian) * length, centre.Y, centre.Z - MathF.Cos(radian) * length);
        }

        private float FindAngle(Vector3 centre, Vector3 new_point)
        {
            float angle_rad = MathF.PI - MathF.Atan2(new_point.X - centre.X, new_point.Z - centre.Z);
            if (angle_rad < 0)
                angle_rad += 2 * MathF.PI;
            return angle_rad;
        }

        // ��Ѫ��
        private void getCharHpcur(Event @event, ScriptAccessory accessory)
        {
            var actor = (IBattleChara?)accessory.Data.Objects.SearchById(12345);
            var hp = actor.CurrentHp;
        }

        // ������ IDX
        private int getPlayerIdIndex(ScriptAccessory accessory, uint pid)
        {
            return accessory.Data.PartyList.IndexOf(pid);
        }

        // ������ְ�ܼ��
        private string getPlayerJobIndex(ScriptAccessory accessory, uint pid)
        {
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
}